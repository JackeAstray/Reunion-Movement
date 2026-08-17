using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 长度前缀编解码器：[4 字节负载长度（小端）][(可选)2 字节消息 ID（小端）][负载]。
    /// 支持流式分帧（SupportsStreamFraming = true），可配合 RawTcp 通道对接任意
    /// "长度前缀"协议的服务器（这是最常见的通用 TCP 帧格式）。
    /// 4 字节长度字段上限 2GB，实际缓冲受 NetworkStreamAssembler 上限保护。
    /// </summary>
    public sealed class LengthPrefixedCodec : INetworkMessageCodec
    {
        public const int LengthFieldSize = sizeof(int);
        public const int MessageIdSize = sizeof(ushort);

        readonly bool includeMessageId;

        /// <summary>帧内是否包含 2 字节消息 ID（false 时消息 ID 恒为 0）</summary>
        public bool IncludeMessageId => includeMessageId;

        int frameHeaderSize => LengthFieldSize + (includeMessageId ? MessageIdSize : 0);

        public LengthPrefixedCodec(bool includeMessageId = true)
        {
            this.includeMessageId = includeMessageId;
        }

        public bool SupportsStreamFraming => true;

        public byte[] Encode(ushort messageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var frame = new byte[frameHeaderSize + payload.Length];
            WriteInt32LE(frame, 0, payload.Length);
            if (includeMessageId)
            {
                frame[LengthFieldSize] = (byte)(messageId & 0xFF);
                frame[LengthFieldSize + 1] = (byte)((messageId >> 8) & 0xFF);
            }
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, frameHeaderSize, payload.Length);
            }
            return frame;
        }

        public bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength)
        {
            frameLength = 0;
            if (buffer == null || count < LengthFieldSize) return false; // 连长度字段都不完整，继续等待
            int payloadLen = ReadInt32LE(buffer, offset);
            if (payloadLen < 0)
            {
                // 非法长度：消费长度字段本身，交由 TryDecode 失败丢弃
                frameLength = LengthFieldSize;
                return true;
            }
            long total = (long)frameHeaderSize + payloadLen;
            if (count < total) return false; // 帧未完整到达，继续等待
            frameLength = (int)total;
            return true;
        }

        public bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload)
        {
            messageId = 0;
            payload = default;
            if (frame == null || length < LengthFieldSize) return false;
            int payloadLen = ReadInt32LE(frame, offset);
            if (payloadLen < 0 || (long)frameHeaderSize + payloadLen > length) return false;
            if (includeMessageId)
            {
                messageId = (ushort)(frame[offset + LengthFieldSize] | (frame[offset + LengthFieldSize + 1] << 8));
            }
            payload = new ArraySegment<byte>(frame, offset + frameHeaderSize, payloadLen);
            return true;
        }

        static void WriteInt32LE(byte[] dst, int pos, int value)
        {
            dst[pos] = (byte)(value & 0xFF);
            dst[pos + 1] = (byte)((value >> 8) & 0xFF);
            dst[pos + 2] = (byte)((value >> 16) & 0xFF);
            dst[pos + 3] = (byte)((value >> 24) & 0xFF);
        }

        static int ReadInt32LE(byte[] src, int pos)
        {
            return src[pos] | (src[pos + 1] << 8) | (src[pos + 2] << 16) | (src[pos + 3] << 24);
        }
    }
}
