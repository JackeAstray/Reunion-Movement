// 一个宏伟的发送管道，用于屏蔽所有复杂性。
// 安全地将消息从主线程发送到发送线程。
// -> 线程安全内置
// -> 未来会实现 byte[] 池化
//
// => 隐藏 telepathy 的所有复杂性
// => 易于在不同队列实现之间切换
// => 易于测试

using System;
using System.Collections.Generic;

namespace Telepathy
{
    public class MagnificentSendPipe
    {
        // 消息队列
        // ConcurrentQueue 会导致分配。改为 lock{}。
        // -> byte[] 始终为 MaxMessageSize
        // -> ArraySegment 指示实际的消息内容
        //
        // 重要：使用时均需 lock{}
        readonly Queue<ArraySegment<byte>> queue = new Queue<ArraySegment<byte>>();

        // byte[] 池以避免分配
        // Take & Return 在管道中得到了很好的封装。
        // 外部不需要担心任何事情。
        // 并且它可以方便地被测试。
        //
        // 重要：使用时均需 lock{}
        Pool<byte[]> pool;

        // 构造函数
        public MagnificentSendPipe(int MaxMessageSize)
        {
            // 初始化池以每次创建最大消息大小的 byte[]
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }

        // 供统计用。不要在调用后假设 Count 保持不变。
        public int Count
        {
            get { lock (this) { return queue.Count; } }
        }

        // 池计数，用于测试
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }

        // 入队消息
        // ArraySegment 用于稍后无分配发送。
        // -> segment 的数组只在 Enqueue() 返回之前被使用！
        public void Enqueue(ArraySegment<byte> message)
        {
            // 池和队列的使用始终需要锁定
            lock (this)
            {
                // ArraySegment 的数组只在返回之前有效，因此复制
                // 到我们可以安全排队的 byte[] 中。

                // 先从池中获取一个以避免分配
                byte[] bytes = pool.Take();

                // 将其复制进去
                Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                // 指示消息所在的部分
                ArraySegment<byte> segment = new ArraySegment<byte>(bytes, 0, message.Count);

                // 现在入队
                queue.Enqueue(segment);
            }
        }

        // 发送线程需要出队每个 byte[] 并写入 socket。
        // -> 一个个出队并写入 socket 可行，但是比一次性出队所有项要慢得多（少一次锁）
        // -> lock{} & DequeueAll 比 ConcurrentQueue 每次出队慢得多要快得多。
        //
        // -> 更简单的方案是返回一个包含所有 byte[] 的列表（会分配）并把每个写入 socket
        // -> 更快的方案是在这里把每个序列化进一个 payload 缓冲区并只向 socket 写一次。
        //    减少 socket 调用带来显著的性能提升。
        // -> 为了避免每次分配列表，我们在这里就把所有内容序列化进 payload
        // => 将所有复杂性包含在管道里，使得测试和修改算法非常容易！
        //
        // 重要：在这里序列化允许我们稍后将 byte[] 返回到池以完全避免分配！
        public bool DequeueAndSerializeAll(ref byte[] payload, out int packetSize)
        {
            // 池和队列的使用始终需要锁定
            lock (this)
            {
                // 空时不做任何事
                packetSize = 0;
                if (queue.Count == 0)
                    return false;

                // 可能有多个待处理消息。合并成一个包以避免 TCP 开销并提高性能。
                //
                // 重要：Mirror & DOTSNET 已经按 MaxMessageSize 批量处理，但我们仍然把所有待处理消息
                //        序列化进一个大 payload，这样我们只向 TCP 发送一次。这对性能非常重要。
                packetSize = 0;
                foreach (ArraySegment<byte> message in queue)
                    packetSize += 4 + message.Count; // 头 + 内容

                // 如果 payload 为 null 或者之前的太小，则创建新的
                // 重要：payload.Length 可能 > packetSize！不要使用它来判断
                if (payload == null || payload.Length < packetSize)
                    payload = new byte[packetSize];

                // 出队所有 byte[] 消息并序列化进包
                int position = 0;
                while (queue.Count > 0)
                {
                    // 出队
                    ArraySegment<byte> message = queue.Dequeue();

                    // 在 position 处写入头（大小）
                    Utils.IntToBytesBigEndianNonAlloc(message.Count, payload, position);
                    position += 4;

                    // 将消息复制到 payload 的 position 处
                    Buffer.BlockCopy(message.Array, message.Offset, payload, position, message.Count);
                    position += message.Count;

                    // 返回到池以便重用（避免分配！）
                    pool.Return(message.Array);
                }

                // 我们确实序列化了一些东西
                return true;
            }
        }

        public void Clear()
        {
            // 池和队列的使用始终需要锁定
            lock (this)
            {
                // 通过出队清空队列，以便将每个 byte[] 返回到池
                while (queue.Count > 0)
                {
                    pool.Return(queue.Dequeue().Array);
                }
            }
        }
    }
}