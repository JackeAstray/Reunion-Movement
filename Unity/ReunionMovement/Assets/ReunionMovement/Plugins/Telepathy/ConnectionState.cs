// 服务器和客户端都需要一个连接状态对象。
// -> 服务器需要它来跟踪多个连接
// -> 客户端需要它以便在每次新连接时创建新的连接状态，从而避免正在退出的线程仍然修改当前状态的竞态。
//    （修复了所有不稳定的测试）
//
// ... 此外，它还允许我们共享代码！
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class ConnectionState
    {
        public TcpClient client;

        // 将消息从主线程发送到发送线程的线程安全管道
        public readonly MagnificentSendPipe sendPipe;

        // ManualResetEvent 用于唤醒发送线程。比 Thread.Sleep 更好。
        // -> 发送完毕时调用 Set()
        // -> 再次有东西可发送时调用 Reset()
        // -> 调用 WaitOne() 来阻塞直到被 Set()
        public ManualResetEvent sendPending = new ManualResetEvent(false);

        public ConnectionState(TcpClient client, int MaxMessageSize)
        {
            this.client = client;

            // 使用最大消息大小为池化创建发送管道
            sendPipe = new MagnificentSendPipe(MaxMessageSize);
        }
    }
}