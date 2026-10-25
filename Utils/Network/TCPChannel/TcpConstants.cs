namespace ReunionMovement.Common.Util
{
    public class TcpConstants
    {
        /// <summary>
        /// Telepathy TCP 单个数据包最大数据量；
        /// 与 NetworkServerConfig/NetworkClientConfig 的 maxAssembledFrameSize（默认 1MB）保持一致，
        /// 避免"组装器允许 1MB 帧但 Telepathy 只收 16KB"导致的发送线程异常/静默丢帧。
        /// 注意：每连接接收缓冲为 4 + MaxMessageSize，连接数较多时按需下调并同步修改 maxAssembledFrameSize。
        /// </summary>
        public const int MaxMessageSize = 1 << 20;//1024*1024
    }
}
