using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// [2 字节消息 ID（小端）][负载] 编解码器（数据报模式）。
    /// 依赖传输层消息边界（Telepathy / kcp2k / SimpleWebTransport 均保证），
    /// 帧内无需长度前缀；消息 ID 用于上层协议分发（配合 NetworkMessageDispatcher）。
    /// 与静态类 NetworkMessageCodec 的线上格式完全一致（默认配置，兼容既有协议）。
    /// </summary>
    public sealed class MessageIdCodec : INetworkMessageCodec
    {
        public static readonly MessageIdCodec Instance = new MessageIdCodec();

        public bool SupportsStreamFraming => false;

        public byte[] Encode(ushort messageId, byte[] payload)
            => NetworkMessageCodec.Encode(messageId, payload);

        public bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength)
        {
            frameLength = 0;
            return false; // 数据报模式：帧边界由传输层保证，不支持流式分帧
        }

        public bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload)
        {
            messageId = 0;
            payload = default;
            if (frame == null || length < NetworkMessageCodec.HeaderSize) return false;
            messageId = (ushort)(frame[offset] | (frame[offset + 1] << 8));
            payload = new ArraySegment<byte>(frame, offset + NetworkMessageCodec.HeaderSize, length - NetworkMessageCodec.HeaderSize);
            return true;
        }
    }
}
