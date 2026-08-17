using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 轻量网络消息编解码：[2 字节消息 ID（小端）][负载]。
    /// 传输层（Telepathy / kcp2k / SimpleWebTransport）已保证消息边界与可靠性，
    /// 因此帧内无需长度前缀；消息 ID 用于上层协议分发（配合 NetworkMessageDispatcher）。
    /// 用法：
    ///   channel.SendMessage(NetworkMessageCodec.Encode(1, payload));
    ///   if (NetworkMessageCodec.TryDecodeId(frame, out var id)) { ... }
    /// </summary>
    public static class NetworkMessageCodec
    {
        /// <summary>帧头大小（2 字节消息 ID）</summary>
        public const int HeaderSize = sizeof(ushort);

        /// <summary>组装消息帧：[2 字节 ID][负载]</summary>
        public static byte[] Encode(ushort messageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var frame = new byte[HeaderSize + payload.Length];
            frame[0] = (byte)(messageId & 0xFF);
            frame[1] = (byte)((messageId >> 8) & 0xFF);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, HeaderSize, payload.Length);
            }
            return frame;
        }

        /// <summary>解析帧头消息 ID（不复制负载）</summary>
        public static bool TryDecodeId(byte[] frame, out ushort messageId)
        {
            messageId = 0;
            if (frame == null || frame.Length < HeaderSize) return false;
            messageId = (ushort)(frame[0] | (frame[1] << 8));
            return true;
        }

        /// <summary>解析完整帧（负载以 ArraySegment 零拷贝返回）</summary>
        public static bool TryDecode(byte[] frame, out ushort messageId, out ArraySegment<byte> payload)
        {
            if (!TryDecodeId(frame, out messageId))
            {
                payload = default;
                return false;
            }
            payload = new ArraySegment<byte>(frame, HeaderSize, frame.Length - HeaderSize);
            return true;
        }
    }
}
