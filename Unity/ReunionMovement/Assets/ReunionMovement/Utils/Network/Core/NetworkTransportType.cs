namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络传输类型 —— 统一抽象，由 NetworkChannelFactory 创建对应通道实现。
    /// </summary>
    public enum NetworkTransportType
    {
        /// <summary>TCP（Telepathy，自带 4 字节长度帧，适合与本框架两端互连）</summary>
        Tcp = 0,
        /// <summary>KCP（kcp2k，可靠 UDP，弱网表现好）</summary>
        Kcp = 1,
        /// <summary>WebSocket（SimpleWebTransport，浏览器/网关友好，支持 ws:// 与 wss://）</summary>
        WebSocket = 2,
        /// <summary>原始 TCP 字节流（无内建帧协议，配合编解码器可对接任意 TCP 服务器）</summary>
        RawTcp = 3,
    }
}
