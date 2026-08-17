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
        // 可靠发送待确认表（seq → 确认信号；SendReliableAsync 使用）
        readonly Dictionary<int, UniTaskCompletionSource<bool>> pendingAcks = new Dictionary<int, UniTaskCompletionSource<bool>>();

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
        int reliableSeq; // 可靠发送序号（Interlocked 自增）

        // ===== 流量统计 / RTT =====
        /// <summary>累计发送字节数（协议帧，含帧头）</summary>
        public long BytesSent { get; private set; }
        /// <summary>累计接收字节数（原始字节流）</summary>
        public long BytesReceived { get; private set; }
        /// <summary>最近一次心跳往返时延（毫秒；0 = 尚无测量）</summary>
        public float LastRttMs { get; private set; }
        private float pingSentTime = -1f;

        // ===== 断线可靠消息重发队列（SendReliableAsync persistOnDisconnect=true 时使用）=====
        private readonly Queue<(ushort messageId, byte[] payload)> reliableResendQueue
            = new Queue<(ushort messageId, byte[] payload)>();
        private const int MaxReliableResendQueue = 256;

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

        /// <summary>发送结果（背压感知：区别于 Send 只回 bool）</summary>
        public enum SendResult
        {
            /// <summary>发送成功</summary>
            Ok,
            /// <summary>未连接</summary>
            NotConnected,
            /// <summary>通道拒绝（发送缓冲满/背压）或编码/发送异常</summary>
            Rejected,
        }

        /// <summary>
        /// 发送消息并返回详细结果（背压感知）：通道层返回 false 时统一归为 Rejected，
        /// 调用方可据此降速/丢弃非关键消息，与发送侧的速率限制构成全链路背压。
        /// </summary>
        public SendResult SendDetailed(ushort messageId, byte[] payload)
        {
            if (!IsConnected)
            {
                Log.Warning("[NetworkClient] 未连接，发送失败");
                return SendResult.NotConnected;
            }
            byte[] frame;
            try
            {
                frame = codec.Encode(messageId, payload);
            }
            catch (Exception ex)
            {
                OnError?.Invoke("编码异常: " + ex.Message);
                return SendResult.Rejected;
            }
            try
            {
                bool ok = channel.SendMessage(frame);
                if (ok) BytesSent += frame.Length;
                return ok ? SendResult.Ok : SendResult.Rejected;
            }
            catch (Exception ex)
            {
                OnError?.Invoke("发送异常: " + ex.Message);
                return SendResult.Rejected;
            }
        }

        /// <summary>发送消息（消息 ID + 负载，按编解码器组装帧）</summary>
        public bool Send(ushort messageId, byte[] payload)
        {
            return SendDetailed(messageId, payload) == SendResult.Ok;
        }

        /// <summary>发送 UTF-8 文本（消息 ID = 0）</summary>
        public bool SendString(string text)
        {
            return Send(Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        /// <summary>
        /// 可靠发送：服务端收到后回 ACK 确认，超时自动重发（适合支付/存档类重要消息）。
        /// 服务端自动解包并派发原消息（业务无需感知），返回 true 表示已收到 ACK。
        /// persistOnDisconnect=true 时，断线/重试耗尽会把消息放入重发队列，重连成功后自动补发。
        /// </summary>
        public async UniTask<bool> SendReliableAsync(ushort messageId, byte[] payload, TimeSpan timeout, int maxRetries = 5, bool persistOnDisconnect = false)
        {
            if (!IsConnected)
            {
                if (persistOnDisconnect) TryEnqueueReliable(messageId, payload);
                return false;
            }

            int seq = Interlocked.Increment(ref reliableSeq);
            byte[] framePayload = BuildReliablePayload(seq, messageId, payload);
            var tcs = new UniTaskCompletionSource<bool>();
            pendingAcks[seq] = tcs;

            try
            {
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    if (!IsConnected || !Send(NetworkConstants.ReservedAckMessageId, framePayload))
                    {
                        if (persistOnDisconnect) TryEnqueueReliable(messageId, payload);
                        return false;
                    }

                    // 必须用不抛异常的 TimeoutWithoutException：Timeout<T> 超时会抛 TimeoutException，
                    // 重试循环永远走不到；且超时计时用 UnscaledDeltaTime，游戏暂停时不悬挂。
                    // tcs 完成值：true=收到 ACK；false=连接断开（HandleDisconnected 置 false），
                    // 此路径不可误报成功（支付/存档类消息从未送达）。
                    var (isTimeout, ackResult) = await tcs.Task.TimeoutWithoutException(timeout, DelayType.UnscaledDeltaTime);
                    if (isTimeout) continue; // 超时：重发
                    if (!ackResult && persistOnDisconnect)
                    {
                        // 断线：消息未送达，入队待重连补发
                        TryEnqueueReliable(messageId, payload);
                    }
                    return ackResult;
                }
            }
            finally
            {
                pendingAcks.Remove(seq);
            }

            if (persistOnDisconnect) TryEnqueueReliable(messageId, payload);
            Log.Warning("[NetworkClient] 可靠发送 seq={0} 重试 {1} 次未获确认", seq, maxRetries);
            return false;
        }

        /// <summary>入队待重发的可靠消息（有界：防止断线期间无限积压）</summary>
        void TryEnqueueReliable(ushort messageId, byte[] payload)
        {
            if (reliableResendQueue.Count >= MaxReliableResendQueue)
            {
                Log.Warning("[NetworkClient] 可靠消息重发队列已满（{0}），丢弃 messageId={1}", MaxReliableResendQueue, messageId);
                return;
            }
            reliableResendQueue.Enqueue((messageId, payload));
        }

        /// <summary>重连成功后补发积压的可靠消息（persistOnDisconnect=false，避免失败自我回填成死循环）</summary>
        async UniTaskVoid DrainReliableResendQueue()
        {
            while (reliableResendQueue.Count > 0 && IsConnected && !closed)
            {
                var (id, payload) = reliableResendQueue.Dequeue();
                Log.Debug("[NetworkClient] 重连补发可靠消息 messageId={0}（剩余 {1}）", id, reliableResendQueue.Count);
                await SendReliableAsync(id, payload, TimeSpan.FromSeconds(Mathf.Max(1f, config.reconnectBaseDelay)), 2);
            }
        }

        /// <summary>打包可靠帧负载：[4B seq 小端][2B 原消息 ID 小端][原负载]</summary>
        static byte[] BuildReliablePayload(int seq, ushort messageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var result = new byte[6 + payload.Length];
            result[0] = (byte)seq;
            result[1] = (byte)(seq >> 8);
            result[2] = (byte)(seq >> 16);
            result[3] = (byte)(seq >> 24);
            result[4] = (byte)(messageId & 0xFF);
            result[5] = (byte)(messageId >> 8);
            Buffer.BlockCopy(payload, 0, result, 6, payload.Length);
            return result;
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
            // 重连成功：补发断线期间积压的可靠消息（persistOnDisconnect 队列）
            DrainReliableResendQueue().Forget();
        }

        void HandleDisconnected()
        {
            if (closed) return;
            FailAllPending("连接已断开");
            // 断开时结束全部可靠发送等待（重连后 seq 表已无意义）
            foreach (var kv in pendingAcks)
            {
                kv.Value.TrySetResult(false);
            }
            pendingAcks.Clear();
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
            BytesReceived += data.Length;
            lastReceiveTime = Time.realtimeSinceStartup;
            assembler.Feed(data, OnFrame);
        }

        void OnFrame(ushort messageId, ArraySegment<byte> frame, ArraySegment<byte> payload)
        {
            if (OnRawFrame != null)
            {
                try { OnRawFrame(frame.ToArray()); }
                catch (Exception ex) { Log.Warning("[NetworkClient] OnRawFrame 回调异常: {0}", ex.Message); }
            }

            // 系统帧：PONG（服务端心跳应答）—— 仅确认链路活跃（lastReceiveTime 已在 HandleData 更新），
            // 不派发给业务层
            if (messageId == NetworkConstants.ReservedPongMessageId)
            {
                if (pingSentTime > 0f)
                {
                    LastRttMs = Mathf.Max(0f, (Time.realtimeSinceStartup - pingSentTime) * 1000f);
                    pingSentTime = -1f;
                }
                return;
            }

            // 系统帧：ACK（可靠发送确认）—— 完成对应待确认任务，不派发给业务层
            if (messageId == NetworkConstants.ReservedAckMessageId)
            {
                if (payload.Count >= 4)
                {
                    var arr = payload.Array;
                    int off = payload.Offset;
                    int seq = arr[off] | (arr[off + 1] << 8) | (arr[off + 2] << 16) | (arr[off + 3] << 24);
                    if (pendingAcks.TryGetValue(seq, out var ackTcs))
                    {
                        pendingAcks.Remove(seq);
                        ackTcs.TrySetResult(true);
                    }
                }
                return;
            }

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
            // 协议级心跳：保留 ID PING 帧（不占用业务 ID 0、不混用 heartbeatText），
            // 服务端自动回 PONG 应答；配合服务端 idleTimeoutSeconds 完成死链双向检测。
            // 记录发送时刻：PONG 回来时计算 RTT（LastRttMs 供诊断/延迟补偿使用）
            pingSentTime = Time.realtimeSinceStartup;
            if (!Send(NetworkConstants.ReservedPingMessageId, Array.Empty<byte>()))
            {
                pingSentTime = -1f;
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
                    // 超时计时不受 timeScale 影响：游戏暂停（timeScale=0）时 RPC 不会悬挂到恢复
                    UniTask.Delay(timeout, ignoreTimeScale: true, PlayerLoopTiming.Update, ct));
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
