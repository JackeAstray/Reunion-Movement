// 一个宏伟的接收管道，用于屏蔽所有复杂性。
// 安全地将消息从接收线程发送到主线程。
// -> 线程安全内置
// -> 将来将实现 byte[] 池化
//
// => 隐藏了 telepathy 的所有复杂性
// => 易于在堆栈/队列/ConcurrentQueue 等之间切换
// => 易于测试
using System;
using System.Collections.Generic;

namespace Telepathy
{
    public class MagnificentReceivePipe
    {
        // 队列条目消息。只在此处使用。
        // -> byte 数组始终为 4 + MaxMessageSize
        // -> ArraySegment 指示实际的消息内容
        struct Entry
        {
            public int connectionId;
            public EventType eventType;
            public ArraySegment<byte> data;
            public Entry(int connectionId, EventType eventType, ArraySegment<byte> data)
            {
                this.connectionId = connectionId;
                this.eventType = eventType;
                this.data = data;
            }
        }

        // 消息队列
        // ConcurrentQueue 会分配。改用 lock{}。
        //
        // 重要：使用时均需 lock{}
        readonly Queue<Entry> queue = new Queue<Entry>();

        // byte[] 池以避免分配
        // Take & Return 在管道中得到了很好的封装。
        // 外部不需要担心任何事情。
        // 并且它可以很方便地被测试。
        //
        // 重要：使用时均需 lock{}
        Pool<byte[]> pool;

        // 不幸的是，对于高并发测试来说，为每个 connectionId 使用一个接收管道要慢得多。
        // 现在我们为所有连接使用一个管道。
        // => 我们仍然需要限制每个连接排队的消息数量，以防止某个滥用连接造成队列被其消息填满从而拖慢所有人。
        // => 目前我们使用一个简单的每 connectionId 计数器。
        Dictionary<int, int> queueCounter = new Dictionary<int, int>();

        // 构造函数
        public MagnificentReceivePipe(int MaxMessageSize)
        {
            // 初始化池以每次创建最大消息大小的 byte[]
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }

        // 返回此 connectionId 的排队消息数量。
        // 用于统计。不要在调用后假设 Count 保持不变。
        public int Count(int connectionId)
        {
            lock (this)
            {
                return queueCounter.TryGetValue(connectionId, out int count)
                       ? count
                       : 0;
            }
        }

        // 总计数
        public int TotalCount
        {
            get { lock (this) { return queue.Count; } }
        }

        // 池计数，用于测试
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }

        // 入队消息
        // -> 使用 ArraySegment 以避免后续分配
        // -> 参数直接传递，这样更明显我们不会仅仅把传入的 'message' 入队，
        //    而是将其内容复制到 byte[] 并在内部存储，等等。
        public void Enqueue(int connectionId, EventType eventType, ArraySegment<byte> message)
        {
            // 池和队列的使用始终需要锁定
            lock (this)
            {
                // 这条消息是否包含数据数组内容？
                ArraySegment<byte> segment = default;
                if (message != default)
                {
                    // ArraySegment 仅在返回之前有效。
                    // 将其复制到我们可以存储的 byte[] 中。
                    // ArraySegment 的数组只在返回之前有效，因此复制到一个可以安全排队的 byte[] 中。

                    // 先从池中获取一个以避免分配
                    byte[] bytes = pool.Take();

                    // 将其复制进去
                    Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                    // 指示消息所在的部分
                    segment = new ArraySegment<byte>(bytes, 0, message.Count);
                }

                // 将其入队
                // 重要：传递的是段指向的池字节数组，
                //            不是仅在返回之前有效的 'message'！
                Entry entry = new Entry(connectionId, eventType, segment);
                queue.Enqueue(entry);

                // 增加该 connectionId 的计数器
                int oldCount = Count(connectionId);
                queueCounter[connectionId] = oldCount + 1;
            }
        }

        // 查看下一个消息
        // -> 允许调用者在管道仍持有 byte[] 的同时处理它
        // -> 应该在处理后调用 TryDequeue，以便将 byte[] 返回到池中！
        // => 详见 TryDequeue 注释！
        //
        // 重要：TryPeek & Dequeue 必须在同一线程上调用！
        public bool TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> data)
        {
            connectionId = 0;
            eventType = EventType.Disconnected;
            data = default;

            // 汊和队列的使用始终需要锁定
            lock (this)
            {
                if (queue.Count > 0)
                {
                    Entry entry = queue.Peek();
                    connectionId = entry.connectionId;
                    eventType = entry.eventType;
                    data = entry.data;
                    return true;
                }
                return false;
            }
        }

        // 出队下一个消息
        // -> 简单地出队并将 byte[] 返回到池（如果有）
        // -> 使用 Peek 来真正处理第一个元素，同时管道仍持有 byte[]
        // -> 不返回元素，因为出队时需要将 byte[] 返回到池。
        //    不允许调用方在 byte[] 已返回到池时仍然持有它。
        // => Peek & Dequeue 是池化接收管道避免分配的最简单、干净的解决方案！
        //
        // 重要：TryPeek & Dequeue 必须在同一线程上调用！
        public bool TryDequeue()
        {
            // 池和队列的使用始终需要锁定
            lock (this)
            {
                if (queue.Count > 0)
                {
                    // 出队
                    Entry entry = queue.Dequeue();

                    // 将 byte[] 返回到池（如果有）。
                    // 并非所有消息类型都有 byte[] 内容。
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }

                    // 减少该 connectionId 的计数器
                    queueCounter[entry.connectionId]--;

                    // 如果为零则移除。我们不想永远保留旧的 connectionId，
                    // 那会导致内存缓慢增长。
                    if (queueCounter[entry.connectionId] == 0)
                        queueCounter.Remove(entry.connectionId);

                    return true;
                }
                return false;
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
                    // 出队
                    Entry entry = queue.Dequeue();

                    // 将 byte[] 返回到池（如果有）。
                    // 并非所有消息类型都有 byte[] 内容。
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }
                }

                // 清除计数器
                queueCounter.Clear();
            }
        }
    }
}