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

        enum EventType { Connected, Data, Disconnected, Error }

        TcpClient socket;
        NetworkStream stream;
        Thread recvThread;
        volatile bool running;
        volatile bool connected;
        readonly ConcurrentQueue<(EventType type, byte[] data, string message)> events = new ConcurrentQueue<(EventType type, byte[] data, string message)>();
        readonly object sendLock = new object();
        readonly int receiveChunkSize;

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
            running = true;
            connected = false;
            try
            {
                socket = new TcpClient();
                socket.NoDelay = true;
                _ = ConnectAsync(socket, host, port);
            }
            catch (Exception ex)
            {
                events.Enqueue((EventType.Error, null, "TCP 连接失败: " + ex.Message));
                events.Enqueue((EventType.Disconnected, null, null));
            }
        }

        async Task ConnectAsync(TcpClient client, string host, int port)
        {
            try
            {
                await client.ConnectAsync(host, port).ConfigureAwait(false);
                if (!running || !ReferenceEquals(client, socket))
                {
                    TryClose(client);
                    return;
                }
                stream = client.GetStream();
                connected = true;
                events.Enqueue((EventType.Connected, null, null));
                recvThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "RawTcpClient.Recv" };
                recvThread.Start();
            }
            catch (Exception ex)
            {
                events.Enqueue((EventType.Error, null, "TCP 连接失败: " + ex.Message));
                events.Enqueue((EventType.Disconnected, null, null));
            }
        }

        void ReceiveLoop()
        {
            var buffer = new byte[receiveChunkSize];
            try
            {
                while (running)
                {
                    int n = stream.Read(buffer, 0, buffer.Length);
                    if (n <= 0) break;
                    var copy = new byte[n];
                    Buffer.BlockCopy(buffer, 0, copy, 0, n);
                    events.Enqueue((EventType.Data, copy, null));
                }
            }
            catch (Exception ex)
            {
                if (running)
                {
                    events.Enqueue((EventType.Error, null, "接收异常: " + ex.Message));
                }
            }
            if (running)
            {
                connected = false;
                events.Enqueue((EventType.Disconnected, null, null));
            }
        }

        public void TickRefresh()
        {
            int processed = 0;
            while (processed < 512 && events.TryDequeue(out var ev))
            {
                processed++;
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
