using System;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public class WebSocketClientStandAlone : SimpleWebClient
    {
        readonly ClientSslHelper sslHelper;
        readonly ClientHandshake handshake;
        readonly TcpConfig tcpConfig;
        Connection conn;

        internal WebSocketClientStandAlone(int maxMessageSize, int maxMessagesPerTick, TcpConfig tcpConfig) : base(maxMessageSize, maxMessagesPerTick)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException();
#else
            sslHelper = new ClientSslHelper();
            handshake = new ClientHandshake();
            this.tcpConfig = tcpConfig;
#endif
        }

        public override void Connect(Uri serverAddress)
        {
            state = ClientState.Connecting;

            // 在启动线程之前创建连接，这样在连接建立前发送队列就已经存在
            TcpClient client = new TcpClient();
            tcpConfig.ApplyTo(client);

            // 在此创建 Connection 对象，以便在连接失败时正确通过 Dispose 断开连接
            conn = new Connection(client, AfterConnectionDisposed);

            Thread receiveThread = new Thread(() => ConnectAndReceiveLoop(serverAddress));
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        void ConnectAndReceiveLoop(Uri serverAddress)
        {
            try
            {
                // 连接在上面创建
                TcpClient client = conn.client;
                conn.receiveThread = Thread.CurrentThread;

                try
                {
                    client.Connect(serverAddress.Host, serverAddress.Port);
                }
                catch (SocketException)
                {
                    client.Dispose();
                    throw;
                }


                bool success = sslHelper.TryCreateStream(conn, serverAddress);
                if (!success)
                {
                    Log.Warn("[SWT-WebSocketClientStandAlone]: 无法使用 {0} 创建流", serverAddress);
                    conn.Dispose();
                    return;
                }

                success = handshake.TryHandshake(conn, serverAddress);
                if (!success)
                {
                    Log.Warn("[SWT-WebSocketClientStandAlone]: 与 {0} 握手失败", serverAddress);
                    conn.Dispose();
                    return;
                }

                Log.Info("[SWT-WebSocketClientStandAlone]: 与 {0} 握手成功", serverAddress);

                state = ClientState.Connected;

                receiveQueue.Enqueue(new Message(EventType.Connected));

                Thread sendThread = new Thread(() =>
                {
                    SendLoop.Config sendConfig = new SendLoop.Config(
                        conn,
                        bufferSize: Constants.HeaderSize + Constants.MaskSize + maxMessageSize,
                        setMask: true);

                    SendLoop.Loop(sendConfig);
                });

                conn.sendThread = sendThread;
                sendThread.IsBackground = true;
                sendThread.Start();

                ReceiveLoop.Config config = new ReceiveLoop.Config(conn,
                    maxMessageSize,
                    false,
                    receiveQueue,
                    bufferPool);
                ReceiveLoop.Loop(config);
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException) { Log.Error("[SWT-WebSocketClientStandAlone]: 线程被中止"); }
            catch (Exception e) { Log.Exception(e); }
            finally
            {
                // 在此关闭连接，以防连接失败
                conn?.Dispose();
            }
        }

        void AfterConnectionDisposed(Connection conn)
        {
            state = ClientState.NotConnected;
            // 确保断开事件只被调用一次
            receiveQueue.Enqueue(new Message(EventType.Disconnected));
        }

        public override void Disconnect()
        {
            state = ClientState.Disconnecting;
            Log.Verbose("[SWT-WebSocketClientStandAlone]: 调用断开连接");

            if (conn == null)
                state = ClientState.NotConnected;
            else
                conn?.Dispose();
        }

        public override void Send(ArraySegment<byte> segment)
        {
            ArrayBuffer buffer = bufferPool.Take(segment.Count);
            buffer.CopyFrom(segment);

            conn.sendQueue.Enqueue(buffer);
            conn.sendPending.Set();
        }
    }
}
