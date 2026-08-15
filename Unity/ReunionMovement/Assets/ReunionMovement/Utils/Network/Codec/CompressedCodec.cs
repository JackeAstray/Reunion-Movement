using System;
using System.IO;
using System.IO.Compression;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 压缩编解码器包装器 —— 包装底层编解码器，对负载进行 Deflate 压缩/解压。
    /// 帧格式与底层编解码器一致，仅负载为压缩数据；解压端还原原始负载段（新分配的数组）。
    ///
    /// 适用场景：大 JSON / 状态同步等带宽敏感负载（可省 50%~90% 流量）。
    /// 注意：小负载（几十字节）压缩收益低甚至略增体积，建议按业务评估后整链路启用。
    /// 压缩/解压均为零依赖（System.IO.Compression.DeflateStream），跨平台一致。
    /// </summary>
    public sealed class CompressedCodec : INetworkMessageCodec
    {
        /// <summary>解压输出上限（防解压炸弹：小压缩数据解压出巨大数据撑爆内存）</summary>
        public const int MaxDecompressedSize = 16 * 1024 * 1024; // 16MB

        readonly INetworkMessageCodec inner;

        public bool SupportsStreamFraming => inner.SupportsStreamFraming;

        public CompressedCodec(INetworkMessageCodec inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public byte[] Encode(ushort messageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var compressed = DeflateCompress(payload);
            return inner.Encode(messageId, compressed);
        }

        public bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength)
        {
            return inner.TryGetFrameLength(buffer, offset, count, out frameLength);
        }

        public bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload)
        {
            if (!inner.TryDecode(frame, offset, length, out messageId, out var compressed))
            {
                payload = default;
                return false;
            }
            try
            {
                payload = new ArraySegment<byte>(DeflateDecompress(compressed));
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[CompressedCodec] 负载解压失败（数据损坏或超过上限）: {0}", ex.Message);
                payload = default;
                return false;
            }
        }

        static byte[] DeflateCompress(byte[] data)
        {
            using var output = new MemoryStream(data.Length / 2 + 64);
            using (var ds = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                ds.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        /// <summary>解压读取缓冲（线程静态复用：ReceiveLoop 在通道线程、assembler 在主线程，避免共享缓冲跨线程污染）</summary>
        [ThreadStatic]
        static byte[] s_DecompressBuffer;

        static byte[] DeflateDecompress(ArraySegment<byte> data)
        {
            using var input = new MemoryStream(data.Array, data.Offset, data.Count);
            using var ds = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(Math.Min(data.Count * 3, MaxDecompressedSize));
            var buffer = s_DecompressBuffer ?? (s_DecompressBuffer = new byte[8192]);
            int read;
            while ((read = ds.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxDecompressedSize)
                {
                    throw new InvalidDataException($"解压数据超过上限 {MaxDecompressedSize} 字节");
                }
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
    }
}
