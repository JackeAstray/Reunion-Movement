using System;

namespace kcp2k
{
    // kcp 处理的可靠消息头。
    // 注意：这不是原始接收消息的头部，因为握手/断开需要可靠传输。
    // 如果把这些放在 rawreceive 上，消息可能丢失且不会重传！
    public enum KcpHeaderReliable : byte
    {
        // 不要对 0x00 做出反应，能帮忙过滤随机噪声。
        Hello      = 1,
        // ping 目前走可靠通道（也可以走不可靠），
        // 两者的区别不大，这里走可靠通道更方便，因为已有可靠消息的 KcpHeader。
        // ping 仅用于保持连接活跃，延迟并不重要。
        Ping       = 2,
        Data       = 3,
    }

    public enum KcpHeaderUnreliable : byte
    {
        // 用户可发送不可靠消息
        Data = 4,
        // 断开使用快速不可靠发送（glenn fielder 方法）
        Disconnect = 5,
    }

    // 提供从/到 byte 的安全转换。
    // 攻击者可能发送无效值，所以 255 等值不能被解析。
    public static class KcpHeader
    {
        public static bool ParseReliable(byte value, out KcpHeaderReliable header)
        {
            if (Enum.IsDefined(typeof(KcpHeaderReliable), value))
            {
                header = (KcpHeaderReliable)value;
                return true;
            }

            header = KcpHeaderReliable.Ping; // 任意默认值
            return false;
        }

        public static bool ParseUnreliable(byte value, out KcpHeaderUnreliable header)
        {
            if (Enum.IsDefined(typeof(KcpHeaderUnreliable), value))
            {
                header = (KcpHeaderUnreliable)value;
                return true;
            }

            header = KcpHeaderUnreliable.Disconnect; // 任意默认值
            return false;
        }
    }
}
