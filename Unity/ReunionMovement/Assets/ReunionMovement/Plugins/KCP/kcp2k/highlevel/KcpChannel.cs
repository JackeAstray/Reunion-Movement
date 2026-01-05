namespace kcp2k
{
    // 原始消息的通道类型和头部
    public enum KcpChannel : byte
    {
        // 不要对 0x00 做出反应，能帮助过滤随机噪声。
        Reliable   = 1,
        Unreliable = 2
    }
}