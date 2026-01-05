// Kcp Peer，类似于 UDP Peer，但封装了可靠性、通道、
// 超时、认证、状态等功能。
//
// 仍然与 IO 无关，以便兼容 udp、无分配、中继、原生等。
using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace kcp2k
{
    public abstract class KcpPeer
    {
        // kcp 可靠性算法
        internal Kcp kcp;

        // 安全 cookie 用以防止 UDP 欺骗。
        // 感谢 IncludeSec 发现该问题。
        //
        // 服务端将期望的 cookie 传给客户端的 KcpPeer。
        // KcpPeer 将 cookie 发送给连接的客户端。
        // KcpPeer 仅接受包含 cookie 的数据包。
        // => cookie 应该是加密安全的随机数，无法轻易预测。
        // => cookie 可以是 hash(ip, port)，但必须加盐以避免可预测性。
        internal uint cookie;

        // 状态：一创建 peer 即视为已连接。
        // KcpConnection 遗留字段，在以后重构可移除。
        protected KcpState state = KcpState.Connected;

        // 如果在以下毫秒数内都未收到任何数据，则视为断开
        public const int DEFAULT_TIMEOUT = 10000;
        public int timeout;
        uint lastReceiveTime;

        // 内部时间。
        // Stopwatch 提供 ElapsedMilliseconds，在长时间运行中应该比 Unity 的 time.deltaTime 更精确。
        readonly Stopwatch watch = new Stopwatch();

        // 接收 kcp 处理后消息的缓冲（避免分配）。
        // 重要：这是用于 KCP 消息的缓冲，大小为：1 字节头 + ReliableMaxMessageSize
        readonly byte[] kcpMessageBuffer;// = new byte[1 + ReliableMaxMessageSize];

        // 发送缓冲，用于把用户消息交给 kcp 处理（避免分配）。
        // 重要：大小为：1 字节头 + ReliableMaxMessageSize
        readonly byte[] kcpSendBuffer;// = new byte[1 + ReliableMaxMessageSize];

        // 原始发送缓冲正好为 MTU 大小。
        readonly byte[] rawSendBuffer;

        // 偶尔发送 ping 以避免对端超时。
        public const int PING_INTERVAL = 1000;
        uint lastPingTime;

        // 如果我们发送的消息超过 kcp 能处理的能力，发送/接收缓冲和队列会增长，延迟也会变得非常高。
        // => 若连接无法跟上，应断开以保护服务器在高负载下的稳定性。
        internal const int QueueDisconnectThreshold = 10000;

        // 用于调试信息的队列与缓冲计数 getter
        public int SendQueueCount     => kcp.snd_queue.Count;
        public int ReceiveQueueCount  => kcp.rcv_queue.Count;
        public int SendBufferCount    => kcp.snd_buf.Count;
        public int ReceiveBufferCount => kcp.rcv_buf.Count;

        // 我们需要从每个 MaxMessageSize 计算中减去通道和 cookie 字节。
        // 同时需要告诉 kcp 使用 MTU-1 来为该字节留出空间。
        public const int CHANNEL_HEADER_SIZE = 1;
        public const int COOKIE_HEADER_SIZE = 4;
        public const int METADATA_SIZE = CHANNEL_HEADER_SIZE + COOKIE_HEADER_SIZE;

        // 可靠通道（即 kcp）MaxMessageSize，供外部知晓最大可发送长度。
        static int ReliableMaxMessageSize_Unconstrained(int mtu, uint rcv_wnd) =>
            (mtu - Kcp.OVERHEAD - METADATA_SIZE) * ((int)rcv_wnd - 1) - 1;

        public static int ReliableMaxMessageSize(int mtu, uint rcv_wnd) =>
            ReliableMaxMessageSize_Unconstrained(mtu, Math.Min(rcv_wnd, Kcp.FRG_MAX));

        // 不可靠最大消息大小为 MTU - 通道头 - kcp 头
        public static int UnreliableMaxMessageSize(int mtu) =>
            mtu - METADATA_SIZE - 1;

        // 每秒最大发送速率可由 kcp 参数计算
        public uint MaxSendRate    => kcp.snd_wnd * kcp.mtu * 1000 / kcp.interval;
        public uint MaxReceiveRate => kcp.rcv_wnd * kcp.mtu * 1000 / kcp.interval;

        // 基于 mtu 和 wnd 计算一次最大消息大小
        public readonly int unreliableMax;
        public readonly int reliableMax;

        // 创建并配置新的 KCP 实例。
        protected KcpPeer(KcpConfig config, uint cookie)
        {
            Reset(config);
            this.cookie = cookie;
            Log.Info($"[KCP] {GetType()}: 已创建 cookie={cookie}");

            rawSendBuffer = new byte[config.Mtu];

            unreliableMax = UnreliableMaxMessageSize(config.Mtu);
            reliableMax = ReliableMaxMessageSize(config.Mtu, config.ReceiveWindowSize);

            kcpMessageBuffer = new byte[1 + reliableMax];
            kcpSendBuffer    = new byte[1 + reliableMax];
        }

        // 重置所有状态。
        protected void Reset(KcpConfig config)
        {
            cookie = 0;
            state = KcpState.Connected;
            lastReceiveTime = 0;
            lastPingTime = 0;
            watch.Restart();

            kcp = new Kcp(0, RawSendReliable);

            kcp.SetNoDelay(config.NoDelay ? 1u : 0u, config.Interval, config.FastResend, !config.CongestionWindow);
            kcp.SetWindowSize(config.SendWindowSize, config.ReceiveWindowSize);

            // 告诉 kcp 使用 MTU - METADATA_SIZE
            kcp.SetMtu((uint)config.Mtu - METADATA_SIZE);

            kcp.dead_link = config.MaxRetransmits;
            timeout = config.Timeout;
        }

        // 回调 ///////////////////////////////////////////////////////////
        protected abstract void OnAuthenticated();
        protected abstract void OnData(ArraySegment<byte> message, KcpChannel channel);
        protected abstract void OnDisconnected();
        protected abstract void OnError(ErrorCode error, string message);
        protected abstract void RawSend(ArraySegment<byte> data);

        ////////////////////////////////////////////////////////////////////////

        void HandleTimeout(uint time)
        {
            if (time >= lastReceiveTime + timeout)
            {
                OnError(ErrorCode.Timeout, $"{GetType()}: 超时 {timeout}ms，未收到任何消息。断开连接。");
                Disconnect();
            }
        }

        void HandleDeadLink()
        {
            if (kcp.state == -1)
            {
                OnError(ErrorCode.Timeout, $"{GetType()}: dead_link 检测到消息被重传 {kcp.dead_link} 次仍未确认。断开连接。");
                Disconnect();
            }
        }

        void HandlePing(uint time)
        {
            if (time >= lastPingTime + PING_INTERVAL)
            {
                SendPing();
                lastPingTime = time;
            }
        }

        void HandleChoked()
        {
            int total = kcp.rcv_queue.Count + kcp.snd_queue.Count +
                        kcp.rcv_buf.Count   + kcp.snd_buf.Count;
            if (total >= QueueDisconnectThreshold)
            {
                OnError(ErrorCode.Congestion,
                        $"{GetType()}: 连接无法处理数据，正在断开。\n" +
                        $"队列总数 {total}>{QueueDisconnectThreshold}. rcv_queue={kcp.rcv_queue.Count} snd_queue={kcp.snd_queue.Count} rcv_buf={kcp.rcv_buf.Count} snd_buf={kcp.snd_buf.Count}\n" +
                        $"* 尝试启用 NoDelay，减小 INTERVAL，禁用拥塞窗口（启用 NOCWND!），增大发送/接收窗口或压缩数据。\n" +
                        $"* 或者可能是我们的网络或对端网络太慢。");

                kcp.snd_queue.Clear();

                Disconnect();
            }
        }

        bool ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message)
        {
            message = default;
            header = KcpHeaderReliable.Ping;

            int msgSize = kcp.PeekSize();
            if (msgSize <= 0) return false;

            if (msgSize > kcpMessageBuffer.Length)
            {
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: 可能的分配攻击，msgSize {msgSize} > 缓冲 {kcpMessageBuffer.Length}。断开连接。");
                Disconnect();
                return false;
            }

            int received = kcp.Receive(kcpMessageBuffer, msgSize);
            if (received < 0)
            {
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive 失败，error={received}。关闭连接。");
                Disconnect();
                return false;
            }

            byte headerByte = kcpMessageBuffer[0];
            if (!KcpHeader.ParseReliable(headerByte, out header))
            {
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: 解析头失败: {headerByte} 未定义于 {typeof(KcpHeaderReliable)}。\n");
                Disconnect();
                return false;
            }

            message = new ArraySegment<byte>(kcpMessageBuffer, 1, msgSize - 1);
            lastReceiveTime = (uint)watch.ElapsedMilliseconds;
            return true;
        }

        void TickIncoming_Connected(uint time)
        {
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            if (ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message))
            {
                switch (header)
                {
                    case KcpHeaderReliable.Hello:
                    {
                        Log.Info($"[KCP] {GetType()}: 收到带 cookie={cookie} 的 hello");
                        state = KcpState.Authenticated;
                        OnAuthenticated();
                        break;
                    }
                    case KcpHeaderReliable.Ping:
                    {
                        break;
                    }
                    case KcpHeaderReliable.Data:
                    {
                        OnError(ErrorCode.InvalidReceive, $"[KCP] {GetType()}: 在 Connected 状态收到无效头 {header}。断开连接。");
                        Disconnect();
                        break;
                    }
                }
            }
        }

        void TickIncoming_Authenticated(uint time)
        {
            HandleTimeout(time);
            HandleDeadLink();
            HandlePing(time);
            HandleChoked();

            while (ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message))
            {
                switch (header)
                {
                    case KcpHeaderReliable.Hello:
                    {
                        Log.Warning($"{GetType()}: 在 Authenticated 状态收到无效头 {header}。断开连接。");
                        Disconnect();
                        break;
                    }
                    case KcpHeaderReliable.Data:
                    {
                        if (message.Count > 0)
                        {
                            OnData(message, KcpChannel.Reliable);
                        }
                        else
                        {
                            OnError(ErrorCode.InvalidReceive, $"{GetType()}: 在 Authenticated 状态收到空的 Data 消息。断开连接。");
                            Disconnect();
                        }
                        break;
                    }
                    case KcpHeaderReliable.Ping:
                    {
                        break;
                    }
                }
            }
        }

        public virtual void TickIncoming()
        {
            uint time = (uint)watch.ElapsedMilliseconds;

            try
            {
                switch (state)
                {
                    case KcpState.Connected:
                    {
                        TickIncoming_Connected(time);
                        break;
                    }
                    case KcpState.Authenticated:
                    {
                        TickIncoming_Authenticated(time);
                        break;
                    }
                    case KcpState.Disconnected:
                    {
                        break;
                    }
                }
            }
            catch (SocketException exception)
            {
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: 断开连接因为 {exception}。这是正常的。");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: 断开连接因为 {exception}。这是正常的。");
                Disconnect();
            }
            catch (Exception exception)
            {
                OnError(ErrorCode.Unexpected, $"{GetType()}: 未预期的异常: {exception}");
                Disconnect();
            }
        }

        public virtual void TickOutgoing()
        {
            uint time = (uint)watch.ElapsedMilliseconds;

            try
            {
                switch (state)
                {
                    case KcpState.Connected:
                    case KcpState.Authenticated:
                    {
                        kcp.Update(time);
                        break;
                    }
                    case KcpState.Disconnected:
                    {
                        break;
                    }
                }
            }
            catch (SocketException exception)
            {
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: 断开连接因为 {exception}。这是正常的。");
                Disconnect();
            }
            catch (ObjectDisposedException exception)
            {
                OnError(ErrorCode.ConnectionClosed, $"{GetType()}: 断开连接因为 {exception}。这是正常的。");
                Disconnect();
            }
            catch (Exception exception)
            {
                OnError(ErrorCode.Unexpected, $"{GetType()}: 未预期的异常: {exception}");
                Disconnect();
            }
        }

        protected void OnRawInputReliable(ArraySegment<byte> message)
        {
            int input = kcp.Input(message.Array, message.Offset, message.Count);
            if (input != 0)
            {
                Log.Warning($"[KCP] {GetType()}: Input 失败，error={input}，缓冲长度={message.Count - 1}");
            }
        }

        protected void OnRawInputUnreliable(ArraySegment<byte> message)
        {
            if (message.Count < 1) return;

            byte headerByte = message.Array[message.Offset + 0];
            if (!KcpHeader.ParseUnreliable(headerByte, out KcpHeaderUnreliable header))
            {
                OnError(ErrorCode.InvalidReceive, $"{GetType()}: 解析头失败: {headerByte} 未定义于 {typeof(KcpHeaderUnreliable)}。\n");
                Disconnect();
                return;
            }

            message = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);

            switch (header)
            {
                case KcpHeaderUnreliable.Data:
                {
                    if (state == KcpState.Authenticated)
                    {
                        OnData(message, KcpChannel.Unreliable);
                        lastReceiveTime = (uint)watch.ElapsedMilliseconds;
                    }
                    else
                    {
                        // 在未认证前收到不可靠消息很常见，忽略即可。
                    }
                    break;
                }
                case KcpHeaderUnreliable.Disconnect:
                {
                    Log.Info($"[KCP] {GetType()}: 收到断开消息");
                    Disconnect();
                    break;
                }
            }
        }

        void RawSendReliable(byte[] data, int length)
        {
            rawSendBuffer[0] = (byte)KcpChannel.Reliable;
            Utils.Encode32U(rawSendBuffer, 1, cookie);
            Buffer.BlockCopy(data, 0, rawSendBuffer, 1+4, length);
            ArraySegment<byte> segment = new ArraySegment<byte>(rawSendBuffer, 0, length + 1+4);
            RawSend(segment);
        }

        void SendReliable(KcpHeaderReliable header, ArraySegment<byte> content)
        {
            if (1 + content.Count > kcpSendBuffer.Length)
            {
                OnError(ErrorCode.InvalidSend, $"{GetType()}: 发送可靠消息失败，大小 {content.Count} > ReliableMaxMessageSize={reliableMax}");
                return;
            }

            kcpSendBuffer[0] = (byte)header;

            if (content.Count > 0)
                Buffer.BlockCopy(content.Array, content.Offset, kcpSendBuffer, 1, content.Count);

            int sent = kcp.Send(kcpSendBuffer, 0, 1 + content.Count);
            if (sent < 0)
            {
                OnError(ErrorCode.InvalidSend, $"{GetType()}: Send 失败，error={sent}，内容长度={content.Count}");
            }
        }

        void SendUnreliable(KcpHeaderUnreliable header, ArraySegment<byte> content)
        {
            if (content.Count > unreliableMax)
            {
                Log.Error($"[KCP] {GetType()}: 发送不可靠消息失败，大小 {content.Count} > UnreliableMaxMessageSize={unreliableMax}");
                return;
            }

            rawSendBuffer[0] = (byte)KcpChannel.Unreliable;
            Utils.Encode32U(rawSendBuffer, 1, cookie);
            rawSendBuffer[5] = (byte)header;

            if (content.Count > 0)
                Buffer.BlockCopy(content.Array, content.Offset, rawSendBuffer, 1 + 4 + 1, content.Count);

            ArraySegment<byte> segment = new ArraySegment<byte>(rawSendBuffer, 0, content.Count + 1 + 4 + 1);
            RawSend(segment);
        }

        public void SendHello()
        {
            Log.Info($"[KCP] {GetType()}: 向对端发送握手，cookie={cookie}");
            SendReliable(KcpHeaderReliable.Hello, default);
        }

        public void SendData(ArraySegment<byte> data, KcpChannel channel)
        {
            // 发送空段是不允许的。
            if (data.Count == 0)
            {
                OnError(ErrorCode.InvalidSend, $"{GetType()}: 试图发送空消息，这不应该发生。断开连接。");
                Disconnect();
                return;
            }

            switch (channel)
            {
                case KcpChannel.Reliable:
                    SendReliable(KcpHeaderReliable.Data, data);
                    break;
                case KcpChannel.Unreliable:
                    SendUnreliable(KcpHeaderUnreliable.Data, data);
                    break;
            }
        }

        void SendPing() => SendReliable(KcpHeaderReliable.Ping, default);

        void SendDisconnect()
        {
            for (int i = 0; i < 5; ++i)
                SendUnreliable(KcpHeaderUnreliable.Disconnect, default);
        }

        public virtual void Disconnect()
        {
            if (state == KcpState.Disconnected)
                return;

            try
            {
                SendDisconnect();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            Log.Info($"[KCP] {GetType()}: 已断开连接。");
            state = KcpState.Disconnected;
            OnDisconnected();
        }
    }
}
