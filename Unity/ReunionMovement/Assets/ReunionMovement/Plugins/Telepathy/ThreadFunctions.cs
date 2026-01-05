// 重要
// 强制所有线程函数为静态。
// => Common.Send/ReceiveLoop 非常危险，因为很容易不小心在不同线程之间共享 Common 的状态。
// => header buffer、payload 等曾在将线程函数从静态改为非静态后被意外共享。
// => C# 不会自动检测数据竞争。我们所能做的最好办法是把所有线程代码移动到静态函数并将所有状态传入它们。
//
// 为了强调不要改为非静态，我们把它们放在一个静态类中！
using System;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public static class ThreadFunctions
    {
        // 通过流发送消息（<size,content> 消息结构）。此函数有时会阻塞！
        //（例如，如果某人延迟很高或线路被切断）
        // -> payload 可以包含多个 <<size, content, size, content, ...> 部分
        public static bool SendMessagesBlocking(NetworkStream stream, byte[] payload, int packetSize)
        {
            // stream.Write 在对端关闭连接时会抛出异常
            try
            {
                // 写入整个内容
                stream.Write(payload, 0, packetSize);
                return true;
            }
            catch (Exception exception)
            {
                // 将其记录为信息而不是错误，因为服务器有时会关闭
                Log.Info("发送: stream.Write 异常: " + exception);
                return false;
            }
        }
        // 以阻塞方式读取消息。
        // 将数据写入 byte[] 并返回写入的字节数以避免分配。
        public static bool ReadMessageBlocking(NetworkStream stream, int MaxMessageSize, byte[] headerBuffer, byte[] payloadBuffer, out int size)
        {
            size = 0;

            // payloadBuffer 需要是 Header + MaxMessageSize
            if (payloadBuffer.Length != 4 + MaxMessageSize)
            {
                Log.Error($"ReadMessageBlocking: payloadBuffer 需要大小 4 + MaxMessageSize = {4 + MaxMessageSize}，而不是 {payloadBuffer.Length}");
                return false;
            }

            // 精确读取 4 字节头（阻塞）
            if (!stream.ReadExactly(headerBuffer, 4))
                return false;

            // 转换为 int
            size = Utils.BytesToIntBigEndian(headerBuffer);

            // 保护免受分配攻击。攻击者可能发送多个伪造的 "2GB 头" 包，导致服务器分配多个 2GB 字节数组并耗尽内存。
            //
            // 也要保护 size <= 0 的情况，这会导致问题
            if (size > 0 && size <= MaxMessageSize)
            {
                // 精确读取 'size' 字节内容（阻塞）
                return stream.ReadExactly(payloadBuffer, size);
            }
            Log.Warning("ReadMessageBlocking: 可能的头部攻击，头部大小为: " + size + " 字节。");
            return false;
        }

        // 线程接收函数对客户端和服务器的客户端都是相同的
        public static void ReceiveLoop(int connectionId, TcpClient client, int MaxMessageSize, MagnificentReceivePipe receivePipe, int QueueLimit)
        {
            // 从 client 获取 NetworkStream
            NetworkStream stream = client.GetStream();

            // 每个接收循环需要它自己的接收缓冲区，大小为 HeaderSize + MaxMessageSize
            // 以避免运行时分配。
            // 重要：不要把它们做成成员，否则服务器上的每个连接将同时使用相同的缓冲区。
            byte[] receiveBuffer = new byte[4 + MaxMessageSize];

            // 避免 header[4] 分配
            //
            // 重要：不要把它们做成成员，否则服务器上的每个连接将同时使用相同的缓冲区。
            byte[] headerBuffer = new byte[4];

            // 必须用 try/catch 包裹，否则线程异常会静默发生
            try
            {
                // 将 Connected 事件添加到管道
                receivePipe.Enqueue(connectionId, EventType.Connected, default);

                // 关于读取数据：
                // -> 通常我们会尽可能多地读取，然后从接收的数据中提取尽可能多的 <size,content>,<size,content> 消息。那样做既复杂又开销大
                // -> 我们使用一个技巧：
                //      Read(2) -> size
                //        Read(size) -> content
                //      重复
                //    Read 是阻塞的，但在完整消息到达之前等待是最优的。
                // => 这是最优雅且快速的解决方案。
                //    + 不会重新调整大小
                //    + 没有额外分配，只有一个用于内容的分配
                //    + 没有复杂的提取逻辑
                while (true)
                {
                    // 读取下一条消息（阻塞），如果流关闭则停止
                    if (!ReadMessageBlocking(stream, MaxMessageSize, headerBuffer, receiveBuffer, out int size))
                        // 使用 break 而不是 return，以便仍然执行流关闭
                        break;

                    // 为读取的消息创建 ArraySegment
                    ArraySegment<byte> message = new ArraySegment<byte>(receiveBuffer, 0, size);

                    // 通过管道发送到主线程
                    // -> 它会内部复制消息，这样我们就可以重用接收缓冲区进行下一次读取！
                    receivePipe.Enqueue(connectionId, EventType.Data, message);

                    // 如果该 connectionId 的接收管道变得太大，则断开连接。
                    // -> 避免队列内存无限增长
                    // -> 断开对负载均衡有益
                    if (receivePipe.Count(connectionId) >= QueueLimit)
                    {
                        // 记录原因
                        Log.Warning($"receivePipe 达到连接 {connectionId} 的限制 {QueueLimit}。这可能发生在网络消息到达速度远快于处理速度。为负载均衡断开该连接。");

                        // 重要：不要清空整个队列。我们为所有连接使用一个队列。
                        //receivePipe.Clear();

                        // 只是跳出循环。finally{} 会关闭所有东西。
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                // 出现了问题。线程被中断或连接被关闭，等等。
                // -> 无论如何我们都应该优雅地停止
                Log.Info("ReceiveLoop: connectionId=" + connectionId + " 接收函数结束，原因: " + exception);
            }
            finally
            {
                // 无论如何都清理
                stream.Close();
                client.Close();

                // 断开后添加 'Disconnected' 消息。
                // -> 必须在关闭流之后添加，以避免竞争条件
                //    例如 Disconnected -> Reconnect 在流关闭前 Connected 仍然为 true 会导致问题
                receivePipe.Enqueue(connectionId, EventType.Disconnected, default);
            }
        }
        // 线程发送函数
        // 注意：我们确实需要每个连接一个发送线程，以便某个连接阻塞时，其他连接仍能继续发送
        public static void SendLoop(int connectionId, TcpClient client, MagnificentSendPipe sendPipe, ManualResetEvent sendPending)
        {
            // 从 client 获取 NetworkStream
            NetworkStream stream = client.GetStream();

            // 避免 payload[packetSize] 分配。大小按需动态增长以便批量发送。
            // 重要：不要把它做成成员，否则服务器上的每个连接将同时使用相同的缓冲区。
            byte[] payload = null;

            try
            {
                while (client.Connected) // 尝试这样循环。客户端最终会被关闭。
                {
                    // 在做任何事之前重置 ManualResetEvent。这样可以避免竞态条件。
                    // -> 否则可能在重置前被 Send() 调用，从而忽略该调用直到下一次 Set
                    sendPending.Reset(); // WaitOne() 将阻塞直到再次 Set()

                    // 出队并序列化所有消息
                    // 一个加锁的 TryDequeueAll 比 ConcurrentQueue 快两倍，参考 SafeQueue.cs！
                    if (sendPipe.DequeueAndSerializeAll(ref payload, out int packetSize))
                    {
                        // 发送消息（阻塞）或在流关闭时停止
                        if (!SendMessagesBlocking(stream, payload, packetSize))
                            // 使用 break 而不是 return，以便仍然执行流关闭
                            break;
                    }

                    // 不要让 CPU 100% 占用：等待直到队列非空
                    sendPending.WaitOne();
                }
            }
            catch (ThreadAbortException)
            {
                // 在停止时发生。不要记录任何内容。
            }
            catch (ThreadInterruptedException)
            {
                // 当接收线程中断发送线程时会发生。
            }
            catch (Exception exception)
            {
                // 出现了问题。线程被中断或连接关闭，等等。优雅停止
                Log.Info("SendLoop 异常: connectionId=" + connectionId + " 原因: " + exception);
            }
            finally
            {
                // 无论如何都清理
                // 我们在发送时可能会遇到 SocketException，这时应该关闭连接
                // 这会导致 ReceiveLoop 结束并触发 Disconnected 消息。否则即使我们不能再发送，连接也会一直保持。
                stream.Close();
                client.Close();
            }
        }
    }
}