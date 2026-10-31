using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using Gear.Common;
using Gear.Utilities;
namespace Gear.Framework.Modules
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

        /// <summary>丢弃告警节流时间戳（多线程竞争仅影响日志频率，可忽略）</summary>
        int lastDropWarnTick;

        /// <summary>有界入队：超限时丢弃最旧事件（并发下的近似判断即可），并低频告警暴露主线程停摆问题</summary>
        private void EnqueueEvent((EventType type, int connectionId, object connection, string message) ev)
        {
            if (events.Count >= MaxPendingEvents)
            {
                events.TryDequeue(out _);
                int now = Environment.TickCount;
                if (unchecked(now - lastDropWarnTick) > 1000)
                {
                    lastDropWarnTick = now;
                    Log.Warning("[RawTcpServerChannel] 事件队列满（{0}），丢弃最旧事件：主线程 TickRefresh 可能停摆，连接/数据事件将丢失", MaxPendingEvents);
                }
            }
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

        /// <summary>最大连接数（0 = 不限）。超出时新连接在接入阶段立即关闭并上报 OnError。
        /// 默认 64 防连接洪水（每连接 1 接收线程 + 缓冲），按需调大</summary>
        public int MaxConnections = 64;

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
                // 每连接独立配额：避免单个高吞吐连接吃满全局预算，导致其它连接的数据整帧被饿死
                int connBudget = 256;
                while (connBudget-- > 0 && processed < 4096 && conn.TryDequeue(out var ev))
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
        // 发送队列：主线程只入队，socket 写由线程池串行泵执行（对端不读时不再卡住主线程）
        readonly ConcurrentQueue<byte[]> sendQueue = new ConcurrentQueue<byte[]>();
        long queuedSendBytes;
        int sendPumpRunning;
        NetworkStream stream;
        Thread recvThread;
        volatile bool running;
        volatile bool closed;

        /// <summary>事件队列上限（同上：主线程停摆时防内存无界增长）</summary>
        private const int MaxPendingEvents = 1024;

        /// <summary>接收字节预算（每连接）：只按条数限流时 1024×64KB≈64MB/连接，按字节压到常量级</summary>
        private const long MaxPendingBytes = 4L * 1024 * 1024;

        /// <summary>发送积压预算（每连接）</summary>
        private const long MaxQueuedSendBytes = 2L * 1024 * 1024;

        long pendingBytes;

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
                    Enqueue(new RecvEvent { Type = RecvEventType.Data, Data = copy });
                }
            }
            catch (Exception ex)
            {
                if (running)
                {
                    Enqueue(new RecvEvent { Type = RecvEventType.Error, Message = "接收异常: " + ex.Message });
                }
            }
            if (running)
            {
                Enqueue(new RecvEvent { Type = RecvEventType.Disconnected });
            }
        }

        /// <summary>有界入队（条数 + 字节双预算），超限丢最旧，防主线程停摆时内存无界增长</summary>
        private void Enqueue(RecvEvent ev)
        {
            long after = Interlocked.Add(ref pendingBytes, ev.Data?.Length ?? 0);
            while ((events.Count >= MaxPendingEvents || after > MaxPendingBytes)
                   && events.TryDequeue(out var dropped))
            {
                after = Interlocked.Add(ref pendingBytes, -(dropped.Data?.Length ?? 0));
            }
            events.Enqueue(ev);
        }

        public bool TryDequeue(out RecvEvent ev)
        {
            if (!events.TryDequeue(out ev)) return false;
            Interlocked.Add(ref pendingBytes, -(ev.Data?.Length ?? 0));
            return true;
        }

        /// <summary>发送（非阻塞）：入队后由线程池串行写，失败时关闭连接交给 TickRefresh 回收</summary>
        public bool Send(byte[] data)
        {
            if (!running || closed || data == null || data.Length == 0) return false;

            long after = Interlocked.Add(ref queuedSendBytes, data.Length);
            if (after > MaxQueuedSendBytes)
            {
                Interlocked.Add(ref queuedSendBytes, -data.Length);
                Log.Warning("RawTcp 连接 {0} 发送队列积压超限（{1} 字节），本次发送被拒绝", Id, after);
                return false;
            }

            sendQueue.Enqueue(data);
            StartSendPump();
            return true;
        }

        private void StartSendPump()
        {
            if (Interlocked.CompareExchange(ref sendPumpRunning, 1, 0) != 0) return;
            _ = SendPumpAsync();
        }

        private async Task SendPumpAsync()
        {
            try
            {
                while (sendQueue.TryDequeue(out var data))
                {
                    try
                    {
                        if (!running || closed || stream == null) continue;
                        // 异步写也可能因对端零窗口无限挂起：加超时兜底（超时后关闭连接）
                        var writeTask = stream.WriteAsync(data, 0, data.Length);
                        var completed = await Task.WhenAny(writeTask, Task.Delay(10000)).ConfigureAwait(false);
                        if (completed != writeTask) throw new TimeoutException("发送超时");
                        await writeTask.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // 主动 Close 造成的写中断不是错误，不要往事件队列里塞虚假 Error
                        if (!closed)
                        {
                            Log.Warning("RawTcp 连接 {0} 发送失败: {1}", Id, ex.Message);
                            Enqueue(new RecvEvent { Type = RecvEventType.Error, Message = "发送失败: " + ex.Message });
                        }
                        Close();
                        break;
                    }
                    finally
                    {
                        Interlocked.Add(ref queuedSendBytes, -data.Length);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref sendPumpRunning, 0);
                if (!sendQueue.IsEmpty) StartSendPump();
            }
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            running = false;
            while (sendQueue.TryDequeue(out var pending))
            {
                Interlocked.Add(ref queuedSendBytes, -pending.Length);
            }
            try { stream?.Dispose(); } catch { }
            try { socket?.Close(); } catch { }
        }
    }
}
