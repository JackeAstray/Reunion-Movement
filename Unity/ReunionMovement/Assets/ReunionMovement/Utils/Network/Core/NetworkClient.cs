using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 高级网络客户端 —— 传输无关的统一客户端 API：
    /// 1. 统一 TCP / KCP / WebSocket / RawTcp 四种传输（NetworkChannelFactory）；
    /// 2. 可插拔编解码器（消息 ID / 长度前缀 / 原始透传），对接任意服务器协议；
    /// 3. 状态机 + 自动重连（指数退避 + 抖动）+ 连接超时；
    /// 4. 心跳发送 + 死链检测（heartbeatTimeout &gt; 0 时启用）；
    /// 5. 消息分发（按 ID / 强类型 / 请求响应 RPC 带超时）；
    /// 6. 线程安全：所有事件均在 Tick 调用线程（主线程）派发。
    /// 宿主需每帧调用 Tick()，或调用 DriveAsync(...).Forget() 自动驱动。
    /// </summary>
    public sealed class NetworkClient
    {
        /// <summary>客户端连接状态机</summary>
        public enum ClientState
        {
            /// <summary>空闲未连接</summary>
            Disconnected = 0,
            /// <summary>正在建立连接</summary>
            Connecting = 1,
            /// <summary>已连接</summary>
            Connected = 2,
            /// <summary>等待重连（倒计时中）</summary>
            Reconnecting = 3,
            /// <summary>已关闭（Close 后不可再用）</summary>
            Closed = 4,
        }

        readonly NetworkClientConfig config;
        readonly INetworkMessageCodec codec;
        readonly NetworkStreamAssembler assembler;
        readonly NetworkTypedProtocol typedProtocol = new NetworkTypedProtocol();
        readonly INetworkSerializer serializer;
        readonly NetworkMessageDispatcher dispatcher = new NetworkMessageDispatcher();
        readonly Dictionary<int, UniTaskCompletionSource<byte[]>> pendingRequests = new Dictionary<int, UniTaskCompletionSource<byte[]>>();

        INetworkClientChannel channel;
        ClientState state = ClientState.Disconnected;
        bool explicitStop;   // Disconnect()/Close() 置位：阻断自动重连
        bool closed;         // Close() 置位：对象终结，不可再用
        int reconnectAttempts;
        float reconnectTimer;
        bool reconnectWaiting;
        float connectingTimer;
        float heartbeatSendTimer;
        float lastReceiveTime;
        int rpcCorrelation;

        /// <summary>状态变化（旧状态, 新状态）</summary>
        public event Action<ClientState, ClientState> OnStateChanged;
        public event Action OnConnected;
        public event Action OnDisconnected;
        /// <summary>完整帧原始字节（含帧头，副本可安全持有）</summary>
        public event Action<byte[]> OnRawFrame;
        /// <summary>解码后的消息（消息 ID + 负载段，零拷贝，回调返回后失效）</summary>
        public event Action<ushort, ArraySegment<byte>> OnMessage;
        public event Action<string> OnError;

        public ClientState State => state;
        public bool IsConnected => state == ClientState.Connected && channel != null && channel.IsConnect;
        public NetworkClientConfig Config => config;
        public int ReconnectAttempts => reconnectAttempts;
        public INetworkClientChannel Channel => channel;
        public INetworkMessageCodec Codec => codec;
        /// <summary>消息分发器（RegisterHandler / OnUnknownMessage）</summary>
        public NetworkMessageDispatcher Dispatcher => dispatcher;

        public NetworkClient(NetworkClientConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.host)) throw new ArgumentException("host 不能为空", nameof(config));
            codec = NetworkCodecFactory.Create(config.codec);
            assembler = new NetworkStreamAssembler(codec, config.maxAssembledFrameSize);
            serializer = JsonNetSerializer.Instance;
        }

        #region 生命周期

        /// <summary>开始连接（可重复调用：关闭旧连接后重新建立，重置重连计数）</summary>
        public void Connect()
        {
            ThrowIfClosed();
            explicitStop = false;
            reconnectAttempts = 0;
            reconnectWaiting = false;
            reconnectTimer = 0f;
            CreateChannel();
        }

        /// <summary>主动断开：停止自动重连（可再次 Connect 复用本实例）</summary>
        public void Disconnect()
        {
            if (closed) return;
            explicitStop = true;
            reconnectWaiting = false;
            CloseChannel();
            SetState(ClientState.Disconnected);
        }

        /// <summary>彻底关闭：释放资源，此后不可再用（请重建 NetworkClient 实例）</summary>
        public void Close()
        {
            if (closed) return;
            closed = true;
            explicitStop = true;
            reconnectWaiting = false;
            CloseChannel();
            FailAllPending("客户端已关闭");
            SetState(ClientState.Closed);
        }

        void CreateChannel()
        {
            CloseChannel();
            if (closed) return;

            channel = NetworkChannelFactory.CreateClient(config.transport, config.channelName);
            channel.OnConnected += HandleConnected;
            channel.OnDataReceived += HandleData;
            channel.OnDisconnected += HandleDisconnected;
            channel.OnError += HandleError;
            RegisterToNetworkMgr(channel);

            connectingTimer = 0f;
            SetState(ClientState.Connecting);
            try
            {
                channel.Connect(config.host, config.port);
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkClient] 发起连接异常: {0}", ex.Message);
                HandleError("发起连接异常: " + ex.Message);
                CloseChannel();
                HandleDisconnected();
            }
        }

        void CloseChannel()
        {
            var old = channel;
            channel = null;
            if (old == null) return;
            old.OnConnected -= HandleConnected;
            old.OnDataReceived -= HandleData;
            old.OnDisconnected -= HandleDisconnected;
            old.OnError -= HandleError;
            try { old.Close(); } catch (Exception ex) { Log.Warning("[NetworkClient] 关闭通道异常: {0}", ex.Message); }
            UnregisterFromNetworkMgr(old);
        }
        #endregion

        #region 驱动

        /// <summary>驱动一帧（使用 unscaledDeltaTime）</summary>
        public void Tick()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>驱动一帧（自定义时间步长）</summary>
        public void Tick(float deltaTime)
        {
            if (closed) return;

            // 1) 重连倒计时
            if (reconnectWaiting)
            {
                reconnectTimer -= deltaTime;
                if (reconnectTimer <= 0f)
                {
                    reconnectWaiting = false;
                    if (!closed && !explicitStop)
                    {
                        CreateChannel();
                    }
                }
            }

            // 2) 连接超时
            if (state == ClientState.Connecting && config.connectTimeout > 0f)
            {
                connectingTimer += deltaTime;
                if (connectingTimer >= config.connectTimeout)
                {
                    Log.Warning("[NetworkClient] 连接超时（{0}s）", config.connectTimeout);
                    OnError?.Invoke($"连接超时（{config.connectTimeout}s）");
                    CloseChannel();
                    HandleDisconnected(); // 触发自动重连流程
                }
            }

            // 3) 心跳发送 + 死链检测
            if (state == ClientState.Connected && config.enableHeartbeat && config.heartbeatInterval > 0f)
            {
                heartbeatSendTimer += deltaTime;
                if (heartbeatSendTimer >= config.heartbeatInterval)
                {
                    heartbeatSendTimer = 0f;
                    SendHeartbeat();
                }
                if (config.heartbeatTimeout > 0f
                    && Time.realtimeSinceStartup - lastReceiveTime >= config.heartbeatTimeout)
                {
                    Log.Warning("[NetworkClient] 心跳超时（{0}s 未收到数据），判定死链", config.heartbeatTimeout);
                    OnError?.Invoke($"心跳超时（{config.heartbeatTimeout}s），判定死链");
                    CloseChannel();
                    HandleDisconnected();
                }
            }

            // 4) 通道刷新（事件在本帧内派发）
            channel?.TickRefresh();
        }

        /// <summary>
        /// 自动驱动（UniTask 循环，直到取消或 Close）。
        /// 用法：client.DriveAsync(ct).Forget(); 期间无需手动 Tick。
        /// </summary>
        public async UniTask DriveAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && !closed)
            {
                Tick();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken).SuppressCancellationThrow();
            }
        }
        #endregion

        #region 发送 / 接收 / 分发

        /// <summary>发送负载（消息 ID = 0）</summary>
        public bool Send(byte[] payload) => Send(NetworkConstants.DefaultMessageId, payload);

        /// <summary>发送消息（消息 ID + 负载，按编解码器组装帧）</summary>
        public bool Send(ushort messageId, byte[] payload)
        {
            if (!IsConnected)
            {
                Log.Warning("[NetworkClient] 未连接，发送失败");
                return false;
            }
            try
            {
                return channel.SendMessage(codec.Encode(messageId, payload));
            }
            catch (Exception ex)
            {
                OnError?.Invoke("发送异常: " + ex.Message);
                return false;
            }
        }

        /// <summary>发送 UTF-8 文本（消息 ID = 0）</summary>
        public bool SendString(string text)
        {
            return Send(Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        /// <summary>注册类型与消息 ID 的绑定（SendObject / RegisterObjectHandler 的前置步骤）</summary>
        public void RegisterObjectMessage<T>(ushort messageId)
        {
            typedProtocol.Register<T>(messageId);
        }

        /// <summary>发送强类型对象（JSON 序列化，类型需先注册）</summary>
        public bool SendObject<T>(T obj)
        {
            if (!typedProtocol.TryGetId(typeof(T), out var messageId))
            {
                Log.Error("[NetworkClient] 类型 {0} 未注册消息 ID，请先调用 RegisterObjectMessage", typeof(T).Name);
                return false;
            }
            byte[] data;
            try
            {
                data = serializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"对象序列化失败（{typeof(T).Name}）: " + ex.Message);
                return false;
            }
            return Send(messageId, data);
        }

        /// <summary>注册强类型消息处理器（类型需先注册消息 ID）</summary>
        public void RegisterObjectHandler<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!typedProtocol.TryGetId(typeof(T), out var messageId))
            {
                throw new InvalidOperationException($"类型 {typeof(T).Name} 未注册消息 ID，请先调用 RegisterObjectMessage");
            }
            dispatcher.RegisterHandler(messageId, payload =>
            {
                try
                {
                    handler(serializer.Deserialize<T>(payload.ToArray()));
                }
                catch (Exception ex)
                {
                    Log.Warning("[NetworkClient] 对象处理器异常（{0}）: {1}", typeof(T).Name, ex.Message);
                }
            });
        }

        void HandleConnected()
        {
            if (closed || channel == null) return;
            reconnectAttempts = 0;
            reconnectWaiting = false;
            lastReceiveTime = Time.realtimeSinceStartup;
            heartbeatSendTimer = 0f;
            SetState(ClientState.Connected);
            try { OnConnected?.Invoke(); } catch (Exception ex) { Log.Warning("[NetworkClient] OnConnected 回调异常: {0}", ex.Message); }
        }

        void HandleDisconnected()
        {
            if (closed) return;
            FailAllPending("连接已断开");
            SetState(ClientState.Disconnected);
            try { OnDisconnected?.Invoke(); } catch (Exception ex) { Log.Warning("[NetworkClient] OnDisconnected 回调异常: {0}", ex.Message); }
            if (config.autoReconnect && !explicitStop)
            {
                BeginReconnect();
            }
        }

        void HandleError(string error)
        {
            if (closed) return;
            Log.Warning("[NetworkClient] {0}", error);
            try { OnError?.Invoke(error); } catch (Exception ex) { Log.Warning("[NetworkClient] OnError 回调异常: {0}", ex.Message); }
        }

        void HandleData(byte[] data)
        {
            if (closed || data == null || data.Length == 0) return;
            lastReceiveTime = Time.realtimeSinceStartup;
            assembler.Feed(data, OnFrame);
        }

        void OnFrame(ushort messageId, ArraySegment<byte> frame, ArraySegment<byte> payload)
        {
            try { OnRawFrame?.Invoke(frame.ToArray()); }
            catch (Exception ex) { Log.Warning("[NetworkClient] OnRawFrame 回调异常: {0}", ex.Message); }

            // 系统帧：RPC 响应
            if (messageId == NetworkConstants.ReservedResponseMessageId)
            {
                if (NetworkRpcFrames.TryDecodeResponse(payload, out int correlationId, out var response))
                {
                    if (pendingRequests.TryGetValue(correlationId, out var tcs))
                    {
                        pendingRequests.Remove(correlationId);
                        tcs.TrySetResult(response.ToArray());
                    }
                    else
                    {
                        Log.Warning("[NetworkClient] 收到未知关联 ID {0} 的 RPC 响应", correlationId);
                    }
                }
                else
                {
                    Log.Warning("[NetworkClient] RPC 响应帧格式错误");
                }
                return;
            }

            // 系统帧：RPC 请求（仅服务端应处理）
            if (messageId == NetworkConstants.ReservedRequestMessageId)
            {
                Log.Warning("[NetworkClient] 收到 RPC 请求帧，已忽略（客户端不支持处理请求）");
                return;
            }

            try { OnMessage?.Invoke(messageId, payload); }
            catch (Exception ex) { Log.Warning("[NetworkClient] OnMessage 回调异常: {0}", ex.Message); }
            dispatcher.Dispatch(messageId, payload);
        }
        #endregion

        #region 自动重连 / 心跳

        void BeginReconnect()
        {
            if (closed || explicitStop || !config.autoReconnect) return;
            if (config.maxReconnectAttempts >= 0 && reconnectAttempts >= config.maxReconnectAttempts)
            {
                Log.Warning("[NetworkClient] 自动重连次数耗尽（{0} 次），停止重连", reconnectAttempts);
                OnError?.Invoke($"自动重连次数耗尽（{reconnectAttempts} 次），已停止");
                return; // 保持 Disconnected 状态
            }
            reconnectAttempts++;
            float delay = ComputeReconnectDelay();
            reconnectTimer = delay;
            reconnectWaiting = true;
            SetState(ClientState.Reconnecting);
            Log.Info("[NetworkClient] 将在 {0:0.0}s 后进行第 {1} 次重连", delay, reconnectAttempts);
        }

        float ComputeReconnectDelay()
        {
            float factor = Mathf.Pow(config.reconnectBackoffFactor, Mathf.Clamp(reconnectAttempts - 1, 0, 20));
            float delay = Mathf.Min(config.reconnectBaseDelay * factor, config.reconnectMaxDelay);
            if (config.reconnectJitter > 0f)
            {
                delay *= 1f - config.reconnectJitter + UnityEngine.Random.value * config.reconnectJitter * 2f;
            }
            return Mathf.Max(0.05f, delay);
        }

        void SendHeartbeat()
        {
            var text = config.heartbeatText ?? string.Empty;
            if (!SendString(text))
            {
                Log.Warning("[NetworkClient] 心跳发送失败");
            }
        }

        void FailAllPending(string message)
        {
            if (pendingRequests.Count == 0) return;
            foreach (var kv in pendingRequests)
            {
                kv.Value.TrySetException(new InvalidOperationException(message));
            }
            pendingRequests.Clear();
        }
        #endregion

        #region RPC 请求/响应

        /// <summary>
        /// 发送请求并等待响应（服务端需用 NetworkServer.RegisterRequestHandler 注册同 ID 处理器）。
        /// 超时抛 TimeoutException；连接断开/关闭抛 InvalidOperationException。
        /// </summary>
        public UniTask<byte[]> RequestAsync(ushort messageId, byte[] payload, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (state != ClientState.Connected || channel == null || !channel.IsConnect)
            {
                return UniTask.FromException<byte[]>(new InvalidOperationException("未连接，无法发起请求"));
            }
            int correlationId = Interlocked.Increment(ref rpcCorrelation);
            var requestFrame = NetworkRpcFrames.EncodeRequest(correlationId, messageId, payload);
            if (!Send(NetworkConstants.ReservedRequestMessageId, requestFrame))
            {
                return UniTask.FromException<byte[]>(new InvalidOperationException("发送请求失败"));
            }
            var tcs = new UniTaskCompletionSource<byte[]>();
            pendingRequests[correlationId] = tcs;
            return WaitResponseAsync(correlationId, tcs, timeout, cancellationToken);
        }

        /// <summary>RequestAsync 的秒数便捷重载</summary>
        public UniTask<byte[]> RequestAsync(ushort messageId, byte[] payload, float timeoutSeconds, CancellationToken cancellationToken = default)
        {
            return RequestAsync(messageId, payload, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        }

        /// <summary>强类型请求/响应（类型需先注册消息 ID）</summary>
        public async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (!typedProtocol.TryGetId(typeof(TRequest), out var messageId))
            {
                throw new InvalidOperationException($"类型 {typeof(TRequest).Name} 未注册消息 ID（先调用 RegisterObjectMessage）");
            }
            var response = await RequestAsync(messageId, serializer.Serialize(request), timeout, cancellationToken);
            return serializer.Deserialize<TResponse>(response);
        }

        async UniTask<byte[]> WaitResponseAsync(int correlationId, UniTaskCompletionSource<byte[]> tcs, TimeSpan timeout, CancellationToken ct)
        {
            try
            {
                var (hasResultLeft, result) = await UniTask.WhenAny(
                    tcs.Task,
                    UniTask.Delay(timeout, ignoreTimeScale: false, PlayerLoopTiming.Update, ct));
                if (hasResultLeft) return result;
                throw new TimeoutException($"请求超时（{timeout.TotalSeconds:0.0}s）");
            }
            finally
            {
                pendingRequests.Remove(correlationId);
            }
        }
        #endregion

        #region NetworkMgr 联动

        static void RegisterToNetworkMgr(INetworkChannel channel)
        {
            try
            {
                if (SingletonMgr<NetworkMgr>.IsInitialized)
                {
                    NetworkMgr.Instance.AddChannel(channel);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkClient] NetworkMgr 注册失败: {0}", ex.Message);
            }
        }

        static void UnregisterFromNetworkMgr(INetworkChannel channel)
        {
            try
            {
                if (SingletonMgr<NetworkMgr>.IsInitialized)
                {
                    NetworkMgr.Instance.ScheduleRemove(channel);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkClient] NetworkMgr 注销失败: {0}", ex.Message);
            }
        }
        #endregion

        void SetState(ClientState next)
        {
            if (state == next) return;
            var prev = state;
            state = next;
            try { OnStateChanged?.Invoke(prev, next); }
            catch (Exception ex) { Log.Warning("[NetworkClient] OnStateChanged 回调异常: {0}", ex.Message); }
        }

        void ThrowIfClosed()
        {
            if (closed) throw new InvalidOperationException("NetworkClient 已 Close，不可复用，请创建新实例");
        }
    }
}
