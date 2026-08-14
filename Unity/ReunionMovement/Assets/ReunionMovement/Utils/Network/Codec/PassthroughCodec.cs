using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 原始透传编解码器：帧即负载，消息 ID 恒为 0，零开销零拷贝。
    /// 用于对接任意自定义协议（协议解析完全由上层自行完成），
    /// 或仅在两端都是本框架组件且不需要消息分发的场景。
    /// </summary>
    public sealed class PassthroughCodec : INetworkMessageCodec
    {
        public static readonly PassthroughCodec Instance = new PassthroughCodec();

        public bool SupportsStreamFraming => false;

        public byte[] Encode(ushort messageId, byte[] payload) => payload ?? Array.Empty<byte>();

        public bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength)
        {
            frameLength = 0;
            return false;
        }

        public bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload)
        {
            messageId = 0;
            payload = default;
            if (frame == null || length < 0) return false;
            payload = new ArraySegment<byte>(frame, offset, length);
            return true;
        }
    }
}
