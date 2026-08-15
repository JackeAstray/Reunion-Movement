using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 原始 TCP 客户端通道 —— 无内建帧协议，直接暴露 TCP 字节流。
    /// 这是"对接任意服务器"的关键：配合 NetworkStreamAssembler + 长度前缀编解码器（或透传模式），
    /// 可与任何自定义协议的 TCP 服务器通信（长度前缀/行分隔/私有二进制协议均可）。
    /// 连接为异步（不阻塞主线程）；接收在后台线程，事件统一在 TickRefresh（主线程）派发。
    /// 注意：消息边界由上层编解码器决定 —— 请为 RawTcp 选择 SupportsStreamFraming = true 的编解码器
    /// （如 LengthPrefixed），否则每个 64KB 读取块会被当作一帧。
    /// </summary>
    public sealed class RawTcpClientChannel : INetworkClientChannel
    {
        public const int DefaultReceiveChunkSize = 1 << 16; // 64KB

        /// <summary>发送超时（毫秒）：对端停止读取时防止主线程被 NetworkStream.Write 无限阻塞</summary>
        public const int DefaultSendTimeoutMs = 10000;

        /// <summary>事件队列上限：主线程停摆时后台接收仍在入队，超限丢弃最旧事件防内存无界增长</summary>
        private const int MaxPendingEvents = 1024;

        enum EventType { Connected, Data, Disconnected, Error }

        TcpClient socket;
        NetworkStream stream;
        Thread recvThread;
        volatile bool running;
        volatile bool connected;
        // 连接代次：同一实例重连时递增。旧接收线程醒来后据此判定自身已过期并直接退出，
        // 防止旧线程复用新 stream（双线程抢读/数据交错）并排入虚假的 Error/Disconnected 事件。
        int generation;
        readonly ConcurrentQueue<(EventType type, byte[] data, string message)> events = new ConcurrentQueue<(EventType type, byte[] data, string message)>();
        readonly object sendLock = new object();
        readonly int receiveChunkSize;

        /// <summary>有界入队：超限时丢弃最旧事件（并发下的近似判断即可，多丢少丢一条无碍）</summary>
        private void EnqueueEvent((EventType type, byte[] data, string message) ev)
        {
            if (events.Count >= MaxPendingEvents) events.TryDequeue(out _);
            events.Enqueue(ev);
        }

        public string ChannelName { get; set; }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public bool IsConnect => connected;

        public bool IsOpen => connected;

        public event Action OnConnected;
        public event Action<byte[]> OnDataReceived;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        public RawTcpClientChannel(string channelName, int receiveChunkSize = DefaultReceiveChunkSize)
        {
            ChannelName = channelName;
            this.receiveChunkSize = receiveChunkSize <= 0 ? DefaultReceiveChunkSize : receiveChunkSize;
        }

        public void Connect(string host, int port)
        {
            // 清理旧连接（同一实例重连场景）
            CloseSocket();
            while (events.TryDequeue(out _)) { }

            Host = host;
            Port = port;
            // 新代次必须先递增、再置 running=true：旧 recvThread 被 CloseSocket 的 Dispose 唤醒后，
            // 若恰落在两句之间且 running 已为 true 而代次未变，会对已释放的旧 stream 读取并向
            // 新连接注入伪造的 Error/Disconnected 事件（窄窗口但真实存在）
            int gen = Interlocked.Increment(ref generation);
            running = true;
            connected = false;
            try
            {
                socket = new TcpClient();
                socket.NoDelay = true;
                // 防止对端停止读取时发送缓冲填满导致主线程无限阻塞
                socket.SendTimeout = DefaultSendTimeoutMs;
                _ = ConnectAsync(socket, host, port, gen);
            }
            catch (Exception ex)
            {
                EnqueueEvent((EventType.Error, null, "TCP 连接失败: " + ex.Message));
                EnqueueEvent((EventType.Disconnected, null, null));
            }
        }

        async Task ConnectAsync(TcpClient client, string host, int port, int gen)
        {
            try
            {
                await client.ConnectAsync(host, port).ConfigureAwait(false);
                // 代次校验：等待期间已再次重连（gen 过期）或 socket 被替换时放弃本次连接
                if (!running || gen != generation || !ReferenceEquals(client, socket))
                {
                    TryClose(client);
                    return;
                }
                stream = client.GetStream();
                connected = true;
                EnqueueEvent((EventType.Connected, null, null));
                recvThread = new Thread(() => ReceiveLoop(stream, gen)) { IsBackground = true, Name = "RawTcpClient.Recv" };
                recvThread.Start();
            }
            catch (Exception ex)
            {
                if (running && gen == generation)
                {
                    EnqueueEvent((EventType.Error, null, "TCP 连接失败: " + ex.Message));
                    EnqueueEvent((EventType.Disconnected, null, null));
                }
            }
        }

        void ReceiveLoop(NetworkStream localStream, int gen)
        {
            var buffer = new byte[receiveChunkSize];
            try
            {
                // 关键：读局部 stream 引用（不被重连覆盖到新 stream）+ 代次校验。
                // 旧线程因 CloseSocket 的 Dispose 从 Read 唤醒后，若已发生重连（代次不匹配）
                // 立即退出，不会排入伪造的“接收异常/断开”事件。
                while (running && gen == generation)
                {
                    int n = localStream.Read(buffer, 0, buffer.Length);
                    if (n <= 0) break;
                    var copy = new byte[n];
                    Buffer.BlockCopy(buffer, 0, copy, 0, n);
                    EnqueueEvent((EventType.Data, copy, null));
                }
            }
            catch (Exception ex)
            {
                if (running && gen == generation)
                {
                    EnqueueEvent((EventType.Error, null, "接收异常: " + ex.Message));
                }
            }
            if (running && gen == generation)
            {
                connected = false;
                EnqueueEvent((EventType.Disconnected, null, null));
            }
        }

        public void TickRefresh()
        {
            int processed = 0;
            while (processed < 512 && events.TryDequeue(out var ev))
            {
                processed++;
                try
                {
                    switch (ev.type)
                    {
                        case EventType.Connected:
                            OnConnected?.Invoke();
                            break;
                        case EventType.Data:
                            OnDataReceived?.Invoke(ev.data);
                            break;
                        case EventType.Disconnected:
                            OnDisconnected?.Invoke();
                            break;
                        case EventType.Error:
                            OnError?.Invoke(ev.message);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // 订阅者异常隔离：与项目其他用户事件派发一致，不中断剩余事件
                    Log.Warning("[RawTcpClientChannel] {0} 事件订阅者异常（已隔离）: {1}", ev.type, ex.Message);
                }
            }
        }

        public bool SendMessage(byte[] data)
        {
            if (!connected || data == null || data.Length == 0) return false;
            lock (sendLock)
            {
                if (!connected || stream == null) return false;
                try
                {
                    stream.Write(data, 0, data.Length);
                    return true;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke("发送失败: " + ex.Message);
                    return false;
                }
            }
        }

        public void Close()
        {
            CloseSocket();
            OnConnected = null;
            OnDataReceived = null;
            OnDisconnected = null;
            OnError = null;
            while (events.TryDequeue(out _)) { }
        }

        void CloseSocket()
        {
            running = false;
            connected = false;
            try { stream?.Dispose(); } catch { }
            stream = null;
            TryClose(socket);
            socket = null;
        }

        static void TryClose(TcpClient client)
        {
            try { client?.Close(); } catch { }
        }
    }
}
