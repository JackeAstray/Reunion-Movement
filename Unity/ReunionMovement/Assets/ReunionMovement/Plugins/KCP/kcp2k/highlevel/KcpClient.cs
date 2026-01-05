// kcp 客户端逻辑封装成类。
// 供 Mirror、DOTSNET、测试等使用。
using System;
using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpClient : KcpPeer
    {
        // IO
        protected Socket socket;
        public EndPoint remoteEndPoint;

        // 对外暴露本地 endpoint，便于用户 / 中继 / NAT 遍历等使用
        public EndPoint LocalEndPoint => socket?.LocalEndPoint;

        // 配置
        protected readonly KcpConfig config;

        // 原始接收缓冲区始终需要为 MTU 大小，即使 MaxMessageSize 更大。
        // kcp 总是以 MTU 分片发送，缓冲区小于 MTU 会静默丢弃多余数据。
        // => 我们需要 MTU 能放下 channel + message！
        // => 受保护是因为可能有人重写 RawReceive 仍想复用缓冲区。
        protected readonly byte[] rawReceiveBuffer;

        // 回调
        // 即使发生错误也会回调，以便库显示对话框等。
        // 而不是直接记录日志。
        // （使用 string 而不是 Exception 以便易用并避免用户恐慌）
        //
        // 事件为只读，在构造函数中设置。
        // 这可以确保在使用时它们已经初始化。
        // 解决了 https://github.com/MirrorNetworking/Mirror/issues/3337 等问题
        protected readonly Action OnConnectedCallback;
        protected readonly Action<ArraySegment<byte>, KcpChannel> OnDataCallback;
        protected readonly Action OnDisconnectedCallback;
        protected readonly Action<ErrorCode, string> OnErrorCallback;

        // 状态
        bool active = false; // 在 connect() 和 disconnect() 之间为 active
        public bool connected;

        public KcpClient(Action OnConnected,
                         Action<ArraySegment<byte>, KcpChannel> OnData,
                         Action OnDisconnected,
                         Action<ErrorCode, string> OnError,
                         KcpConfig config)
                         : base(config, 0) // 客户端初始没有 cookie
        {
            // 先初始化回调以确保可以被安全使用。
            OnConnectedCallback = OnConnected;
            OnDataCallback = OnData;
            OnDisconnectedCallback = OnDisconnected;
            OnErrorCallback = OnError;
            this.config = config;

            // 创建 MTU 大小的接收缓冲
            rawReceiveBuffer = new byte[config.Mtu];
        }

        // 回调 ///////////////////////////////////////////////////////////
        // 某些回调需要包裹额外逻辑
        protected override void OnAuthenticated()
        {
            Log.Info($"[KCP] 客户端: OnConnected");
            connected = true;
            OnConnectedCallback();
        }

        protected override void OnData(ArraySegment<byte> message, KcpChannel channel) =>
            OnDataCallback(message, channel);

        protected override void OnError(ErrorCode error, string message) =>
            OnErrorCallback(error, message);

        protected override void OnDisconnected()
        {
            Log.Info($"[KCP] 客户端: OnDisconnected");
            connected = false;
            socket?.Close();
            socket = null;
            remoteEndPoint = null;
            OnDisconnectedCallback();
            active = false;
        }

        ////////////////////////////////////////////////////////////////////////
        public void Connect(string address, ushort port)
        {
            if (connected)
            {
                Log.Warning("[KCP] 客户端: 已连接！");
                return;
            }

            // 连接之前解析主机名。
            // 修复: https://github.com/MirrorNetworking/Mirror/issues/3361
            if (!Common.ResolveHostname(address, out IPAddress[] addresses))
            {
                // 将错误传递给用户回调。无需再次记录日志。
                OnError(ErrorCode.DnsResolve, $"无法解析主机: {address}");
                OnDisconnectedCallback();
                return;
            }

            // 为每次新会话创建新的 peer
            // 客户端不需要安全 cookie。
            Reset(config);

            Log.Info($"[KCP] 客户端: 连接到 {address}:{port}");

            // 创建 socket
            remoteEndPoint = new IPEndPoint(addresses[0], port);
            socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            active = true;

            // recv & send 在主线程调用。
            // 需要确保它们永远不会阻塞。
            // 即使每个连接阻塞 1ms 也会影响扩展性。
            socket.Blocking = false;

            // 配置缓冲区大小
            Common.ConfigureSocketBuffers(socket, config.RecvBufferSize, config.SendBufferSize);

            // 绑定到 endpoint，这样我们可以使用 send/recv 而不是 sendto/recvfrom。
            socket.Connect(remoteEndPoint);

            // 立即向服务器发送 hello 消息。
            // 服务器会调用 OnMessage 并将新连接加入。
            // 注意：这时 cookie=0，直到收到服务器的 hello。
            SendHello();
        }

        // IO - 输入
        // virtual 以便中继等可覆盖。
        // 在它返回 true 时循环调用以处理本 tick 的所有消息。
        // 返回的 ArraySegment 在下一次 RawReceive 调用之前有效。
        protected virtual bool RawReceive(out ArraySegment<byte> segment)
        {
            segment = default;
            if (socket == null) return false;

            try
            {
                return socket.ReceiveNonBlocking(rawReceiveBuffer, out segment);
            }
            // 对于非阻塞套接字，如果没有消息，Receive 会抛出 WouldBlock。这是可以接受的。
            // 仅为其它错误记录日志。
            catch (SocketException e)
            {
                // 对端关闭连接并不总是“错误”。
                // 但连接不应静默结束。至少记录一条日志以便调试。
                // 例如，当连接时没有服务器则会发生。
                Log.Info($"[KCP] Client.RawReceive: 看起来对端已关闭连接。这是正常的: {e}");
                base.Disconnect();
                return false;
            }
        }

        // IO - 输出
        // virtual 以便中继等可覆盖。
        protected override void RawSend(ArraySegment<byte> data)
        {
            // 仅当 socket 已连接/创建时才发送。
            // 用户可能在未连接时调用发送函数，导致 NRE。
            if (socket == null) return;

            try
            {
                socket.SendNonBlocking(data);
            }
            catch (SocketException e)
            {
                Log.Info($"[KCP] Client.RawSend: 看起来对端已关闭连接。这是正常的: {e}");
                // base.Disconnect(); <- 不要调用，若 SendDisconnect() 已抛出会导致死锁

            }
        }

        public void Send(ArraySegment<byte> segment, KcpChannel channel)
        {
            if (!connected)
            {
                Log.Warning("[KCP] 客户端: 未连接，无法发送！");
                return;
            }

            SendData(segment, channel);
        }

        // 插入原始 IO。通常来自 socket.Receive。
        // offset 对于中继很有用，我们可能解析头部然后把其余数据喂给 kcp。
        public void RawInput(ArraySegment<byte> segment)
        {
            // 确保有效大小：至少 1 字节通道 + 4 字节 cookie
            if (segment.Count <= 5) return;

            // 解析通道
            // byte channel = segment[0]; ArraySegment[i] 在旧版 Unity Mono 中不受支持
            byte channel = segment.Array[segment.Offset + 0];

            // 服务器消息总是包含安全 cookie。
            // 解析之，如果未分配则赋值，如果突然不同则警告。
            Utils.Decode32U(segment.Array, segment.Offset + 1, out uint messageCookie);
            if (messageCookie == 0)
            {
                Log.Error($"[KCP] 客户端: 收到 cookie=0 的消息，这不应该发生。服务器应始终包含安全 cookie。");
            }

            if (cookie == 0)
            {
                cookie = messageCookie;
                Log.Info($"[KCP] 客户端: 收到初始 cookie: {cookie}");
            }
            else if (cookie != messageCookie)
            {
                Log.Warning($"[KCP] 客户端: 丢弃 cookie 不匹配的消息: {messageCookie} 期望: {cookie}。");
                return;
            }

            // 解析消息
            ArraySegment<byte> message = new ArraySegment<byte>(segment.Array, segment.Offset + 1+4, segment.Count - 1-4);

            switch (channel)
            {
                case (byte)KcpChannel.Reliable:
                {
                    OnRawInputReliable(message);
                    break;
                }
                case (byte)KcpChannel.Unreliable:
                {
                    OnRawInputUnreliable(message);
                    break;
                }
                default:
                {
                    // 无效通道通常表示随机互联网噪声。
                    // 服务器可能收到随机 UDP 数据。
                    // 忽略，但记录以便调试。
                    Log.Warning($"[KCP] 客户端: 无效通道头: {channel}，可能是互联网噪声");
                    break;
                }
            }
        }

        // 处理入站，应在更新世界前调用。
        // virtual 因为中继可能需要注入自己的 ping 等。
        public override void TickIncoming()
        {
            // 先从 socket 接收，然后处理入站
            // （即使我们没收到任何东西，也需要 tick ping 等）
            // （active 为 null 表示未激活）
            if (active)
            {
                while (RawReceive(out ArraySegment<byte> segment))
                    RawInput(segment);
            }

            // RawReceive 可能已断开对端。再次检查 active。
            if (active) base.TickIncoming();
        }

        // 处理出站，应在更新世界后调用。
        // virtual 因为中继可能需要注入自己的 ping 等。
        public override void TickOutgoing()
        {
            // 在 active 时处理出站
            if (active) base.TickOutgoing();
        }

        // 处理入站和出站以便使用
        // => 理想情况下在更新世界前调用 ProcessIncoming()，在更新世界后调用 ProcessOutgoing() 以降低延迟
        public virtual void Tick()
        {
            TickIncoming();
            TickOutgoing();
        }
    }
}
