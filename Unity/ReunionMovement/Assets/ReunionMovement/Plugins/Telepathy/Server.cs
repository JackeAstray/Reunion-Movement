using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Server : Common
    {
        // 可挂接的事件
        // => OnData 使用 ArraySegment，以便稍后无分配接收
        public Action<int, string> OnConnected;
        public Action<int, ArraySegment<byte>> OnData;
        public Action<int> OnDisconnected;

        // 侦听器
        public TcpListener listener;
        Thread listenerThread;

        // 如果发送队列变得太大，则断开连接。
        // -> 避免队列内存无限增长
        // -> 断开对负载均衡有利。比冒着整个服务器/所有连接的风险要好
        // -> 巨大的队列还会引入数秒延迟
        //
        // Mirror/DOTSNET 使用 MaxMessageSize 批量处理，因此对于 16kb 的最大大小：
        //   limit =  1,000 表示 16 MB 内存/连接
        //   limit = 10,000 表示 160 MB 内存/连接
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;

        // 线程安全的接收管道
        // 重要：不幸的是，为每个连接使用一个管道在 150 CCU 测试下要慢很多。
        // 因此我们需要为所有连接使用一个管道。这能很好地扩展。
        protected MagnificentReceivePipe receivePipe;

        // 管道计数，对调试/基准有用
        public int ReceivePipeTotalCount => receivePipe.TotalCount;

        // 客户端集合 <connectionId, ConnectionState>
        readonly ConcurrentDictionary<int, ConnectionState> clients = new ConcurrentDictionary<int, ConnectionState>();

        // connectionId 计数器
        int counter;

        // 公开的下一个 id 函数，以防有人需要保留 id
        // （例如，如果 hostMode 应始终使用 0 连接，外部连接应从 1 开始等）
        public int NextConnectionId()
        {
            int id = Interlocked.Increment(ref counter);

            // 几乎不可能达到 int 最大值（约 20 亿）。即使每秒创建 1 个连接，也需要 68 年。
            // -> 但如果发生，我们应该抛出异常，因为调用者可能应该停止接受客户端。
            // -> 因此这里不需要实现 'bool Next(out id)'.
            if (id == int.MaxValue)
            {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }

        // 检查服务器是否在运行
        public bool Active => listenerThread != null && listenerThread.IsAlive;

        // 构造函数
        public Server(int MaxMessageSize) : base(MaxMessageSize) { }

        // 侦听线程的监听函数
        // 注意：没有 maxConnections 参数。上层 API 应处理该逻辑。
        void Listen(int port)
        {
            // 必须用 try/catch 包裹，否则线程异常会静默发生
            try
            {
                // 使用 .Create 在所有 IPv4 和 IPv6 地址上启动监听器
                listener = TcpListener.Create(port);
                listener.Server.NoDelay = NoDelay;
                // 重要：不要在 listener 上设置 send/receive 超时。
                // 在 Linux 上将接收超时设置在阻塞的 Accept 调用上会导致 EACCEPT（mono 将其解释为 EWOULDBLOCK）。
                // https://stackoverflow.com/questions/1917814/eagain-error-for-accept-on-blocking-socket/1918118#1918118
                // => 修复 https://github.com/vis2k/Mirror/issues/2695
                //
                //listener.Server.SendTimeout = SendTimeout;
                //listener.Server.ReceiveTimeout = ReceiveTimeout;
                listener.Start();
                Log.Info("服务器：正在监听端口=" + port);

                // 不断接受新的客户端
                while (true)
                {
                    // 等待并接受新客户端
                    // 注意：在这里使用 'using' 不合适，因为它会在线程启动后尝试 dispose，而我们仍然需要它在那个线程中使用
                    TcpClient client = listener.AcceptTcpClient();

                    // 设置 socket 选项
                    client.NoDelay = NoDelay;
                    client.SendTimeout = SendTimeout;
                    client.ReceiveTimeout = ReceiveTimeout;

                    // 线程安全地生成下一个 connection id
                    int connectionId = NextConnectionId();

                    // 立即添加到字典
                    ConnectionState connection = new ConnectionState(client, MaxMessageSize);
                    clients[connectionId] = connection;

                    // 为每个客户端生成一个发送线程
                    Thread sendThread = new Thread(() =>
                    {
                        // 用 try/catch 包裹，否则线程异常会静默发生
                        try
                        {
                            // 运行发送循环
                            // 重要：不要在线程之间共享状态！
                            ThreadFunctions.SendLoop(connectionId, client, connection.sendPipe, connection.sendPending);
                        }
                        catch (ThreadAbortException)
                        {
                            // 在停止时发生。无需记录。
                            //（我们在 SendLoop 中也捕获它，但在中止时仍会抛到这里，不要显示错误）
                        }
                        catch (Exception exception)
                        {
                            Log.Error("服务器发送线程异常：" + exception);
                        }
                    });
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    // 为每个客户端生成接收线程
                    Thread receiveThread = new Thread(() =>
                    {
                        // 用 try/catch 包裹，否则线程异常会静默发生
                        try
                        {
                            // 运行接收循环
                            // （receive pipe 在所有循环中共享）
                            ThreadFunctions.ReceiveLoop(connectionId, client, MaxMessageSize, receivePipe, ReceiveQueueLimit);

                            // 重要：在线程结束后不要从 clients 中移除。需要在 Tick() 中移除，以便仍能处理队列中的断开连接事件。
                            // （立即移除客户端会导致管道丢失，断开事件永远不会被处理）

                            // sendthread 可能正等待 ManualResetEvent，
                            // 因此如果连接已关闭，确保它也结束。
                            // 否则发送线程只有在实际发送数据时才会结束。
                            sendThread.Interrupt();
                        }
                        catch (Exception exception)
                        {
                            Log.Error("服务器客户端线程异常：" + exception);
                        }
                    });
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch (ThreadAbortException exception)
            {
                // 如果在按下 Play 再次运行时线程仍在运行，UnityEditor 会导致 AbortException。没关系。
                Log.Info("服务器线程已中止。这没关系。" + exception);
            }
            catch (SocketException exception)
            {
                // 调用 StopServer 将中断该线程并抛出 'SocketException: interrupted'。这是预期的。
                Log.Info("服务器线程已停止。这没关系。" + exception);
            }
            catch (Exception exception)
            {
                // 出现了问题。可能很重要。
                Log.Error("服务器异常：" + exception);
            }
        }

        // 在后台线程中开始监听新连接并为每个连接生成新线程。
        public bool Start(int port)
        {
            // 如果已经启动则不执行
            if (Active) return false;

            // 使用最大消息大小为池化创建接收管道
            // => 每次都创建新的管道！
            //    如果旧接收线程仍在结束，它可能仍在使用旧管道。我们不希望为我们的新启动冒旧数据的风险。
            receivePipe = new MagnificentReceivePipe(MaxMessageSize);

            // 启动监听线程
            //（设置较低优先级。如果主线程太忙，接受更多客户端也没有太大意义）
            Log.Info("服务器：启动，端口=" + port);
            listenerThread = new Thread(() => { Listen(port); });
            listenerThread.IsBackground = true;
            listenerThread.Priority = ThreadPriority.BelowNormal;
            listenerThread.Start();
            return true;
        }

        public void Stop()
        {
            // 只有在运行时才执行
            if (!Active) return;

            Log.Info("服务器：正在停止...");

            // 停止监听以便在我们关闭客户端连接时不会有新连接进来
            //（如果我们在 Start 后很快调用 Stop，listener 可能为 null）
            listener?.Stop();

            // 不惜一切代价中止 listener 线程。只有这样才能保证 Stop 之后 .Active 立即为 false。
            // -> 调用 .Join 有时会无限等待
            listenerThread?.Interrupt();
            listenerThread = null;

            // 关闭所有客户端连接
            foreach (KeyValuePair<int, ConnectionState> kvp in clients)
            {
                TcpClient client = kvp.Value.client;
                // 如果尚未关闭，则尝试关闭流。可能已由断开连接关闭，因此使用 try/catch
                try { client.GetStream().Close(); } catch { }
                client.Close();
            }

            // 清除客户端列表
            clients.Clear();

            // 重置计数器，以便如果我们再次启动，连接 ID 从 1 开始
            counter = 0;
        }

        // 使用 socket 连接向客户端发送消息。
        // ArraySegment 用于稍后无分配发送。
        // -> segment 的数组只在 Send() 返回之前被使用！
        public bool Send(int connectionId, ArraySegment<byte> message)
        {
            // 限制最大消息大小以避免分配攻击。
            if (message.Count <= MaxMessageSize)
            {
                // 找到连接
                if (clients.TryGetValue(connectionId, out ConnectionState connection))
                {
                    // 检查发送管道限制
                    if (connection.sendPipe.Count < SendQueueLimit)
                    {
                        // 添加到线程安全的发送管道并立即返回。
                        connection.sendPipe.Enqueue(message);
                        connection.sendPending.Set(); // 中断 SendThread 的 WaitOne()
                        return true;
                    }
                    // 如果发送队列变得太大则断开连接。
                    // -> 避免队列内存无限增长
                    // -> 断开有助于负载均衡。
                    else
                    {
                        // 记录原因
                        Log.Warning($"Server.Send：连接 {connectionId} 的发送队列已达到限制 {SendQueueLimit}。这可能是因为发送速度超过网络处理能力。为实现负载均衡，正在断开此连接。");

                        // 直接关闭它。发送线程会处理其余操作。
                        connection.client.Close();
                        return false;
                    }
                }

                // 向无效 connectionId 发送是有时会发生的。例如，如果客户端断开连接，服务器可能仍然尝试发送一帧，
                // 在它再次调用 GetNextMessages 并意识到断开发生之前。所以不要垃圾日志。
                return false;
            }
            Log.Error("Server.Send：消息过大：" + message.Count + "。限制：" + MaxMessageSize);
            return false;
        }

        // 获取客户端 IP，服务器有时需要，例如用于封禁
        public string GetClientAddress(int connectionId)
        {
            try
            {
                // 找到连接
                if (clients.TryGetValue(connectionId, out ConnectionState connection))
                {
                    return ((IPEndPoint)connection.client.Client.RemoteEndPoint).Address.ToString();
                }
                return "";
            }
            catch (SocketException)
            {
                // 在 UWP + Unity 2019 中使用 server.listener.LocalEndpoint 会导致异常，捕获以恢复
                return "unknown";
            }
            catch (ObjectDisposedException)
            {
                return "Disposed";
            }
            catch (Exception)
            {
                return "";
            }
        }

        // 断开（踢出）客户端
        public bool Disconnect(int connectionId)
        {
            // 找到连接
            if (clients.TryGetValue(connectionId, out ConnectionState connection))
            {
                // 直接关闭它。发送线程会处理其余操作。
                connection.client.Close();
                Log.Info("Server.Disconnect 连接Id:" + connectionId);
                return true;
            }
            return false;
        }

        // Tick: 为每个连接处理最多 'limit' 个消息
        // => limit 参数用于避免死锁 / 过长的冻结
        // => 返回剩余未处理消息数量，便于调用者下次继续处理
        public int Tick(int processLimit, Func<bool> checkEnabled = null)
        {
            // 只有在管道创建后（Start() 之后）才处理
            if (receivePipe == null)
                return 0;

            // 为该连接处理最多 'processLimit' 条消息
            for (int i = 0; i < processLimit; ++i)
            {
                // 如果 checkEnabled 返回 false，则停止处理（例如场景更改）
                if (checkEnabled != null && !checkEnabled())
                    break;

                // 先 peek。允许我们在不移除元素的情况下处理第一个排队项，从而保持池化的 byte[] 可用。
                if (receivePipe.TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case EventType.Connected:
                            OnConnected?.Invoke(connectionId, GetClientAddress(connectionId));
                            break;
                        case EventType.Data:
                            OnData?.Invoke(connectionId, message);
                            break;
                        case EventType.Disconnected:
                            OnDisconnected?.Invoke(connectionId);
                            // 在处理完最终的断开消息后，现在从 clients 中移除该连接。
                            clients.TryRemove(connectionId, out ConnectionState _);
                            break;
                    }

                    // 重要：在处理完事件之后再出队并将其返回到池中。
                    receivePipe.TryDequeue();
                }
                // 没有更多消息，停止循环。
                else break;
            }

            // 返回剩余待处理的消息数，供下次使用
            return receivePipe.TotalCount;
        }
    }
}
