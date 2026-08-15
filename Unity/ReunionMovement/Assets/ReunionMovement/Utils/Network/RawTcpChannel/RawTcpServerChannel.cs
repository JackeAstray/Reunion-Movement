using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

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

        /// <summary>事件队列上限：主线程停摆时后台接收仍在入队，超限丢弃最旧事件防内存无界增长</summary>
        private const int MaxPendingEvents = 1024;

        enum EventType { Connected, Error }

        TcpListener listener;
        Thread acceptThread;
        volatile bool running;
        readonly ConcurrentQueue<(EventType type, int connectionId, object connection, string message)> events = new ConcurrentQueue<(EventType type, int connectionId, object connection, string message)>();
        readonly Dictionary<int, RawTcpServerConnection> connections = new Dictionary<int, RawTcpServerConnection>();
        // TickRefresh 复用快照列表（避免每帧 new List 分配）
        readonly List<RawTcpServerConnection> tickSnapshot = new List<RawTcpServerConnection>();
        readonly int receiveChunkSize;
        int nextConnectionId = 1;

        /// <summary>有界入队：超限时丢弃最旧事件（并发下的近似判断即可）</summary>
        private void EnqueueEvent((EventType type, int connectionId, object connection, string message) ev)
        {
            if (events.Count >= MaxPendingEvents) events.TryDequeue(out _);
            events.Enqueue(ev);
        }

        public string ChannelName { get; set; }

        public int Port { get; private set; }

        public string Host => string.Empty; // 监听所有网卡

        public bool Active => running;

        public bool IsOpen => running;

        public event Action<int, string> OnConnected;
        public event Action<int, byte[]> OnDataReceived;
        public event Action<int> OnDisconnected;
        public event Action<int, string> OnError;

        /// <summary>最大连接数（0 = 不限）。超出时新连接在接入阶段立即关闭并上报 OnError</summary>
        public int MaxConnections = 0;

        /// <summary>空闲超时（秒，0 = 禁用）：超过时长未收到任何数据的连接被关闭（半开连接/静默客户端回收）</summary>
        public float IdleTimeoutSeconds = 0f;

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
                    // 防止对端停止读取时发送缓冲填满导致主线程被 stream.Write 无限阻塞
                    client.SendTimeout = 10000;
                    int id = Interlocked.Increment(ref nextConnectionId);
                    var conn = new RawTcpServerConnection(id, client, receiveChunkSize);
                    string address = string.Empty;
                    try { address = client.Client.RemoteEndPoint?.ToString() ?? string.Empty; } catch { }
                    EnqueueEvent((EventType.Connected, id, conn, address));
                }
                catch (Exception ex)
                {
                    if (!running) return;
                    if (ex is SocketException || ex is ObjectDisposedException)
                    {
                        if (!running) return;
                    }
                    EnqueueEvent((EventType.Error, -1, null, "Accept 异常: " + ex.Message));
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
                try
                {
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
                            // 连接上限：每个连接 = 1 个接收线程 + 64KB 缓冲，
                            // 未限制时恶意连接洪水可耗尽线程与内存（DoS）
                            if (MaxConnections > 0 && connections.Count >= MaxConnections)
                            {
                                conn.Close();
                                OnError?.Invoke(ev.connectionId, string.Format("连接数已达上限 {0}，拒绝接入", MaxConnections));
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
                catch (Exception ex)
                {
                    Log.Warning("[RawTcpServerChannel] 事件订阅者异常（已隔离）: {0}", ex.Message);
                }
            }

            // 2) 各连接的接收队列（复用快照列表，Tick 期间字典可能被移除条目，不能直接遍历 Values）
            tickSnapshot.Clear();
            tickSnapshot.AddRange(connections.Values);
            foreach (var conn in tickSnapshot)
            {
                while (processed < 4096 && conn.TryDequeue(out var ev))
                {
                    processed++;
                    try
                    {
                        switch (ev.Type)
                        {
                            case RawTcpServerConnection.RecvEventType.Data:
                                // 更新最后活跃时间（空闲踢人判定依据）
                                conn.LastReceiveTime = Time.realtimeSinceStartup;
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
                    catch (Exception ex)
                    {
                        Log.Warning("[RawTcpServerChannel] 连接 {0} 事件订阅者异常（已隔离）: {1}", conn.Id, ex.Message);
                    }
                }
                // 发送失败等原因静默关闭的连接也需清理
                if (conn.IsClosed && connections.Remove(conn.Id))
                {
                    try { OnDisconnected?.Invoke(conn.Id); }
                    catch (Exception ex) { Log.Warning("[RawTcpServerChannel] OnDisconnected 订阅者异常（已隔离）: {0}", ex.Message); }
                }
                // 空闲超时踢人：半开连接（断电/静默）Read 永久阻塞不产生任何事件，
                // 必须在主线程按最后活跃时间回收，否则线程/缓冲永久泄漏
                else if (!conn.IsClosed && IdleTimeoutSeconds > 0f
                    && Time.realtimeSinceStartup - conn.LastReceiveTime > IdleTimeoutSeconds)
                {
                    Log.Warning("RawTcp 连接 {0} 空闲超时（{1}s），已关闭", conn.Id, IdleTimeoutSeconds);
                    if (connections.Remove(conn.Id))
                    {
                        conn.Close();
                        try { OnDisconnected?.Invoke(conn.Id); }
                        catch (Exception ex) { Log.Warning("[RawTcpServerChannel] OnDisconnected 订阅者异常（已隔离）: {0}", ex.Message); }
                    }
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
        // 事件队列上限（同上：主线程停摆时防内存无界增长）
        private const int MaxPendingEvents = 1024;
        NetworkStream stream;
        Thread recvThread;
        volatile bool running;
        volatile bool closed;

        public int Id { get; }

        public string Address { get; }

        public bool IsClosed => closed;

        /// <summary>最后收到数据的时间（主线程更新，供服务端空闲超时踢人判定）</summary>
        public float LastReceiveTime { get; set; }

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
                    // 有界入队：主线程停摆时丢弃最旧事件，防内存无界增长
                    if (events.Count >= MaxPendingEvents) events.TryDequeue(out _);
                    events.Enqueue(new RecvEvent { Type = RecvEventType.Data, Data = copy });
                }
            }
            catch (Exception ex)
            {
                if (running)
                {
                    if (events.Count >= MaxPendingEvents) events.TryDequeue(out _);
                    events.Enqueue(new RecvEvent { Type = RecvEventType.Error, Message = "接收异常: " + ex.Message });
                }
            }
            if (running)
            {
                if (events.Count >= MaxPendingEvents) events.TryDequeue(out _);
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
