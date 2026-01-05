using System;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    // ClientState 对象，可以安全地传递给接收线程。
    // => 允许我们在每次连接时创建一个新对象并启动一个
    //    接收线程。
    // => 完全保护我们免受数据竞争。修复了所有不稳定的测试，
    //    例如 .Connecting 或 .client 在尝试新连接时仍被正在退出的线程使用。
    // => 每次创建一个新的客户端状态对象是防止数据竞争的最佳解决方案！
    class ClientConnectionState : ConnectionState
    {
        public Thread receiveThread;

        // TcpClient.Connected 不会检查 socket != null，这会在连接被关闭时
        // 导致 NullReferenceExceptions。 -> 我们在此手动检查。
        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

        // TcpClient 没有用于检查 'connecting' 状态的方法。我们需要手动跟踪。
        // -> 检查 'thread.IsAlive && !Connected' 不够，因为在断开连接后短时间内
        //    线程仍然存活且 Connected 为 false，这会导致竞态条件。
        // -> 我们使用线程安全的布尔封装，所以 ThreadFunction 可以保持静态（需要一个公共锁）。
        // => Connecting 在此处第一次调用 Connect() 时为 true，直到 TcpClient.Connect() 返回为止。
        // => bool 在 C# 中是原子性的（参见语言规范），并使用 volatile 避免编译器重排访问。
        public volatile bool Connecting;

        // 用于接收消息的线程安全管道
        // => 放在 client connection state 内以便我们可以为每次连接创建一个新状态
        //    （不同于 server，它为所有连接使用一个接收管道）
        public readonly MagnificentReceivePipe receivePipe;

        // 构造函数总是为客户端连接创建新的 TcpClient！
        public ClientConnectionState(int MaxMessageSize) : base(new TcpClient(), MaxMessageSize)
        {
            // 为池化创建接收管道并设置最大消息大小
            receivePipe = new MagnificentReceivePipe(MaxMessageSize);
        }

        // 安全地释放所有状态
        public void Dispose()
        {
            // 关闭 client
            client.Close();

            // 等待线程结束。这样可以保证我们在 Disconnect 后可以立即调用 Connect()
            // -> 调用 .Join 有时会无限等待，例如在尝试连接到无响应地址时调用 Disconnect
            receiveThread?.Interrupt();

            // 我们中断了接收线程，所以不能保证 Connecting 已被重置。手动重置它。
            Connecting = false;

            // 清空发送管道。无需保留元素。
            // （不同于接收队列，接收队列仍需处理最新的 Disconnected 消消息等）
            sendPipe.Clear();

            // 重要：不要清空 RECEIVE PIPE。
            // 我们仍然希望在 Tick() 中处理断开连接的消息！

            // 完全释放此 client 对象。线程结束后没有人再使用它，这样 Connected 也会立即变为 false。
            client = null;
        }
    }

    public class Client : Common
    {
        // 可挂接的事件
        // => OnData 使用 ArraySegment，以便稍后无分配接收
        public Action OnConnected;
        public Action<ArraySegment<byte>> OnData;
        public Action OnDisconnected;

        // 如果发送队列变得太大，则断开连接。
        // -> 如果网络比输入慢，这可以避免队列无限增长。
        // -> 断开连接对于负载均衡很有用。断开一个连接比冒着整个服务器/所有连接的风险要好。
        // -> 巨大的队列还会引入数秒的延迟。
        //
        // Mirror/DOTSNET 使用 MaxMessageSize 批量处理，因此对于 16kb 的最大大小：
        //   limit =  1,000 表示 16 MB 内存/连接
        //   limit = 10,000 表示 160 MB 内存/连接
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;

        // 所有客户端状态封装在一个对象中并传递给 ReceiveThread
        // => 我们在每次连接时创建一个新的状态对象以避免旧线程仍修改当前状态的竞态
        ClientConnectionState state;

        // Connected & Connecting
        public bool Connected => state != null && state.Connected;
        public bool Connecting => state != null && state.Connecting;

        // 管道计数，对调试/基准有用
        public int ReceivePipeCount => state != null ? state.receivePipe.TotalCount : 0;

        // 构造函数
        public Client(int MaxMessageSize) : base(MaxMessageSize) { }

        // 线程函数
        // STATIC 以避免共享状态！
        // => 传入 ClientState 对象。每个新线程都创建一个新的状态对象！
        // => 避免旧的正在退出的线程仍修改当前线程的状态 :/
        static void ReceiveThreadFunction(ClientConnectionState state, string ip, int port, int MaxMessageSize, bool NoDelay, int SendTimeout, int ReceiveTimeout, int ReceiveQueueLimit)

        {
            Thread sendThread = null;

            // 必须用 try/catch 包裹，否则线程异常会静默发生
            try
            {
                // 连接（阻塞）
                state.client.Connect(ip, port);
                state.Connecting = false; // volatile!

                // 在 Connect() 创建 socket 后设置 socket 选项
                // （不要在构造函数后设置，因为我们在那里清除了 socket）
                state.client.NoDelay = NoDelay;
                state.client.SendTimeout = SendTimeout;
                state.client.ReceiveTimeout = ReceiveTimeout;

                // 在连接后才启动发送线程
                // 重要：不要在线程之间共享状态！
                sendThread = new Thread(() => { ThreadFunctions.SendLoop(0, state.client, state.sendPipe, state.sendPending); });
                sendThread.IsBackground = true;
                sendThread.Start();

                // 运行接收循环
                // （receive pipe 在所有循环之间共享）
                ThreadFunctions.ReceiveLoop(0, state.client, MaxMessageSize, state.receivePipe, ReceiveQueueLimit);
            }
            catch (SocketException exception)
            {
                // 当 ip 地址正确但该 ip/port 上没有服务器时会发生
                Log.Info("客户端接收：连接到 ip=" + ip + " 端口=" + port + " 失败，原因：" + exception);
            }
            catch (ThreadInterruptedException)
            {
                // 如果 Disconnect() 中断线程，这是预期的
            }
            catch (ThreadAbortException)
            {
                // 如果 Disconnect() 中止线程，这是预期的
            }
            catch (ObjectDisposedException)
            {
                // 如果 Disconnect() 中断线程并在 ReceiveThread 正在阻塞 Connect() 时释放了 client，这种情况会发生
            }
            catch (Exception exception)
            {
                // 出现了问题。可能很重要。
                Log.Error("客户端接收异常：" + exception);
            }
            // 添加 'Disconnected' 事件到接收管道，以便调用者知道 Connect 失败。否则他们永远不会知道。
            state.receivePipe.Enqueue(0, EventType.Disconnected, default);

            // sendthread 可能正等待 ManualResetEvent，
            // 因此如果连接已关闭，确保它也结束。
            // 否则发送线程只有在实际发送数据时才会结束。
            sendThread?.Interrupt();

            // Connect 可能已经失败。线程可能已关闭。
            // 无论如何我们都重置 connecting 状态。
            state.Connecting = false;

            // 如果执行到这里，那么我们已完成。ReceiveLoop 会执行清理，但如果 connect 失败，我们也在这里清理。
            state.client?.Close();
        }

        public void Connect(string ip, int port)
        {
            // 如果已启动则不执行
            if (Connecting || Connected)
            {
                Log.Warning("Telepathy 客户端无法创建连接，因为已有连接正在建立或已连接");
                return;
            }

            // 覆盖旧线程的状态对象。创建新的以避免旧线程仍修改当前状态的竞态！
            state = new ClientConnectionState(MaxMessageSize);

            // 现在开始连接，直到 Connect 成功或失败
            state.Connecting = true;

            // 创建一个既支持 IPv4、IPv6 又支持主机名解析的 TcpClient。
            //
            // * TcpClient(hostname, port): 可行但会立即连接（并阻塞）
            // * TcpClient(AddressFamily.InterNetworkV6): 接受 IPv4 和 IPv6 地址但仅连接 IPv6 服务器（例如 Telepathy）。
            //   即使启用了 DualMode，它也不会连接到 IPv4 服务器（例如 Mirror Booster）。
            // * TcpClient(): 在内部创建 IPv4 socket，这会强制 Connect() 只能使用 IPv4 sockets。
            //
            // => 技巧是清除内部的 IPv4 socket，这样 Connect 会解析主机名并根据需要创建 IPv4 或 IPv6 socket（参见 TcpClient 源码）
            state.client.Client = null; // 清除内部 IPv4 socket，直到 Connect()

            // client.Connect(ip, port) 是阻塞调用。我们在线程中调用它并立即返回。
            // -> 这样应用程序在连接耗时较长时不会挂起，尤其适合游戏
            // -> 也避免了使用 client.BeginConnect，它在快速创建多个客户端时有时会失败
            state.receiveThread = new Thread(() =>
            {
                ReceiveThreadFunction(state, ip, port, MaxMessageSize, NoDelay, SendTimeout, ReceiveTimeout, ReceiveQueueLimit);
            });
            state.receiveThread.IsBackground = true;
            state.receiveThread.Start();
        }

        public void Disconnect()
        {
            // 只有在已启动时才执行
            if (Connecting || Connected)
            {
                // 安全地释放所有状态
                state.Dispose();

                // 重要：不要将 state 设为 null！
                // 我们仍然希望处理管道中的断开连接消息等！
            }
        }

        // 使用 socket 连接向服务器发送消息。
        // ArraySegment 用于稍后无分配发送。
        // -> segment 的数组只在 Send() 返回之前被使用！
        public bool Send(ArraySegment<byte> message)
        {
            if (Connected)
            {
                // 限制最大消息大小以避免分配攻击。
                if (message.Count <= MaxMessageSize)
                {
                    // 检查发送管道限制
                    if (state.sendPipe.Count < SendQueueLimit)
                    {
                        // 添加到线程安全的发送管道并立即返回。
                        // 在这里直接调用 Send 会阻塞（如果另一端延迟或线路断开则可能时间较长）
                        state.sendPipe.Enqueue(message);
                        state.sendPending.Set(); // 中断 SendThread 的 WaitOne()
                        return true;
                    }
                    // 如果发送队列变得太大则断开连接。
                    // -> 避免队列内存无限增长
                    // -> 避免延迟无限增长
                    else
                    {
                        // 记录原因
                        Log.Warning($"Client.Send：发送队列已达到限制 {SendQueueLimit}。这可能是因为发送速度超过网络处理能力。为避免内存与延迟无限增长，正在断开连接。");

                        // 直接关闭它。发送线程会处理其余操作。
                        state.client.Close();
                        return false;
                    }
                }
                Log.Error("Client.Send：消息过大：" + message.Count + "。限制：" + MaxMessageSize);
                return false;
            }
            Log.Warning("Client.Send：未连接！");
            return false;
        }

        // Tick: 处理最多 'limit' 个消息
        // => limit 参数用于避免死锁 / 过长的冻结，如果服务器或客户端处理网络负载过慢
        // => 返回剩余未处理消息数量，便于调用者下次继续处理
        //
        // Tick() 可能处理多个消息，但 Mirror 需要一种在场景更改消息到达时立即停止处理的方式。
        // Mirror 在场景更改期间不能处理任何其他消息。
        public int Tick(int processLimit, Func<bool> checkEnabled = null)
        {
            // 只有在 state 已创建后（connect() 之后）才处理
            // 注意：我们不检查 'only if connected' 因为我们仍希望处理断开连接后的消息！
            if (state == null)
                return 0;

            // 处理最多 'processLimit' 条消息
            for (int i = 0; i < processLimit; ++i)
            {
                // 如果 checkEnabled 返回 false，则停止处理（例如场景更改）
                if (checkEnabled != null && !checkEnabled())
                    break;

                // 先 peek。允许我们在不移除元素的情况下处理第一个排队项，从而保持池化的 byte[] 可用。
                if (state.receivePipe.TryPeek(out int _, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch (eventType)
                    {
                        case EventType.Connected:
                            OnConnected?.Invoke();
                            break;
                        case EventType.Data:
                            OnData?.Invoke(message);
                            break;
                        case EventType.Disconnected:
                            OnDisconnected?.Invoke();
                            break;
                    }

                    // 重要：在处理完事件之后再出队并将其返回到池中。
                    state.receivePipe.TryDequeue();
                }
                // 没有更多消息，停止循环。
                else break;
            }

            // 返回剩余待处理的消息数，供下次使用
            return state.receivePipe.TotalCount;
        }
    }
}
