using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 原始 TCP 服务端通道 —— 接受任意 TCP 客户端，按字节流收发（无内建帧协议）。
    /// 配合 NetworkStreamAssembler + 编解码器，可对接任意协议的客户端（原生 socket/第三方程序均可）。
    /// 接受与接收均在后台线程，事件统一在 TickRefresh（主线程）派发。
    /// 连接 ID 从 1 开始单调递增，断开后不复用。
    /// </summary>
    public sealed class RawTcpServerChannel : INetworkServerChannel
    {
        public const int DefaultReceiveChunkSize = 1 << 16; // 64KB

        enum EventType { Connected, Error }

        TcpListener listener;
        Thread acceptThread;
        volatile bool running;
        readonly ConcurrentQueue<(EventType type, int connectionId, object connection, string message)> events = new ConcurrentQueue<(EventType type, int connectionId, object connection, string message)>();
        readonly Dictionary<int, RawTcpServerConnection> connections = new Dictionary<int, RawTcpServerConnection>();
        readonly int receiveChunkSize;
        int nextConnectionId = 1;

        public string ChannelName { get; set; }

        public int Port { get; private set; }

        public string Host => string.Empty; // 监听所有网卡

        public bool Active => running;

        public bool IsOpen => running;

        public event Action<int, string> OnConnected;
        public event Action<int, byte[]> OnDataReceived;
        public event Action<int> OnDisconnected;
        public event Action<int, string> OnError;

        public RawTcpServerChannel(string channelName, int port, int receiveChunkSize = DefaultReceiveChunkSize)
        {
            ChannelName = channelName;
            Port = port;
            this.receiveChunkSize = receiveChunkSize <= 0 ? DefaultReceiveChunkSize : receiveChunkSize;
        }

        public bool Start()
        {
            if (running) return false;
            try
            {
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();
                running = true;
                acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "RawTcpServer.Accept" };
                acceptThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("RawTcp 服务端启动失败: {0}", ex.Message);
                OnError?.Invoke(-1, "RawTcp 服务端启动失败: " + ex.Message);
                return false;
            }
        }

        void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    if (!running)
                    {
                        try { client.Close(); } catch { }
                        return;
                    }
                    client.NoDelay = true;
                    int id = Interlocked.Increment(ref nextConnectionId);
                    var conn = new RawTcpServerConnection(id, client, receiveChunkSize);
                    string address = string.Empty;
                    try { address = client.Client.RemoteEndPoint?.ToString() ?? string.Empty; } catch { }
                    events.Enqueue((EventType.Connected, id, conn, address));
                }
                catch (Exception ex)
                {
                    if (!running) return;
                    if (ex is SocketException || ex is ObjectDisposedException)
                    {
                        if (!running) return;
                    }
                    events.Enqueue((EventType.Error, -1, null, "Accept 异常: " + ex.Message));
                }
            }
        }

        public void TickRefresh()
        {
            if (!running) return;
            int processed = 0;

            // 1) 接受线程事件（新连接接入主线程字典后才开始接收，保证事件顺序）
            while (processed < 1024 && events.TryDequeue(out var ev))
            {
                processed++;
                switch (ev.type)
                {
                    case EventType.Connected:
                    {
                        var conn = (RawTcpServerConnection)ev.connection;
                        if (connections.ContainsKey(ev.connectionId))
                        {
                            conn.Close();
                            break;
                        }
                        connections[ev.connectionId] = conn;
                        conn.StartReceive();
                        OnConnected?.Invoke(ev.connectionId, ev.message);
                        break;
                    }
                    case EventType.Error:
                        OnError?.Invoke(ev.connectionId, ev.message);
                        break;
                }
            }

            // 2) 各连接的接收队列
            var snapshot = new List<RawTcpServerConnection>(connections.Values);
            foreach (var conn in snapshot)
            {
                while (processed < 4096 && conn.TryDequeue(out var ev))
                {
                    processed++;
                    switch (ev.Type)
                    {
                        case RawTcpServerConnection.RecvEventType.Data:
                            OnDataReceived?.Invoke(conn.Id, ev.Data);
                            break;
                        case RawTcpServerConnection.RecvEventType.Error:
                            OnError?.Invoke(conn.Id, ev.Message);
                            break;
                        case RawTcpServerConnection.RecvEventType.Disconnected:
                            if (connections.Remove(conn.Id))
                            {
                                conn.Close();
                                OnDisconnected?.Invoke(conn.Id);
                            }
                            break;
                    }
                }
                // 发送失败等原因静默关闭的连接也需清理
                if (conn.IsClosed && connections.Remove(conn.Id))
                {
                    OnDisconnected?.Invoke(conn.Id);
                }
            }
        }

        public bool SendMessage(int connectionId, byte[] data)
        {
            if (connections.TryGetValue(connectionId, out var conn))
            {
                return conn.Send(data);
            }
            return false;
        }

        public bool Disconnect(int connectionId)
        {
            if (connections.Remove(connectionId, out var conn))
            {
                conn.Close();
                OnDisconnected?.Invoke(connectionId);
                return true;
            }
            return false;
        }

        public string GetConnectionAddress(int connectionId)
        {
            return connections.TryGetValue(connectionId, out var conn) ? conn.Address : string.Empty;
        }

        public void Close()
        {
            running = false;
            try { listener?.Stop(); } catch { }
            listener = null;
            var snapshot = new List<RawTcpServerConnection>(connections.Values);
            connections.Clear();
            foreach (var conn in snapshot)
            {
                conn.Close();
            }
            OnConnected = null;
            OnDataReceived = null;
            OnDisconnected = null;
            OnError = null;
            while (events.TryDequeue(out _)) { }
        }
    }

    /// <summary>RawTcp 服务端的单连接封装（后台接收线程 + 事件队列）</summary>
    internal sealed class RawTcpServerConnection
    {
        public enum RecvEventType { Data, Disconnected, Error }

        public struct RecvEvent
        {
            public RecvEventType Type;
            public byte[] Data;
            public string Message;
        }

        readonly TcpClient socket;
        readonly int chunkSize;
        readonly ConcurrentQueue<RecvEvent> events = new ConcurrentQueue<RecvEvent>();
        readonly object sendLock = new object();
        NetworkStream stream;
        Thread recvThread;
        volatile bool running;
        volatile bool closed;

        public int Id { get; }

        public string Address { get; }

        public bool IsClosed => closed;

        public RawTcpServerConnection(int id, TcpClient socket, int chunkSize)
        {
            Id = id;
            this.socket = socket;
            this.chunkSize = chunkSize;
            try { Address = socket.Client.RemoteEndPoint?.ToString() ?? string.Empty; } catch { Address = string.Empty; }
        }

        public void StartReceive()
        {
            try
            {
                stream = socket.GetStream();
            }
            catch
            {
                Close();
                return;
            }
            running = true;
            recvThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "RawTcpServer.Recv" + Id };
            recvThread.Start();
        }

        void ReceiveLoop()
        {
            var buffer = new byte[chunkSize];
            try
            {
                while (running)
                {
                    int n = stream.Read(buffer, 0, buffer.Length);
                    if (n <= 0) break;
                    var copy = new byte[n];
                    Buffer.BlockCopy(buffer, 0, copy, 0, n);
                    events.Enqueue(new RecvEvent { Type = RecvEventType.Data, Data = copy });
                }
            }
            catch (Exception ex)
            {
                if (running)
                {
                    events.Enqueue(new RecvEvent { Type = RecvEventType.Error, Message = "接收异常: " + ex.Message });
                }
            }
            if (running)
            {
                events.Enqueue(new RecvEvent { Type = RecvEventType.Disconnected });
            }
        }

        public bool TryDequeue(out RecvEvent ev)
        {
            return events.TryDequeue(out ev);
        }

        public bool Send(byte[] data)
        {
            lock (sendLock)
            {
                if (!running || closed || stream == null || data == null || data.Length == 0) return false;
                try
                {
                    stream.Write(data, 0, data.Length);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Warning("RawTcp 连接 {0} 发送失败: {1}", Id, ex.Message);
                    Close();
                    return false;
                }
            }
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            running = false;
            try { stream?.Dispose(); } catch { }
            try { socket?.Close(); } catch { }
        }
    }
}
