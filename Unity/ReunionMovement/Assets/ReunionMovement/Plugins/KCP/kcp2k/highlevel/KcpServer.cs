// kcp 服务端逻辑封装成类。
// 供 Mirror、DOTSNET、测试等使用。
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace kcp2k
{
    public class KcpServer
    {
        // 回调
        // 即使发生错误也会回调，以便库显示对话框等。
        // 而不是直接记录日志。
        // （使用 string 而不是 Exception 以便易用并避免用户恐慌）
        //
        // 事件为只读，在构造函数中设置。
        // 这可以确保在使用时它们已经初始化。
        // 解决了 https://github.com/MirrorNetworking/Mirror/issues/3337 等问题
        protected readonly Action<int, IPEndPoint> OnConnected; // connectionId, address
        protected readonly Action<int, ArraySegment<byte>, KcpChannel> OnData;
        protected readonly Action<int> OnDisconnected;
        protected readonly Action<int, ErrorCode, string> OnError;

        // 配置
        protected readonly KcpConfig config;

        // 状态
        protected Socket socket;
        EndPoint newClientEP;

        // 对外暴露本地 endpoint，便于用户 / 中继 / NAT 遍历等使用
        public EndPoint LocalEndPoint => socket?.LocalEndPoint;

        // 原始接收缓冲区始终需要为 MTU 大小，即使 MaxMessageSize 更大。
        // kcp 总是以 MTU 分片发送，缓冲区小于 MTU 会静默丢弃多余数据。
        // => 我们需要 mtu 能放下 channel + message！
        protected readonly byte[] rawReceiveBuffer;

        // connections <connectionId, connection>，connectionId 使用 EndPoint.GetHashCode
        public Dictionary<int, KcpServerConnection> connections =
            new Dictionary<int, KcpServerConnection>();

        public KcpServer(Action<int, IPEndPoint> OnConnected,
                         Action<int, ArraySegment<byte>, KcpChannel> OnData,
                         Action<int> OnDisconnected,
                         Action<int, ErrorCode, string> OnError,
                         KcpConfig config)
        {
            // 先初始化回调以确保可以被安全使用。
            this.OnConnected = OnConnected;
            this.OnData = OnData;
            this.OnDisconnected = OnDisconnected;
            this.OnError = OnError;
            this.config = config;

            // 创建 mtu 大小的接收缓冲
            rawReceiveBuffer = new byte[config.Mtu];

            // 创建 newClientEP，支持 IPv4 或 IPv6
            newClientEP = config.DualMode
                          ? new IPEndPoint(IPAddress.IPv6Any, 0)
                          : new IPEndPoint(IPAddress.Any, 0);
        }

        public virtual bool IsActive() => socket != null;

        static Socket CreateServerSocket(bool DualMode, ushort port)
        {
            if (DualMode)
            {
                // 在 "::" 上的 IPv6 Socket，启用 DualMode
                Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

                // 启用 DualMode 可能抛出异常，尝试设置否则记录但继续
                // 修复: https://github.com/MirrorNetworking/Mirror/issues/3358
                try
                {
                    socket.DualMode = true;
                }
                catch (NotSupportedException e)
                {
                    Log.Warning($"[KCP] 无法设置 Dual Mode，继续使用 IPv6，但未启用 DualMode。错误: {e}");
                }

                // 对于 Windows sockets，存在一个罕见的问题：当使用单个 server socket 服务多个客户端时，
                // 如果其中一个客户端被关闭，server socket 在发送/接收时可能抛出异常。
                //
                // 这实际上发生在我们的一位用户身上：
                // https://github.com/MirrorNetworking/Mirror/issues/3611
                //
                // 以下是深入的解释和解决方案：
                //
                // “你可能知道，如果主机收到一个发往当前未绑定的UDP端口的包，它可能会发回一个ICMP‘端口不可达’消息。
                // 是否这样做取决于防火墙、私有/公共设置等因素。然而，在本地主机上，它几乎总是会发回这个包。
                //
                // 现在，在Windows上（且仅在Windows上），默认情况下，收到ICMP端口不可达消息会关闭发送该消息的UDP套接字；
                // 因此，下次你尝试在该套接字上接收时，由于套接字已被操作系统关闭，将会抛出一个异常。
                //
                // 显然，这会在你当前的多客户端、单服务器套接字设置中引发问题，但幸运的是，有一个解决办法：
                //
                // 你需要使用不常使用的Winsock控制码SIO_UDP_CONNRESET，该控制码可以关闭自动关闭套接字的内置行为。
                //
                // 请注意，此ioctl代码仅在Windows（XP及更高版本）上受支持，在Linux上不受支持，因为它是由Winsock扩展提供的。
                // 当然，由于所描述的行为只是Windows上的默认行为，因此这一遗漏并不是一个重大损失。
                // 如果你正试图创建一个跨平台库，则应将其标记为特定于Windows的代码。”
                // 
                // https://stackoverflow.com/questions/74327225/why-does-sending-via-a-udpclient-cause-subsequent-receiving-to-fail
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const uint IOC_IN = 0x80000000U;
                    const uint IOC_VENDOR = 0x18000000U;
                    const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));
                    socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0x00 }, null);
                }

                socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                return socket;
            }
            else
            {
                // IPv4 socket @ "0.0.0.0":port
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return socket;
            }
        }

        public virtual void Start(ushort port)
        {
            // 仅允许启动一次
            if (socket != null)
            {
                Log.Warning("[KCP] 服务器: 已经启动！");
                return;
            }

            // 监听
            socket = CreateServerSocket(config.DualMode, port);

            // recv & send 在主线程调用。
            // 需要确保它们永远不会阻塞。
            // 即使每个连接阻塞 1ms 也会影响扩展性。
            socket.Blocking = false;

            // 配置缓冲区大小
            Common.ConfigureSocketBuffers(socket, config.RecvBufferSize, config.SendBufferSize);
        }

        public void Send(int connectionId, ArraySegment<byte> segment, KcpChannel channel)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.SendData(segment, channel);
            }
        }

        public void Disconnect(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.Disconnect();
            }
        }

        // 对外暴露完整 IPEndPoint，而不仅仅是 IP 地址。
        public IPEndPoint GetClientEndPoint(int connectionId)
        {
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                return connection.remoteEndPoint as IPEndPoint;
            }
            return null;
        }

        // IO - 输入
        // virtual 以便中继、无分配优化等可以覆盖。
        // https://github.com/vis2k/where-allocation
        // 返回 bool 因为并非所有接收都有效。
        protected virtual bool RawReceiveFrom(out ArraySegment<byte> segment, out int connectionId)
        {
            segment = default;
            connectionId = 0;
            if (socket == null) return false;

            try
            {
                if (socket.ReceiveFromNonBlocking(rawReceiveBuffer, out segment, ref newClientEP))
                {
                    // 使用 endpoint hash 作为 connectionId
                    connectionId = Common.ConnectionHash(newClientEP);
                    return true;
                }
            }
            catch (SocketException e)
            {
                // 注意：SocketException 不是 IOException 的子类。
                // 对端关闭连接并不总是“错误”。
                // 但连接不应静默结束。至少记录一条日志以便调试。
                Log.Info($"[KCP] 服务器: ReceiveFrom 失败: {e}");
            }

            return false;
        }

        // IO - 输出
        // virtual 以便中继、无分配优化等可以覆盖。
        protected virtual void RawSend(int connectionId, ArraySegment<byte> data)
        {
            if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                Log.Warning($"[KCP] 服务器: RawSend 无效的 connectionId={connectionId}");
                return;
            }

            try
            {
                socket.SendToNonBlocking(data, connection.remoteEndPoint);
            }
            catch (SocketException e)
            {
                Log.Error($"[KCP] 服务器: SendTo 失败: {e}");
            }
        }

        protected virtual KcpServerConnection CreateConnection(int connectionId)
        {
            // 为该连接生成随机 cookie 以防 UDP 欺骗。
            // 需要随机且无分配以避免 GC。
            uint cookie = Common.GenerateCookie();

            // 首先创建空连接对象而不绑定 peer。
            // 需要它来设置 peer 回调。
            // 之后再分配 peer。
            // 事件需要封装 connectionId。
            KcpServerConnection connection = new KcpServerConnection(
                OnConnectedCallback,
                (message, channel) => OnData(connectionId, message, channel),
                OnDisconnectedCallback,
                (error, reason) => OnError(connectionId, error, reason),
                (data) => RawSend(connectionId, data),
                config,
                cookie,
                newClientEP);

            return connection;

            // 设置认证完成后添加到 connections 的回调
            void OnConnectedCallback(KcpServerConnection conn)
            {
                // 在认证后再加入 connections 字典。
                connections.Add(connectionId, conn);
                Log.Info($"[KCP] 服务器: 添加连接({connectionId})");

                // 在握手完成后再设置 Data + Disconnected 事件

                Log.Info($"[KCP] 服务器: OnConnected({connectionId})");
                IPEndPoint endPoint = conn.remoteEndPoint as IPEndPoint;
                OnConnected(connectionId, endPoint);
            }

            void OnDisconnectedCallback()
            {
                // 标记为移除
                connectionsToRemove.Add(connectionId);

                // 调用外部断开事件
                Log.Info($"[KCP] 服务器: OnDisconnected({connectionId})");
                OnDisconnected(connectionId);
            }
        }

        // 接收 + 添加 + 处理 一次性完成。
        // 如果有更多数据要接收，最好循环调用此函数。
        void ProcessMessage(ArraySegment<byte> segment, int connectionId)
        {
            //Log.Info($"[KCP] server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

            // 是否为新连接？
            if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                // 基于最后接收到的 EndPoint 创建新的 KcpConnection
                connection = CreateConnection(connectionId);

                // DO NOT add to connections yet. only if the first message
                // is actually the kcp handshake. otherwise it's either:
                // * random data from the internet
                // * or from a client connection that we just disconnected
                //   but that hasn't realized it yet, still sending data
                //   from last session that we should absolutely ignore.
                //
                //
                // TODO this allocates a new KcpConnection for each new
                // internet connection. not ideal, but C# UDP Receive
                // already allocated anyway.
                //
                // expecting a MAGIC byte[] would work, but sending the raw
                // UDP message without kcp's reliability will have low
                // probability of being received.
                // for now, this is fine.
                //
                // TODO 这会为每个新互联网连接分配新的 KcpConnection。
                // 虽然 C# UDP Receive 本身就会分配，但不够理想。
                // 期待 MAGIC byte[] 的方法效果不佳，且去掉 kcp 的可靠性会降低接收概率。
                // 暂时这样处理。
                connection.RawInput(segment);
                connection.TickIncoming();
            }
            // 对于已有连接：直接将消息输入 kcp
            else
            {
                connection.RawInput(segment);
            }
        }

        // 处理入站。应在更新世界前调用。
        // virtual 因为中继可能需要注入自己的 ping 或类似逻辑。
        readonly HashSet<int> connectionsToRemove = new HashSet<int>();
        public virtual void TickIncoming()
        {
            // 将所有接收到的消息传入 kcp
            while (RawReceiveFrom(out ArraySegment<byte> segment, out int connectionId))
            {
                ProcessMessage(segment, connectionId);
            }

            // 处理所有 server connection 的输入
            // (即使没有接收到数据。也需要处理 ping 等心跳)
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.TickIncoming();
            }

            // 移除断开的连接
            // (不能在 connection.OnDisconnected 中处理，因为 Tick 可能在遍历连接时调用)
            foreach (int connectionId in connectionsToRemove)
            {
                connections.Remove(connectionId);
            }
            connectionsToRemove.Clear();
        }

        // 处理出站。应在更新世界后调用。
        // virtual 因为中继可能需要注入自己的 ping 或类似逻辑。
        public virtual void TickOutgoing()
        {
            // 刷新所有 server connections
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.TickOutgoing();
            }
        }

        // 处理入站与出站的便捷方法。
        // => 理想状态下应在更新世界前调用 ProcessIncoming() ，后调用 ProcessOutgoing() 以降低延迟
        public virtual void Tick()
        {
            TickIncoming();
            TickOutgoing();
        }

        public virtual void Stop()
        {
            // fixes https://github.com/vis2k/kcp2k/pull/47
            // 需要清理 connections，否则下次会话中仍有残留
            connections.Clear();
            socket?.Close();
            socket = null;
        }
    }
}
