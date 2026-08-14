using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 高级网络服务端 —— 传输无关的统一服务端 API：
    /// 1. 统一 TCP / KCP / WebSocket / RawTcp 四种传输（NetworkChannelFactory）；
    /// 2. 可插拔编解码器（与客户端保持一致即可对接任何协议的客户端）；
    /// 3. 连接注册表（地址 / 时长 / 流量统计 / 用户标记）+ 每连接独立消息分发器；
    /// 4. 单发 / 广播 / 排除广播；
    /// 5. RPC 请求处理（RegisterRequestHandler，响应客户端 RequestAsync）；
    /// 6. 强类型对象消息（BroadcastObject / RegisterObjectHandler）。
    /// 线程安全：所有事件均在 Tick 调用线程（主线程）派发；宿主每帧调用 Tick()。
    /// </summary>
    public sealed class NetworkServer
    {
        readonly NetworkServerConfig config;
        readonly INetworkMessageCodec codec;
        readonly NetworkTypedProtocol typedProtocol = new NetworkTypedProtocol();
        readonly INetworkSerializer serializer = JsonNetSerializer.Instance;
        readonly Dictionary<int, ServerConnection> connections = new Dictionary<int, ServerConnection>();
        readonly Dictionary<ushort, Func<int, byte[], byte[]>> requestHandlers = new Dictionary<ushort, Func<int, byte[], byte[]>>();
        readonly Dictionary<Type, Action<int, object>> objectHandlers = new Dictionary<Type, Action<int, object>>();

        INetworkServerChannel channel;
        bool started;

        /// <summary>服务启动</summary>
        public event Action OnStarted;
        /// <summary>服务停止</summary>
        public event Action OnStopped;
        /// <summary>客户端接入（连接 ID, 地址）</summary>
        public event Action<int, string> OnClientConnected;
        /// <summary>客户端断开（连接 ID）</summary>
        public event Action<int> OnClientDisconnected;
        /// <summary>完整帧原始字节（含帧头，副本可安全持有）</summary>
        public event Action<int, byte[]> OnRawFrame;
        /// <summary>解码后的消息（连接 ID, 消息 ID, 负载段）</summary>
        public event Action<int, ushort, ArraySegment<byte>> OnMessage;
        /// <summary>错误（连接 ID；全局错误为 -1）</summary>
        public event Action<int, string> OnError;

        public bool IsActive => started && channel != null && channel.Active;
        public bool Started => started;
        public int ClientCount => connections.Count;
        public NetworkServerConfig Config => config;
        public INetworkServerChannel Channel => channel;
        public INetworkMessageCodec Codec => codec;
        /// <summary>已连接客户端 ID（只读视图，遍历时勿增删连接）</summary>
        public IReadOnlyCollection<int> ConnectionIds => connections.Keys;

        public NetworkServer(NetworkServerConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            codec = NetworkCodecFactory.Create(config.codec);
        }

        #region 生命周期

        /// <summary>启动监听；失败返回 false 并派发 OnError(-1)</summary>
        public bool Start()
        {
            if (started) return true;
            channel = NetworkChannelFactory.CreateServer(config.transport, config.channelName, config.port);
            channel.OnConnected += HandleConnected;
            channel.OnDataReceived += HandleData;
            channel.OnDisconnected += HandleDisconnected;
            channel.OnError += HandleError;
            RegisterToNetworkMgr(channel);

            if (!channel.Start())
            {
                Log.Error("[NetworkServer] 服务端启动失败（端口 {0} 可能被占用）", config.port);
                CloseChannel();
                try { OnError?.Invoke(-1, $"服务端启动失败（端口 {config.port} 可能被占用）"); }
                catch (Exception ex) { Log.Warning("[NetworkServer] OnError 回调异常: {0}", ex.Message); }
                return false;
            }
            started = true;
            try { OnStarted?.Invoke(); } catch (Exception ex) { Log.Warning("[NetworkServer] OnStarted 回调异常: {0}", ex.Message); }
            return true;
        }

        /// <summary>停止监听并断开全部客户端</summary>
        public void Stop()
        {
            if (!started) return;
            CloseChannel();
            foreach (var kv in connections)
            {
                kv.Value.Dispatcher.ClearHandlers();
            }
            connections.Clear();
            started = false;
            try { OnStopped?.Invoke(); } catch (Exception ex) { Log.Warning("[NetworkServer] OnStopped 回调异常: {0}", ex.Message); }
        }

        /// <summary>驱动一帧（服务端事件在本帧内派发）</summary>
        public void Tick()
        {
            if (started) channel?.TickRefresh();
        }
        #endregion

        #region 发送 / 断开 / 查询

        /// <summary>发送负载到指定客户端（消息 ID = 0）</summary>
        public bool Send(int connectionId, byte[] payload) => Send(connectionId, NetworkConstants.DefaultMessageId, payload);

        /// <summary>发送消息到指定客户端（消息 ID + 负载）</summary>
        public bool Send(int connectionId, ushort messageId, byte[] payload)
        {
            if (!IsActive || !connections.ContainsKey(connectionId)) return false;
            var frame = codec.Encode(messageId, payload);
            var ok = channel.SendMessage(connectionId, frame);
            if (ok && connections.TryGetValue(connectionId, out var conn))
            {
                conn.Info.BytesSent += frame.Length;
            }
            return ok;
        }

        /// <summary>广播负载到全部客户端（消息 ID = 0）</summary>
        public void Broadcast(byte[] payload) => Broadcast(NetworkConstants.DefaultMessageId, payload);

        /// <summary>广播消息到全部客户端</summary>
        public void Broadcast(ushort messageId, byte[] payload)
        {
            var frame = codec.Encode(messageId, payload);
            foreach (var id in connections.Keys)
            {
                if (channel.SendMessage(id, frame) && connections.TryGetValue(id, out var conn))
                {
                    conn.Info.BytesSent += frame.Length;
                }
            }
        }

        /// <summary>广播负载（排除指定客户端）</summary>
        public void BroadcastExcept(int exceptConnectionId, byte[] payload)
            => BroadcastExcept(exceptConnectionId, NetworkConstants.DefaultMessageId, payload);

        /// <summary>广播消息（排除指定客户端）</summary>
        public void BroadcastExcept(int exceptConnectionId, ushort messageId, byte[] payload)
        {
            var frame = codec.Encode(messageId, payload);
            foreach (var id in connections.Keys)
            {
                if (id == exceptConnectionId) continue;
                if (channel.SendMessage(id, frame) && connections.TryGetValue(id, out var conn))
                {
                    conn.Info.BytesSent += frame.Length;
                }
            }
        }

        /// <summary>断开指定客户端</summary>
        public bool DisconnectClient(int connectionId)
        {
            return channel.Disconnect(connectionId);
        }

        /// <summary>获取连接地址</summary>
        public string GetConnectionAddress(int connectionId)
        {
            return channel.GetConnectionAddress(connectionId);
        }

        /// <summary>获取连接元数据（未连接返回 null）</summary>
        public NetworkConnectionInfo GetConnectionInfo(int connectionId)
        {
            return connections.TryGetValue(connectionId, out var conn) ? conn.Info : null;
        }

        /// <summary>获取指定连接的独立消息分发器（未连接返回 null）</summary>
        public NetworkMessageDispatcher GetDispatcher(int connectionId)
        {
            return connections.TryGetValue(connectionId, out var conn) ? conn.Dispatcher : null;
        }
        #endregion

        #region 强类型对象消息

        /// <summary>注册类型与消息 ID 的绑定</summary>
        public void RegisterObjectMessage<T>(ushort messageId)
        {
            typedProtocol.Register<T>(messageId);
        }

        /// <summary>注册强类型对象处理器（所有连接共享；类型需先注册消息 ID）</summary>
        public void RegisterObjectHandler<T>(Action<int, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!typedProtocol.TryGetId(typeof(T), out _))
            {
                throw new InvalidOperationException($"类型 {typeof(T).Name} 未注册消息 ID，请先调用 RegisterObjectMessage");
            }
            objectHandlers[typeof(T)] = (Action<int, object>)((connectionId, obj) => handler(connectionId, (T)obj));
        }

        /// <summary>发送强类型对象到指定客户端</summary>
        public bool SendObject<T>(int connectionId, T obj)
        {
            if (!typedProtocol.TryGetId(typeof(T), out var messageId))
            {
                Log.Error("[NetworkServer] 类型 {0} 未注册消息 ID", typeof(T).Name);
                return false;
            }
            byte[] data;
            try
            {
                data = serializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkServer] 对象序列化失败（{0}）: {1}", typeof(T).Name, ex.Message);
                return false;
            }
            return Send(connectionId, messageId, data);
        }

        /// <summary>广播强类型对象到全部客户端</summary>
        public void BroadcastObject<T>(T obj)
        {
            if (!typedProtocol.TryGetId(typeof(T), out var messageId))
            {
                Log.Error("[NetworkServer] 类型 {0} 未注册消息 ID", typeof(T).Name);
                return;
            }
            byte[] data;
            try
            {
                data = serializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkServer] 对象序列化失败（{0}）: {1}", typeof(T).Name, ex.Message);
                return;
            }
            Broadcast(messageId, data);
        }
        #endregion

        #region RPC 请求处理

        /// <summary>注册 RPC 处理器（响应客户端的 NetworkClient.RequestAsync）</summary>
        public void RegisterRequestHandler(ushort messageId, Func<int, byte[], byte[]> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            requestHandlers[messageId] = handler;
        }

        /// <summary>注销 RPC 处理器</summary>
        public bool UnregisterRequestHandler(ushort messageId)
        {
            return requestHandlers.Remove(messageId);
        }

        /// <summary>注册强类型 RPC 处理器（类型需先注册消息 ID）</summary>
        public void RegisterRequestHandler<TRequest, TResponse>(ushort messageId, Func<int, TRequest, TResponse> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!typedProtocol.TryGetId(typeof(TRequest), out _))
            {
                throw new InvalidOperationException($"类型 {typeof(TRequest).Name} 未注册消息 ID，请先调用 RegisterObjectMessage");
            }
            RegisterRequestHandler(messageId, (connectionId, payload) =>
            {
                var request = serializer.Deserialize<TRequest>(payload);
                var response = handler(connectionId, request);
                return serializer.Serialize(response);
            });
        }

        void HandleRpcRequest(int connectionId, ArraySegment<byte> payload)
        {
            if (!NetworkRpcFrames.TryDecodeRequest(payload, out int correlationId, out ushort targetMessageId, out var requestPayload))
            {
                OnError?.Invoke(connectionId, "RPC 请求帧格式错误");
                return;
            }
            byte[] response = Array.Empty<byte>();
            if (requestHandlers.TryGetValue(targetMessageId, out var handler))
            {
                try
                {
                    response = handler(connectionId, requestPayload.ToArray()) ?? Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    Log.Warning("[NetworkServer] RPC 请求 {0} 处理器异常: {1}", targetMessageId, ex.Message);
                    OnError?.Invoke(connectionId, $"RPC 请求 {targetMessageId} 处理器异常: " + ex.Message);
                }
            }
            else
            {
                Log.Warning("[NetworkServer] 未注册的 RPC 请求 ID: {0}", targetMessageId);
                OnError?.Invoke(connectionId, $"未注册的 RPC 请求 ID: {targetMessageId}");
            }
            // 无论成败均回执（异常/未注册回空响应），避免请求方一直等待
            Send(connectionId, NetworkConstants.ReservedResponseMessageId, NetworkRpcFrames.EncodeResponse(correlationId, response));
        }
        #endregion

        #region 内部：连接事件处理

        void HandleConnected(int connectionId, string address)
        {
            if (connections.ContainsKey(connectionId)) return;
            connections[connectionId] = new ServerConnection(connectionId, address, codec, config.maxAssembledFrameSize);
            try { OnClientConnected?.Invoke(connectionId, address); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnClientConnected 回调异常: {0}", ex.Message); }
        }

        void HandleDisconnected(int connectionId)
        {
            if (!connections.Remove(connectionId, out var conn)) return;
            conn.Dispatcher.ClearHandlers();
            try { OnClientDisconnected?.Invoke(connectionId); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnClientDisconnected 回调异常: {0}", ex.Message); }
        }

        void HandleError(int connectionId, string error)
        {
            Log.Warning("[NetworkServer] id={0} 错误: {1}", connectionId, error);
            try { OnError?.Invoke(connectionId, error); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnError 回调异常: {0}", ex.Message); }
        }

        void HandleData(int connectionId, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            if (!connections.TryGetValue(connectionId, out var conn)) return;
            conn.Info.BytesReceived += data.Length;
            conn.Info.LastReceiveTime = Time.realtimeSinceStartup;
            conn.Assembler.Feed(data, (messageId, frame, payload) => HandleFrame(connectionId, conn, messageId, frame, payload));
        }

        void HandleFrame(int connectionId, ServerConnection conn, ushort messageId, ArraySegment<byte> frame, ArraySegment<byte> payload)
        {
            // 系统帧：RPC 请求
            if (messageId == NetworkConstants.ReservedRequestMessageId)
            {
                HandleRpcRequest(connectionId, payload);
                return;
            }
            // 系统帧：RPC 响应（服务端不应收到）
            if (messageId == NetworkConstants.ReservedResponseMessageId)
            {
                Log.Warning("[NetworkServer] 收到 RPC 响应帧，已忽略");
                return;
            }

            try { OnRawFrame?.Invoke(connectionId, frame.ToArray()); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnRawFrame 回调异常: {0}", ex.Message); }

            try { OnMessage?.Invoke(connectionId, messageId, payload); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnMessage 回调异常: {0}", ex.Message); }

            conn.Dispatcher.Dispatch(messageId, payload);

            // 强类型对象处理器
            if (typedProtocol.TryGetType(messageId, out var type) && objectHandlers.TryGetValue(type, out var handler))
            {
                object obj;
                try
                {
                    obj = serializer.Deserialize(payload.ToArray(), type);
                }
                catch (Exception ex)
                {
                    Log.Warning("[NetworkServer] 对象反序列化失败（{0}）: {1}", type.Name, ex.Message);
                    return;
                }
                try
                {
                    handler(connectionId, obj);
                }
                catch (Exception ex)
                {
                    Log.Warning("[NetworkServer] 对象处理器异常（{0}）: {1}", type.Name, ex.Message);
                }
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
            try { old.Close(); } catch (Exception ex) { Log.Warning("[NetworkServer] 关闭通道异常: {0}", ex.Message); }
            UnregisterFromNetworkMgr(old);
        }

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
                Log.Warning("[NetworkServer] NetworkMgr 注册失败: {0}", ex.Message);
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
                Log.Warning("[NetworkServer] NetworkMgr 注销失败: {0}", ex.Message);
            }
        }
        #endregion

        /// <summary>服务端连接内部状态（信息 + 组装器 + 独立分发器）</summary>
        sealed class ServerConnection
        {
            public readonly NetworkConnectionInfo Info;
            public readonly NetworkStreamAssembler Assembler;
            public readonly NetworkMessageDispatcher Dispatcher = new NetworkMessageDispatcher();

            public ServerConnection(int connectionId, string address, INetworkMessageCodec codec, int maxFrameSize)
            {
                Info = new NetworkConnectionInfo(connectionId, address);
                Assembler = new NetworkStreamAssembler(codec, maxFrameSize);
            }
        }
    }
}
