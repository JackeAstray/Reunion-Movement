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
        /// <summary>
        /// 平台约束归一化：WebGL 播放器无线程、无原始套接字，
        /// Tcp（Telepathy）/ Kcp（kcp2k）/ RawTcp（原生 Socket）都无法工作，只有 WebSocket 可用。
        /// 在唯一入口统一回退，避免运行期才以晦涩错误失败（例如线程创建失败、连接永久 pending）。
        /// 注意：编辑器内即使构建目标是 WebGL 也仍用 Mono 运行、线程可用，故只在
        /// "UNITY_WEBGL 且非编辑器"时回退，以免影响编辑器内调试。
        /// </summary>
        private static NetworkTransportType NormalizeForPlatform(NetworkTransportType type)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (type != NetworkTransportType.WebSocket)
            {
                Log.Error("[NetworkChannelFactory] WebGL 不支持传输 {0}（无线程/无原始套接字），已自动回退为 WebSocket；"
                          + "请在配置中显式使用 NetworkTransportType.WebSocket 以消除此告警", type);
                return NetworkTransportType.WebSocket;
            }
#endif
            return type;
        }

        public static INetworkClientChannel CreateClient(NetworkTransportType type, string channelName)
        {
            type = NormalizeForPlatform(type);
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
#if UNITY_WEBGL && !UNITY_EDITOR
            // 浏览器无法监听端口：WebGL 播放器不能作为服务端主机。
            // 这里给出明确诊断（而不是让它以底层"监听失败"的形式报错），随后仍按 WebSocket 构造通道作为尽力而为。
            Log.Error("[NetworkChannelFactory] WebGL 不能作为服务端主机（浏览器无法监听端口 {0}）；"
                      + "请在原生/专用服务器进程上运行 NetworkServer。", port);
#endif
            type = NormalizeForPlatform(type);
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
