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
        // 已鉴权连接集合（config.requireRpcAuthentication 开启时生效）
        readonly HashSet<int> authenticatedConnections = new HashSet<int>();
        // TickIdleTimeout 的复用缓冲（避免每帧 new List 分配）
        readonly List<int> idleExpiredBuffer = new List<int>();

        // 加密握手（config.enableEncryptedHandshake 开启时生效）
        bool handshakeEnabled;
        byte[] handshakeMasterKey;

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
        /// <summary>客户端完成加密握手（连接 ID；enableEncryptedHandshake 开启时，业务通信应等待此事件）</summary>
        public event Action<int> OnClientSecured;
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
            handshakeEnabled = config.enableEncryptedHandshake;
            handshakeMasterKey = config.handshakeMasterKey;
            if (handshakeEnabled && (handshakeMasterKey == null || handshakeMasterKey.Length != 32))
            {
                Log.Warning("[NetworkServer] 启用加密握手但未提供 32 字节主密钥（等待 SetHandshakeMasterKey 运行时下发）");
                handshakeMasterKey = null;
            }
        }

        /// <summary>运行时下发握手主密钥（32 字节）。生产环境应通过 HTTPS 登录接口下发，替代硬编码常量</summary>
        public void SetHandshakeMasterKey(byte[] masterKey)
        {
            if (masterKey == null || masterKey.Length != 32)
            {
                Log.Error("[NetworkServer] SetHandshakeMasterKey：主密钥必须为 32 字节");
                return;
            }
            handshakeMasterKey = masterKey;
        }

        /// <summary>查询连接是否已完成加密握手（enableEncryptedHandshake 关闭时恒为 false）</summary>
        public bool IsConnectionSecured(int connectionId)
        {
            return connections.TryGetValue(connectionId, out var conn) && conn.Secured;
        }

        #region 生命周期

        /// <summary>启动监听；失败返回 false 并派发 OnError(-1)</summary>
        public bool Start()
        {
            if (started) return true;
            channel = NetworkChannelFactory.CreateServer(config.transport, config.channelName, config.port);
            // RawTcp 双套空闲超时统一：通道层 IdleTimeoutSeconds 未单独配置时沿用 config.idleTimeoutSeconds，
            // 避免"NetworkServer 配了踢人时间但通道层不生效"的静默失效
            if (channel is RawTcpServerChannel raw && raw.IdleTimeoutSeconds <= 0f && config.idleTimeoutSeconds > 0f)
            {
                raw.IdleTimeoutSeconds = config.idleTimeoutSeconds;
            }
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
            authenticatedConnections.Clear();
            started = false;
            try { OnStopped?.Invoke(); } catch (Exception ex) { Log.Warning("[NetworkServer] OnStopped 回调异常: {0}", ex.Message); }
        }

        /// <summary>驱动一帧（服务端事件在本帧内派发 + 空闲超时回收）</summary>
        public void Tick()
        {
            if (!started) return;
            channel?.TickRefresh();
            TickIdleTimeout();
        }

        /// <summary>空闲超时回收：超过 idleTimeoutSeconds 未收到任何数据的连接被断开
        /// （客户端启用心跳后定期发 PING，活跃连接不会误伤；半开/静默连接被清理）</summary>
        void TickIdleTimeout()
        {
            if (config.idleTimeoutSeconds <= 0f || connections.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            idleExpiredBuffer.Clear();
            foreach (var kv in connections)
            {
                if (now - kv.Value.Info.LastReceiveTime > config.idleTimeoutSeconds)
                {
                    idleExpiredBuffer.Add(kv.Key);
                }
            }
            foreach (var id in idleExpiredBuffer)
            {
                Log.Warning("[NetworkServer] 连接 {0} 空闲超时（{1}s 未收到数据），已断开", id, config.idleTimeoutSeconds);
                DisconnectClient(id);
            }
        }
        #endregion

        #region 发送 / 断开 / 查询

        /// <summary>发送负载到指定客户端（消息 ID = 0）</summary>
        public bool Send(int connectionId, byte[] payload) => Send(connectionId, NetworkConstants.DefaultMessageId, payload);

        /// <summary>发送消息到指定客户端（消息 ID + 负载）</summary>
        public bool Send(int connectionId, ushort messageId, byte[] payload)
        {
            if (!IsActive || !connections.TryGetValue(connectionId, out var conn)) return false;
            // 加密握手：未完成握手的连接仅允许系统帧（业务帧该连接尚无法解密，静默丢弃）
            if (handshakeEnabled && !conn.Secured && !NetworkConstants.IsReservedMessageId(messageId))
            {
                return false;
            }
            var frame = conn.Codec.Encode(messageId, payload);
            var ok = channel.SendMessage(connectionId, frame);
            if (ok)
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

        /// <summary>标记连接为已鉴权（开启 config.requireRpcAuthentication 后，未鉴权连接的 RPC 将被拒绝）。
        /// 应在业务登录/握手验证成功后调用；连接断开时自动失效。</summary>
        public void MarkConnectionAuthenticated(int connectionId)
        {
            authenticatedConnections.Add(connectionId);
        }

        /// <summary>查询连接是否已鉴权</summary>
        public bool IsConnectionAuthenticated(int connectionId)
        {
            return authenticatedConnections.Contains(connectionId);
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
                TryNotifyError(connectionId, "RPC 请求帧格式错误");
                return;
            }
            // 鉴权闸门：开启 requireRpcAuthentication 后，未鉴权连接的 RPC 一律拒绝（防越权调用）
            if (config.requireRpcAuthentication && !authenticatedConnections.Contains(connectionId))
            {
                Log.Warning("[NetworkServer] 未鉴权连接 {0} 调用 RPC {1}，已拒绝", connectionId, targetMessageId);
                TryNotifyError(connectionId, $"连接未鉴权，无法调用 RPC {targetMessageId}");
                Send(connectionId, NetworkConstants.ReservedResponseMessageId, NetworkRpcFrames.EncodeResponse(correlationId, Array.Empty<byte>()));
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
                    TryNotifyError(connectionId, $"RPC 请求 {targetMessageId} 处理器异常: " + ex.Message);
                }
            }
            else
            {
                Log.Warning("[NetworkServer] 未注册的 RPC 请求 ID: {0}", targetMessageId);
                TryNotifyError(connectionId, $"未注册的 RPC 请求 ID: {targetMessageId}");
            }
            // 无论成败均回执（异常/未注册回空响应），避免请求方一直等待
            Send(connectionId, NetworkConstants.ReservedResponseMessageId, NetworkRpcFrames.EncodeResponse(correlationId, response));
        }

        /// <summary>OnError 订阅者异常隔离（与 HandleError 一致，异常不得沿派发链崩掉主循环）</summary>
        void TryNotifyError(int connectionId, string message)
        {
            try { OnError?.Invoke(connectionId, message); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnError 回调异常: {0}", ex.Message); }
        }
        #endregion

        #region 内部：连接事件处理

        void HandleConnected(int connectionId, string address)
        {
            if (connections.ContainsKey(connectionId)) return;
            connections[connectionId] = new ServerConnection(connectionId, address, codec, config.maxAssembledFrameSize);
            // 加密握手：向新连接下发服务端随机数（明文；客户端回 ClientHello 后本连接切换为加密）
            if (handshakeEnabled && connections.TryGetValue(connectionId, out var handshakeConn))
            {
                handshakeConn.ServerNonce = NetworkHandshake.GenerateNonce();
                Send(connectionId, NetworkConstants.ReservedHandshakeServerHello, handshakeConn.ServerNonce);
            }
            try { OnClientConnected?.Invoke(connectionId, address); }
            catch (Exception ex) { Log.Warning("[NetworkServer] OnClientConnected 回调异常: {0}", ex.Message); }
        }

        void HandleDisconnected(int connectionId)
        {
            if (!connections.Remove(connectionId, out var conn)) return;
            conn.Dispatcher.ClearHandlers();
            authenticatedConnections.Remove(connectionId); // 断开后鉴权状态失效，防止连接 ID 复用时越权
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

        /// <summary>ACK 回包 [4B seq] 复用缓冲（Send→codec.Encode 同步拷贝，主线程无重入风险，零分配）</summary>
        static readonly byte[] ackSeqBuffer = new byte[4];

        void HandleFrame(int connectionId, ServerConnection conn, ushort messageId, ArraySegment<byte> frame, ArraySegment<byte> payload)
        {
            // 系统帧：心跳 PING（客户端→服务端）—— 自动回 PONG 应答，不计入速率限制
            if (messageId == NetworkConstants.ReservedPingMessageId)
            {
                Send(connectionId, NetworkConstants.ReservedPongMessageId, Array.Empty<byte>());
                return;
            }
            // 系统帧：PONG（客户端不应主动发送）
            if (messageId == NetworkConstants.ReservedPongMessageId)
            {
                Log.Warning("[NetworkServer] 连接 {0} 主动发送 PONG 帧，已忽略", connectionId);
                return;
            }

            // 加密握手（服务端侧）：收到 ClientHello → 派生会话密钥并切换该连接为加密
            if (handshakeEnabled)
            {
                if (messageId == NetworkConstants.ReservedHandshakeClientHello)
                {
                    if (payload.Count != NetworkHandshake.NonceLength)
                    {
                        Log.Warning("[NetworkServer] 连接 {0} ClientHello 随机数长度非法，已忽略", connectionId);
                        return;
                    }
                    if (conn.Secured)
                    {
                        Log.Warning("[NetworkServer] 连接 {0} 重复 ClientHello，已忽略", connectionId);
                        return;
                    }
                    if (handshakeMasterKey == null)
                    {
                        Log.Error("[NetworkServer] 连接 {0} 发起 ClientHello 但主密钥尚未下发，无法完成握手", connectionId);
                        return;
                    }
                    byte[] clientNonce = payload.ToArray();
                    byte[] sessionKey = NetworkHandshake.DeriveSessionKey(handshakeMasterKey, conn.ServerNonce, clientNonce);
                    conn.SwapToEncrypted(EncryptedCodec.Wrap(NetworkCodecFactory.Create(config.codec), sessionKey));
                    try { OnClientSecured?.Invoke(connectionId); }
                    catch (Exception ex) { Log.Warning("[NetworkServer] OnClientSecured 回调异常: {0}", ex.Message); }
                    return;
                }
                if (messageId == NetworkConstants.ReservedHandshakeServerHello)
                {
                    Log.Warning("[NetworkServer] 连接 {0} 发送 ServerHello 帧，已忽略", connectionId);
                    return;
                }
                // 防御：握手未完成仅放行协议保活帧（PING/PONG/ACK），RPC 与业务帧一律静默丢弃
                // （防跳过握手直接调用 RPC / 发送业务帧）
                if (!conn.Secured
                    && messageId != NetworkConstants.ReservedPingMessageId
                    && messageId != NetworkConstants.ReservedPongMessageId
                    && messageId != NetworkConstants.ReservedAckMessageId)
                {
                    return;
                }
            }
            // 系统帧：可靠消息（客户端 SendReliableAsync）—— 解包 [seq][原消息 ID][负载]，
            // 先回 ACK（及时解除客户端重发），再按原消息递归派发
            if (messageId == NetworkConstants.ReservedAckMessageId)
            {
                if (payload.Count >= 6)
                {
                    var arr = payload.Array;
                    int off = payload.Offset;
                    int seq = arr[off] | (arr[off + 1] << 8) | (arr[off + 2] << 16) | (arr[off + 3] << 24);
                    ushort innerId = (ushort)(arr[off + 4] | (arr[off + 5] << 8));
                    // 防御：内嵌消息不得为系统保留帧（防嵌套递归/伪造 ACK 循环）
                    if (NetworkConstants.IsReservedMessageId(innerId))
                    {
                        Log.Warning("[NetworkServer] 连接 {0} 可靠帧内嵌系统保留 ID {1}，已忽略", connectionId, innerId);
                        return;
                    }
                    // ACK 去重：ACK 丢失时客户端会重发同一 seq，若不记录已处理 seq 会重复派发业务消息
                    // （支付/存档类消息 at-least-once 且无幂等保护）。重复 seq 仍回 ACK 止住客户端重发，但不再派发
                    bool isDuplicate = !conn.TryMarkReliableSeq(seq);
                    // 回 ACK：[4B seq]（复用静态缓冲，Encode 同步拷贝无持有风险）
                    ackSeqBuffer[0] = (byte)seq;
                    ackSeqBuffer[1] = (byte)(seq >> 8);
                    ackSeqBuffer[2] = (byte)(seq >> 16);
                    ackSeqBuffer[3] = (byte)(seq >> 24);
                    Send(connectionId, NetworkConstants.ReservedAckMessageId, ackSeqBuffer);
                    if (isDuplicate) return;
                    // 按原消息递归派发（业务对可靠发送无感知）。frame 传合成帧段
                    // （[seq][原ID][负载] 均位于 off 起的连续内存），不能传 default：
                    // 空段的 ToArray 会抛 ArgumentException，导致每条可靠消息刷一条错误日志
                    // 且 OnRawFrame 订阅者拿不到可靠帧原文
                    HandleFrame(connectionId, conn, innerId,
                        new ArraySegment<byte>(arr, off, 6 + payload.Count - 6),
                        new ArraySegment<byte>(arr, off + 6, payload.Count - 6));
                    return;
                }
                Log.Warning("[NetworkServer] 连接 {0} 可靠帧格式错误，已忽略", connectionId);
                return;
            }

            // 消息速率限制：单连接超限丢弃本帧（防刷消息 DoS）。
            // 置于 RPC 分支之前：RPC 洪水同样受限；可靠帧在解包递归后按内层消息计一次（不重复计外层 ACK 帧）
            if (config.maxMessagesPerSecond > 0)
            {
                float now = Time.realtimeSinceStartup;
                if (now - conn.MessageWindowStart >= 1f)
                {
                    conn.MessageWindowStart = now;
                    conn.MessageCountInWindow = 0;
                }
                if (++conn.MessageCountInWindow > config.maxMessagesPerSecond)
                {
                    conn.DroppedMessages++;
                    // 低频告警：每丢弃 100 条记一次，避免刷屏
                    if (conn.DroppedMessages % 100 == 1)
                    {
                        Log.Warning("[NetworkServer] 连接 {0} 消息速率超限（>{1}/s），丢弃本帧（累计丢弃 {2}）",
                            connectionId, config.maxMessagesPerSecond, conn.DroppedMessages);
                    }
                    return;
                }
            }

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

            if (OnRawFrame != null)
            {
                try { OnRawFrame(connectionId, frame.ToArray()); }
                catch (Exception ex) { Log.Warning("[NetworkServer] OnRawFrame 回调异常: {0}", ex.Message); }
            }

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

        /// <summary>服务端连接内部状态（信息 + 组装器 + 独立分发器 + 速率限制窗口）</summary>
        sealed class ServerConnection
        {
            public readonly NetworkConnectionInfo Info;
            public readonly NetworkStreamAssembler Assembler;
            public readonly NetworkMessageDispatcher Dispatcher = new NetworkMessageDispatcher();

            /// <summary>本连接收发使用的编解码器：默认 = 服务端全局 codec；加密握手完成后替换为加密 codec</summary>
            public INetworkMessageCodec Codec;
            /// <summary>加密握手是否已完成（enableEncryptedHandshake 关闭时恒为 false）</summary>
            public bool Secured;
            /// <summary>本连接的服务端随机数（握手 ServerHello 下发）</summary>
            public byte[] ServerNonce;

            // 速率限制窗口（主线程访问）
            public float MessageWindowStart;
            public int MessageCountInWindow;
            public int DroppedMessages;

            // 可靠消息 seq 去重（主线程访问）：ACQ 丢失重发时防止重复派发业务消息
            const int MaxTrackedSeqs = 256;
            readonly HashSet<int> recentReliableSeqs = new HashSet<int>();
            readonly Queue<int> reliableSeqOrder = new Queue<int>();

            /// <summary>标记可靠消息 seq 已处理；返回 false 表示重复 seq（已处理过）</summary>
            public bool TryMarkReliableSeq(int seq)
            {
                if (!recentReliableSeqs.Add(seq)) return false;
                reliableSeqOrder.Enqueue(seq);
                while (reliableSeqOrder.Count > MaxTrackedSeqs)
                {
                    recentReliableSeqs.Remove(reliableSeqOrder.Dequeue());
                }
                return true;
            }

            public ServerConnection(int connectionId, string address, INetworkMessageCodec codec, int maxFrameSize)
            {
                Info = new NetworkConnectionInfo(connectionId, address);
                Codec = codec;
                Assembler = new NetworkStreamAssembler(codec, maxFrameSize);
                MessageWindowStart = Time.realtimeSinceStartup;
            }

            /// <summary>切换本连接为加密模式（发送与接收 codec 同步替换，丢弃组装器残留字节）</summary>
            public void SwapToEncrypted(INetworkMessageCodec encrypted)
            {
                Codec = encrypted;
                Assembler.ReplaceCodec(encrypted);
                Secured = true;
            }
        }
    }
}
