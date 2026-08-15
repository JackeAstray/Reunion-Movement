using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络消息编解码器 —— 定义消息"帧"的边界与消息 ID 格式。
    /// 对接任意服务器时选择匹配的编解码器即可（消息 ID + 负载 / 长度前缀 / 原始透传），
    /// 也可自行实现本接口接入完全自定义的帧协议。
    /// </summary>
    public interface INetworkMessageCodec
    {
        /// <summary>
        /// 是否支持从字节流判定帧边界。
        /// true：流式传输（RawTcp 等无消息边界的字节流），由 NetworkStreamAssembler 累积缓冲分帧；
        /// false：数据报传输（Telepathy / kcp2k / WebSocket 已保证消息边界），每块即一帧。
        /// </summary>
        bool SupportsStreamFraming { get; }

        /// <summary>编码一帧：[帧头（可选）][负载]</summary>
        byte[] Encode(ushort messageId, byte[] payload);

        /// <summary>
        /// 从缓冲区读取完整帧长度（仅流式编解码器需要实现）。
        /// 数据不足返回 false 并保持 buffer 不变；返回 true 时 frameLength 为完整帧总长（含帧头）。
        /// </summary>
        bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength);

        /// <summary>
        /// 解码完整帧：产出消息 ID 与负载段（零拷贝引用 frame，调用方不得长期持有）。
        /// </summary>
        bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload);
    }

    /// <summary>内置编解码器类型（可序列化到 Inspector 配置）</summary>
    public enum NetworkCodecType
    {
        /// <summary>[2 字节消息 ID（小端）][负载] —— 项目默认，兼容既有协议</summary>
        MessageId = 0,
        /// <summary>[4 字节负载长度（小端）][负载] —— 对接通用长度前缀 TCP 服务器</summary>
        LengthPrefixed = 1,
        /// <summary>[4 字节负载长度][2 字节消息 ID][负载] —— 长度前缀 + 消息分发</summary>
        LengthPrefixedWithId = 2,
        /// <summary>原始透传 —— 对接任意自定义协议（消息 ID 恒为 0）</summary>
        Passthrough = 3,
        /// <summary>[2 字节消息 ID][Deflate 压缩负载] —— 带宽敏感场景（大 JSON/状态同步）</summary>
        CompressedMessageId = 4,
        /// <summary>[4 字节长度][2 字节消息 ID][Deflate 压缩负载] —— 长度前缀 + 分发 + 压缩</summary>
        CompressedLengthPrefixedWithId = 5,
    }

    /// <summary>编解码器工厂</summary>
    public static class NetworkCodecFactory
    {
        public static INetworkMessageCodec Create(NetworkCodecType type)
        {
            switch (type)
            {
                case NetworkCodecType.LengthPrefixed:
                    return new LengthPrefixedCodec(includeMessageId: false);
                case NetworkCodecType.LengthPrefixedWithId:
                    return new LengthPrefixedCodec(includeMessageId: true);
                case NetworkCodecType.Passthrough:
                    return PassthroughCodec.Instance;
                case NetworkCodecType.CompressedMessageId:
                    return new CompressedCodec(MessageIdCodec.Instance);
                case NetworkCodecType.CompressedLengthPrefixedWithId:
                    return new CompressedCodec(new LengthPrefixedCodec(includeMessageId: true));
                default:
                    return MessageIdCodec.Instance;
            }
        }
    }
}
