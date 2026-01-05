using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public class WebSocketServer
    {
        public readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

        readonly TcpConfig tcpConfig;
        readonly int maxMessageSize;

        TcpListener listener;
        Thread acceptThread;
        bool serverStopped;
        readonly ServerHandshake handShake;
        readonly ServerSslHelper sslHelper;
        readonly BufferPool bufferPool;
        readonly ConcurrentDictionary<int, Connection> connections = new ConcurrentDictionary<int, Connection>();

        int _idCounter = 0;

        public WebSocketServer(TcpConfig tcpConfig, int maxMessageSize, int handshakeMaxSize, SslConfig sslConfig, BufferPool bufferPool)
        {
            this.tcpConfig = tcpConfig;
            this.maxMessageSize = maxMessageSize;
            sslHelper = new ServerSslHelper(sslConfig);
            this.bufferPool = bufferPool;
            handShake = new ServerHandshake(this.bufferPool, handshakeMaxSize);
        }

        public void Listen(int port)
        {
            listener = TcpListener.Create(port);
            listener.Start();

            Log.Verbose("[SWT-WebSocketServer]: 服务器已在端口 {0} 启动", port);

            acceptThread = new Thread(acceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        public void Stop()
        {
            serverStopped = true;

            // 先中断然后停止，以便正确处理异常
            acceptThread?.Interrupt();
            listener?.Stop();
            acceptThread = null;

            Log.Verbose("[SWT-WebSocketServer]: 服务器停止...正在关闭所有连接。");

            // 复制一份，以免在 foreach 中移除值时发生错误
            Connection[] connectionsCopy = connections.Values.ToArray();
            foreach (Connection conn in connectionsCopy)
                conn.Dispose();

            connections.Clear();
        }

        void acceptLoop()
        {
            try
            {
                try
                {
                    while (true)
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        tcpConfig.ApplyTo(client);

                        // TODO 在将其添加到 connections 字典之前跟踪连接
                        //      这可能不是问题，因为 HandshakeAndReceiveLoop 会检查停止并在发送消息到队列之前返回/释放
                        Connection conn = new Connection(client, AfterConnectionDisposed);
                        Log.Verbose("[SWT-WebSocketServer]: 已有客户端连接，来源 {0}", conn);

                        // 握手需要它自己的线程，因为它需要等待客户端的消息
                        Thread receiveThread = new Thread(() => HandshakeAndReceiveLoop(conn));

                        conn.receiveThread = receiveThread;

                        receiveThread.IsBackground = true;
                        receiveThread.Start();
                    }
                }
                catch (SocketException)
                {
                    // 检查是否为 Interrupted/Abort
                    Utils.CheckForInterupt();
                    throw;
                }
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException) { Log.Error("[SWT-WebSocketServer]: 线程终止异常"); }
            catch (Exception e) { Log.Exception(e); }
        }

        void HandshakeAndReceiveLoop(Connection conn)
        {
            try
            {
                bool success = sslHelper.TryCreateStream(conn);
                if (!success)
                {
                    Log.Warn("[SWT-WebSocketServer]: 创建 SSL Stream 失败 {0}", conn);
                    conn.Dispose();
                    return;
                }

                success = handShake.TryHandshake(conn);

                if (success)
                    Log.Verbose("[SWT-WebSocketServer]: 已发送握手 {0}, false", conn);
                else
                {
                    Log.Warn("[SWT-WebSocketServer]: 握手失败 {0}", conn);
                    conn.Dispose();
                    return;
                }

                // 检查在接受此客户端后是否已调用 Stop
                if (serverStopped)
                {
                    Log.Warn("[SWT-WebSocketServer]: 握手成功后服务器已停止");
                    return;
                }

                conn.connId = Interlocked.Increment(ref _idCounter);
                connections.TryAdd(conn.connId, conn);

                receiveQueue.Enqueue(new Message(conn.connId, EventType.Connected));

                Thread sendThread = new Thread(() =>
                {
                    SendLoop.Config sendConfig = new SendLoop.Config(
                        conn,
                        bufferSize: Constants.HeaderSize + maxMessageSize,
                        setMask: false);

                    SendLoop.Loop(sendConfig);
                });

                conn.sendThread = sendThread;
                sendThread.IsBackground = true;
                sendThread.Name = $"SendThread {conn.connId}";
                sendThread.Start();

                ReceiveLoop.Config receiveConfig = new ReceiveLoop.Config(
                    conn,
                    maxMessageSize,
                    expectMask: true,
                    receiveQueue,
                    bufferPool);

                ReceiveLoop.Loop(receiveConfig);
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException) { Log.Error("[SWT-WebSocketServer]: 线程终止异常"); }
            catch (Exception e) { Log.Exception(e); }
            finally
            {
                // 在此处关闭以防连接失败
                conn.Dispose();
            }
        }

        void AfterConnectionDisposed(Connection conn)
        {
            if (conn.connId != Connection.IdNotSet)
            {
                receiveQueue.Enqueue(new Message(conn.connId, EventType.Disconnected));
                connections.TryRemove(conn.connId, out Connection _);
            }
        }

        public void Send(int id, ArrayBuffer buffer)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                conn.sendQueue.Enqueue(buffer);
                conn.sendPending.Set();
            }
            else
                Log.Warn("[SWT-WebSocketServer]: 无法向 {0} 发送消息，因为在字典中未找到连接。可能已断开连接。", id);
        }

        public bool CloseConnection(int id)
        {
            if (connections.TryGetValue(id, out Connection conn))
            {
                Log.Info($"[SWT-WebSocketServer]: 正在断开连接 {0}", id);
                conn.Dispose();
                return true;
            }
            else
            {
                Log.Warn("[SWT-WebSocketServer]: 无法踢出 {0}，因为未找到该 id。", id);
                return false;
            }
        }

        public string GetClientAddress(int id)
        {
            if (!connections.TryGetValue(id, out Connection conn))
            {
                Log.Warn("[SWT-WebSocketServer]: 无法获取连接 {0} 的地址，因为在字典中未找到连接。", id);
                return null;
            }

            return conn.remoteAddress;
        }

        public Request GetClientRequest(int id)
        {
            if (!connections.TryGetValue(id, out Connection conn))
            {
                Log.Warn("[SWT-WebSocketServer]: 无法获取连接 {0} 的请求，因为在字典中未找到连接。", id);
                return null;
            }

            return conn.request;
        }
    }
}
