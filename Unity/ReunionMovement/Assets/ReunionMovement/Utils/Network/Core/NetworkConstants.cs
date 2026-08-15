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

        /// <summary>心跳 PING 帧消息 ID（客户端→服务端，保留，勿用于业务消息）</summary>
        public const ushort ReservedPingMessageId = 0xFFFD;

        /// <summary>心跳 PONG 帧消息 ID（服务端→客户端，保留，勿用于业务消息）</summary>
        public const ushort ReservedPongMessageId = 0xFFFC;

        /// <summary>可靠消息/ACK 帧消息 ID（保留）：客户端→服务端承载 [seq][原消息 ID][负载]，服务端→客户端承载 [seq] 确认</summary>
        public const ushort ReservedAckMessageId = 0xFFFB;

        /// <summary>是否保留系统消息 ID（ACK/PING/PONG/RPC 帧），业务注册前应拦截</summary>
        public static bool IsReservedMessageId(ushort messageId)
        {
            return messageId >= ReservedAckMessageId; // 0xFFFB~0xFFFF 全部保留
        }
    }
}
