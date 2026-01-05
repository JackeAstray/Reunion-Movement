using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public interface IBufferOwner
    {
        void Return(ArrayBuffer buffer);
    }

    public sealed class ArrayBuffer : IDisposable
    {
        readonly IBufferOwner owner;

        public readonly byte[] array;

        /// <summary>
        /// 写入到缓冲区的字节数
        /// </summary>
        public int count { get; internal set; }

        /// <summary>
        /// 需要调用 Release 的次数，达到该次数后缓冲区会返回到池中
        /// <para>这允许缓冲区在多个地方同时使用</para>
        /// </summary>
        public void SetReleasesRequired(int required)
        {
            releasesRequired = required;
        }

        /// <summary>
        /// 需要调用 Release 的次数，达到该次数后缓冲区会返回到池中
        /// <para>通常为 0，但可以更改以要求多次调用 Release</para>
        /// </summary>
        /// <remarks>
        /// 此值通常为 0，但可以更改以要求多次调用 Release
        /// </remarks>
        int releasesRequired;

        public ArrayBuffer(IBufferOwner owner, int size)
        {
            this.owner = owner;
            array = new byte[size];
        }

        public void Release()
        {
            int newValue = Interlocked.Decrement(ref releasesRequired);
            if (newValue <= 0)
            {
                count = 0;
                owner?.Return(this);
            }
        }
        public void Dispose()
        {
            Release();
        }

        public void CopyTo(byte[] target, int offset)
        {
            if (count > (target.Length + offset))
                throw new ArgumentException($"{nameof(count)} was greater than {nameof(target)}.length", nameof(target));

            Buffer.BlockCopy(array, 0, target, offset, count);
        }

        public void CopyFrom(ArraySegment<byte> segment)
        {
            CopyFrom(segment.Array, segment.Offset, segment.Count);
        }

        public void CopyFrom(byte[] source, int offset, int length)
        {
            if (length > array.Length)
                throw new ArgumentException($"{nameof(length)} was greater than {nameof(array)}.length", nameof(length));

            count = length;
            Buffer.BlockCopy(source, offset, array, 0, length);
        }

        public void CopyFrom(IntPtr bufferPtr, int length)
        {
            if (length > array.Length)
                throw new ArgumentException($"{nameof(length)} was greater than {nameof(array)}.length", nameof(length));

            count = length;
            Marshal.Copy(bufferPtr, array, 0, length);
        }

        public ArraySegment<byte> ToSegment() => new ArraySegment<byte>(array, 0, count);

        [Conditional("UNITY_ASSERTIONS")]
        internal void Validate(int arraySize)
        {
            if (array.Length != arraySize)
                Log.Error("[SWT-ArrayBuffer]: 返回的缓冲区尺寸不正确");
        }
    }

    internal class BufferBucket : IBufferOwner
    {
        public readonly int arraySize;
        readonly ConcurrentQueue<ArrayBuffer> buffers;

        /// <summary>
        /// 跟踪已创建但未返回的数组数量
        /// </summary>
        internal int _current = 0;

        public BufferBucket(int arraySize)
        {
            this.arraySize = arraySize;
            buffers = new ConcurrentQueue<ArrayBuffer>();
        }

        public ArrayBuffer Take()
        {
            IncrementCreated();
            if (buffers.TryDequeue(out ArrayBuffer buffer))
                return buffer;
            else
            {
                Log.Flood($"[SWT-BufferBucket]: 缓冲池({arraySize}) 创建新缓冲区");
                return new ArrayBuffer(this, arraySize);
            }
        }

        public void Return(ArrayBuffer buffer)
        {
            DecrementCreated();
            buffer.Validate(arraySize);
            buffers.Enqueue(buffer);
        }

        [Conditional("DEBUG")]
        void IncrementCreated()
        {
            int next = Interlocked.Increment(ref _current);
            Log.Flood($"[SWT-BufferBucket]: 缓冲池({arraySize}) 数量:{next}");
        }

        [Conditional("DEBUG")]
        void DecrementCreated()
        {
            int next = Interlocked.Decrement(ref _current);
            Log.Flood($"[SWT-BufferBucket]: 缓冲池({arraySize}) 数量:{next}");
        }
    }

    /// <summary>
    /// 不同尺寸缓冲区的集合
    /// </summary>
    /// <remarks>
    /// <para>
    /// 问题: <br/>
    ///     * 需要缓存 byte[]，以避免每次都创建新的数组 <br/>
    ///     * 发送的数组大小各不相同 <br/>
    ///     * 某些消息可能很大，需要能够覆盖该大小的缓冲区 <br/>
    ///     * 大多数消息相对于最大消息大小都较小 <br/>
    /// </para>
    /// <br/>
    /// <para>
    /// 解决方案: <br/>
    ///     * 创建多个覆盖允许范围的缓冲区组 <br/>
    ///     * 使用对数刻度（math.log）按指数方式拆分范围，使小缓冲区有更多的组 <br/>
    /// </para>
    /// </remarks>
    public class BufferPool
    {
        internal readonly BufferBucket[] buckets;
        readonly int bucketCount;
        readonly int smallest;
        readonly int largest;

        public BufferPool(int bucketCount, int smallest, int largest)
        {
            if (bucketCount < 2) throw new ArgumentException("Count must be at least 2");
            if (smallest < 1) throw new ArgumentException("Smallest must be at least 1");
            if (largest < smallest) throw new ArgumentException("Largest must be greater than smallest");

            this.bucketCount = bucketCount;
            this.smallest = smallest;
            this.largest = largest;

            // 在对数刻度上拆分范围（为较小的大小保留更多的桶）
            double minLog = Math.Log(this.smallest);
            double maxLog = Math.Log(this.largest);
            double range = maxLog - minLog;
            double each = range / (bucketCount - 1);

            buckets = new BufferBucket[bucketCount];

            for (int i = 0; i < bucketCount; i++)
            {
                double size = smallest * Math.Pow(Math.E, each * i);
                buckets[i] = new BufferBucket((int)Math.Ceiling(size));
            }

            Validate();

            // 示例
            // 5         count
            // 20        smallest
            // 16400     largest

            // 3.0       log 20
            // 9.7       log 16400

            // 6.7       range 9.7 - 3
            // 1.675     each  6.7 / (5-1)

            // 20        e^ (3 + 1.675 * 0)
            // 107       e^ (3 + 1.675 * 1)
            // 572       e^ (3 + 1.675 * 2)
            // 3056      e^ (3 + 1.675 * 3)
            // 16,317    e^ (3 + 1.675 * 4)

            // 使用 double 不会丢失精度
        }

        [Conditional("UNITY_ASSERTIONS")]
        void Validate()
        {
            if (buckets[0].arraySize != smallest)
                Log.Error("[SWT-BufferPool]: BufferPool 未能为最小值创建桶. bucket:{0} smallest:{1}", buckets[0].arraySize, smallest);

            int largestBucket = buckets[bucketCount - 1].arraySize;
            // 使用 Ceiling 进行四舍五入，因此允许比 largest 多 1
            if (largestBucket != largest && largestBucket != largest + 1)
                Log.Error("[SWT-BufferPool]: BufferPool 未能为最大值创建桶. bucket:{0} largest:{1}", largestBucket, largest);
        }

        public ArrayBuffer Take(int size)
        {
            if (size > largest)
                throw new ArgumentException($"Size ({size}) is greater than largest ({largest})");

            for (int i = 0; i < bucketCount; i++)
                if (size <= buckets[i].arraySize)
                    return buckets[i].Take();

            throw new ArgumentException($"Size ({size}) is greater than largest ({largest})");
        }
    }
}
