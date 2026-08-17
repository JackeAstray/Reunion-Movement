using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络字节流组装器 —— 将任意分片的字节块重组为完整帧（帧边界由编解码器判定）。
    /// - 数据报模式（编解码器不支持流式分帧）：每块即一帧，直接解码；
    /// - 流模式（如 RawTcp 字节流）：累积缓冲并逐帧提取，半帧保留至下一块到达。
    /// 内置缓冲上限，防止恶意/异常服务器发送超大伪帧撑爆内存。
    /// 注意：回调中的帧段/负载段引用内部缓冲，回调返回后即失效，不得长期持有。
    /// </summary>
    public sealed class NetworkStreamAssembler
    {
        public const int DefaultMaxBufferSize = 1 << 20; // 1MB

        readonly INetworkMessageCodec codec;
        readonly int maxBufferSize;
        byte[] buffer = Array.Empty<byte>();
        int count;
        int lastDecodeFailWarnTicks;

        /// <summary>当前缓冲的半帧字节数（仅流模式使用）</summary>
        public int BufferedBytes => count;

        public NetworkStreamAssembler(INetworkMessageCodec codec, int maxBufferSize = DefaultMaxBufferSize)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.maxBufferSize = maxBufferSize <= 0 ? DefaultMaxBufferSize : maxBufferSize;
        }

        /// <summary>
        /// 喂入一段字节块；每解析出完整帧即回调 onFrame(消息 ID, 帧段, 负载段)。
        /// </summary>
        public void Feed(byte[] chunk, Action<ushort, ArraySegment<byte>, ArraySegment<byte>> onFrame)
        {
            if (chunk == null || chunk.Length == 0) return;
            if (onFrame == null) return;

            if (!codec.SupportsStreamFraming)
            {
                // 数据报模式：传输层已保证消息边界，整块即一帧
                if (codec.TryDecode(chunk, 0, chunk.Length, out var id, out var payload))
                {
                    onFrame(id, new ArraySegment<byte>(chunk, 0, chunk.Length), payload);
                }
                return;
            }

            // 流模式：累积缓冲 → 逐帧提取
            if (chunk.Length > maxBufferSize)
            {
                Log.Warning("[NetworkStreamAssembler] 单块 {0} 字节超过上限 {1}，丢弃", chunk.Length, maxBufferSize);
                return;
            }
            EnsureCapacity(count + chunk.Length);
            Buffer.BlockCopy(chunk, 0, buffer, count, chunk.Length);
            count += chunk.Length;

            int offset = 0;
            while (offset < count)
            {
                if (!codec.TryGetFrameLength(buffer, offset, count - offset, out int frameLength))
                {
                    // 半帧等待。内存 DoS 防护：累积字节已超过上限仍无法提取完整帧，
                    // 说明帧头声明的长度必然超限（恶意端发伪长度头 + 持续灌小块数据），
                    // 此时若继续等待 EnsureCapacity 将无限翻倍直至 OOM，必须立即重置。
                    if (count - offset > maxBufferSize)
                    {
                        Log.Warning("[NetworkStreamAssembler] 声明的帧长度超过缓冲上限 {0}（疑似恶意帧头），重置缓冲", maxBufferSize);
                        count = 0;
                        return;
                    }
                    break; // 半帧：等待后续数据
                }
                if (frameLength <= 0 || frameLength > maxBufferSize)
                {
                    Log.Warning("[NetworkStreamAssembler] 非法帧长 {0}，丢弃缓冲并重置", frameLength);
                    count = 0;
                    return;
                }
                if (codec.TryDecode(buffer, offset, frameLength, out var id, out var payload))
                {
                    onFrame(id, new ArraySegment<byte>(buffer, offset, frameLength), payload);
                }
                else
                {
                    // 低频告警：恶意端持续灌负长度头（LengthPrefixedCodec 每 4 字节返回一次
                    // 非法帧长）时，此处会每 4 字节触发一次，需节流防日志刷屏 DoS
                    int now = Environment.TickCount;
                    if (unchecked(now - lastDecodeFailWarnTicks) > 1000)
                    {
                        lastDecodeFailWarnTicks = now;
                        Log.Warning("[NetworkStreamAssembler] 解码失败，跳过 {0} 字节", frameLength);
                    }
                }
                offset += frameLength;
            }

            if (offset == count)
            {
                count = 0;
                return;
            }
            // 保留剩余半帧到缓冲头部
            int remaining = count - offset;
            if (remaining > maxBufferSize)
            {
                Log.Warning("[NetworkStreamAssembler] 残余缓冲 {0} 字节超过上限，重置", remaining);
                count = 0;
                return;
            }
            Buffer.BlockCopy(buffer, offset, buffer, 0, remaining);
            count = remaining;
        }

        /// <summary>清空缓冲（连接重建/协议切换时调用）</summary>
        public void Reset()
        {
            count = 0;
        }

        void EnsureCapacity(int needed)
        {
            if (buffer.Length >= needed) return;
            int size = buffer.Length == 0 ? 256 : buffer.Length * 2;
            while (size < needed) size *= 2;
            Array.Resize(ref buffer, size);
        }
    }
}
