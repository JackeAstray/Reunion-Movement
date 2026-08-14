namespace ReunionMovement.Common.Util
{
    /// <summary>网络层公共常量</summary>
    public static class NetworkConstants
    {
        /// <summary>未注册业务消息时使用的默认消息 ID（Send(payload) 等便捷 API）</summary>
        public const ushort DefaultMessageId = 0;

        /// <summary>RPC 请求帧消息 ID（保留，勿用于业务消息）</summary>
        public const ushort ReservedRequestMessageId = 0xFFFE;

        /// <summary>RPC 响应帧消息 ID（保留，勿用于业务消息）</summary>
        public const ushort ReservedResponseMessageId = 0xFFFF;
    }
}
