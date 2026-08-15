namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 通道工厂 —— 按传输类型创建客户端/服务端通道，
    /// 将 TCP（Telepathy）/ KCP（kcp2k）/ WebSocket（SimpleWebTransport）/ RawTcp（原生 Socket）
    /// 四种传输统一到 INetworkClientChannel / INetworkServerChannel 接口。
    /// 扩展新传输：新增 NetworkTransportType 枚举值 + 在此注册对应实现。
    /// </summary>
    public static class NetworkChannelFactory
    {
        public static INetworkClientChannel CreateClient(NetworkTransportType type, string channelName)
        {
            switch (type)
            {
                case NetworkTransportType.Kcp:
                    return new KcpClientChannel(channelName);
                case NetworkTransportType.WebSocket:
                    return new WebSocketClientChannel(channelName);
                case NetworkTransportType.RawTcp:
                    return new RawTcpClientChannel(channelName);
                default:
                    return new TcpClientChannel(channelName);
            }
        }

        public static INetworkServerChannel CreateServer(NetworkTransportType type, string channelName, int port)
        {
            // 端口校验：KCP 通道将端口强转 ushort，>65535 或负数会被静默截断到错误端口监听，
            // 排查极其困难，这里统一提前告警
            if (port <= 0 || port > 65535)
            {
                Log.Error("[NetworkChannelFactory] 端口 {0} 非法（应为 1-65535），可能无法正常监听", port);
            }
            switch (type)
            {
                case NetworkTransportType.Kcp:
                    return new KcpServerChannel(channelName, (ushort)port);
                case NetworkTransportType.WebSocket:
                    return new WebSocketServerChannel(channelName, port);
                case NetworkTransportType.RawTcp:
                    return new RawTcpServerChannel(channelName, port);
                default:
                    return new TcpServerChannel(channelName, port);
            }
        }
    }
}
