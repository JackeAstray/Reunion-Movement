// 服务器和客户端使用的公共代码
namespace Telepathy
{
    public abstract class Common
    {
        // 重要：不要在发送/接收循环之间共享状态（数据竞争）
        //（除 receive pipe 外，该 pipe 用于所有线程）

        // NoDelay 禁用 Nagle 算法。降低 CPU% 和延迟但增加带宽
        public bool NoDelay = true;

        // 防止分配攻击。每个包前面都有长度头，因此攻击者可能发送一个伪造包 length=2GB，
        // 导致服务器分配 2GB 并耗尽内存。
        // -> 如果需要发送更大的文件，请简单地增大最大包大小！
        // -> 每条消息 16KB 应该足够。
        public readonly int MaxMessageSize;

        // 如果网络在发送过程中被切断，Send 将永远阻塞，因此需要超时（以毫秒为单位）
        public int SendTimeout = 5000;

        // 默认 TCP 接收超时可能非常大（分钟）。这对游戏来说太长了，
        // 让它可配置。
        // -> 以毫秒为单位
        // -> '0' 表示禁用
        // -> 默认禁用，因为有些人可能在没有 Mirror 且不发送心跳包的情况下使用 Telepathy，
        //    因此启用超时可能不合适
        public int ReceiveTimeout = 0;

        // 构造函数
        protected Common(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }
    }
}
