using System.Text;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// 永远不应更改的常量值
    /// <para>
    /// 一些值来自 https://tools.ietf.org/html/rfc6455
    /// </para>
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// 头部最多为 4 字节
        /// <para>
        /// 如果消息小于 125，则头为 2 字节，否则头为 4 字节
        /// </para>
        /// </summary>
        public const int HeaderSize = 4;

        /// <summary>
        /// 头部的最小尺寸
        /// <para>
        /// 如果消息小于 125，则头为 2 字节，否则头为 4 字节
        /// </para>
        /// </summary>
        public const int HeaderMinSize = 2;

        /// <summary>
        /// 短长度使用的字节数
        /// </summary>
        public const int ShortLength = 2;

        /// <summary>
        /// 长长度使用的字节数
        /// </summary>
        public const int LongLength = 8;

        /// <summary>
        /// 消息掩码始终为 4 字节
        /// </summary>
        public const int MaskSize = 4;

        /// <summary>
        /// 当长度为 1 字节时消息的最大大小
        /// <para>
        /// 有效负载长度在 0-125 之间
        /// </para>
        /// </summary>
        public const int BytePayloadLength = 125;

        /// <summary>
        /// 如果负载长度为 126，则接下来的 2 个字节是长度
        /// </summary>
        public const int UshortPayloadLength = 126;

        /// <summary>
        /// 如果负载长度为 127，则接下来的 8 个字节是长度
        /// </summary>
        public const int UlongPayloadLength = 127;

        /// <summary>
        /// 用于 WebSocket 协议的 GUID
        /// </summary>
        public const string HandshakeGUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static readonly int HandshakeGUIDLength = HandshakeGUID.Length;

        public static readonly byte[] HandshakeGUIDBytes = Encoding.ASCII.GetBytes(HandshakeGUID);

        /// <summary>
        /// 握手消息以 \r\n\r\n 结尾
        /// </summary>
        public static readonly byte[] endOfHandshake = new byte[4] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
    }
}
