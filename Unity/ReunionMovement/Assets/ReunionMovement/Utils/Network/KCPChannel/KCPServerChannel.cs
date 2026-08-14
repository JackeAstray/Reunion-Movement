using System;
using kcp2k;
using ReunionMovement.Common;

namespace ReunionMovement.Common.Util
{
    //================================================
    /*
    *1、ServerChannel启动后，接收并维护remote进入的连接;
    *
    *2、当有请求进入并成功建立连接时，触发OnConnected，分发参数分别为
    *NetworkChannelKey以及建立连接的conv;
    *
    *3、当请求断开连接，触发OnDisconnected，分发NetworkChannelKey以及
    *断开连接的conv;
    *
    *4、已连接对象发来数据时，触发OnDataReceived，分发NetworkChannelKey
    *以及发送来数据的conv;
    */
    //================================================
    /// <summary>
    /// / KCP服务端通道；
    /// </summary>
    public class KcpServerChannel : INetworkServerChannel
    {
        KcpServerEndPoint server;

        Action<int, string> onConnected;
        Action<int> onDisconnected;
        Action<int, byte[]> onDataReceived;
        Action<int, string> onError;
        public event Action<int, string> OnConnected
        {
            add { onConnected += value; }
            remove { onConnected -= value; }
        }
        public event Action<int> OnDisconnected
        {
            add { onDisconnected += value; }
            remove { onDisconnected -= value; }
        }
        public event Action<int, byte[]> OnDataReceived
        {
            add { onDataReceived += value; }
            remove { onDataReceived -= value; }
        }
        public event Action<int, string> OnError
        {
            add { onError += value; }
            remove { onError -= value; }
        }

        public int Port { get; private set; }

        public bool Active { get { return server.IsActive(); } }

        public string ChannelName { get; set; }

        public string Host { get { return server.IPAddress; } }

        /// <summary>
        /// 生产默认 KCP 配置：使用 kcp2k 构造函数的生产默认值
        /// （Interval:10ms / Timeout:10000ms / 默认窗口 WND_SND·WND_RCV / MaxRetransmits:DEADLINK），
        /// 仅覆盖 NoDelay 与 DualMode。原测试参数（Interval:1、Timeout:2000、窗口 ×1000，
        /// 注释标注 "测试运行得更快"）会导致毫秒级高频轮询与 2 秒误断死链，不适用于生产网络。
        /// 每次创建通道时新建实例，避免多通道共享可变配置。
        /// </summary>
        protected static KcpConfig CreateDefaultConfig() => new KcpConfig(
            NoDelay: true,   // 低延迟推荐开启
            DualMode: false  // 部分平台不支持 IPv4+IPv6 双栈
        );

        public KcpServerChannel(string channelName, ushort port)
        {
            this.ChannelName = channelName;
            // 将 kcp2k 日志转发到我们的 Log 帮助类
            kcp2k.Log.Info = (s) => Log.Info(s);
            kcp2k.Log.Warning = (s) => Log.Warning(s);
            kcp2k.Log.Error = (s) => Log.Error(s);
            this.Port = port;
            server = new KcpServerEndPoint(
                (connectionId, ipEndPoint) => onConnected?.Invoke(connectionId, ipEndPoint.ToString()),
                OnReceiveDataHandler,
                (connectionId) => onDisconnected?.Invoke(connectionId),
                OnErrorHandler,
                CreateDefaultConfig()
            );
        }

        public bool Start()
        {
            if (Active)
                return false;
            try
            {
                // kcp2k KcpServer.Start 返回 void，绑定失败会抛 SocketException，必须捕获
                server.Start((ushort)Port);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("KCP 服务端启动失败（端口可能被占用）: {0}", ex.Message);
                onError?.Invoke(-1, $"KCP服务端启动失败: {ex.Message}");
                return false;
            }
        }

        public void TickRefresh()
        {
            server.Tick();
        }

        public bool Disconnect(int connectionId)
        {
            server.Disconnect(connectionId);
            return true;
        }

        public bool SendMessage(int connectionId, byte[] data)
        {
            return SendMessage(KcpReliableType.Reliable, connectionId, data);
        }
        public bool SendMessage(KcpReliableType reliableType, int connectionId, byte[] data)
        {
            var segment = new ArraySegment<byte>(data);
            var byteType = (byte)reliableType;
            var channelId = (KcpChannel)byteType;
            switch (channelId)
            {
                case KcpChannel.Unreliable:
                    server.Send(connectionId, segment, KcpChannel.Unreliable);
                    break;
                default:
                    server.Send(connectionId, segment, KcpChannel.Reliable);
                    break;
            }
            return true;
        }

        public string GetConnectionAddress(int connectionId)
        {
            // 连接不存在时 GetClientEndPoint 返回 null，需判空避免 NRE
            var endPoint = server.GetClientEndPoint(connectionId);
            return endPoint == null ? string.Empty : endPoint.Address.ToString();
        }

        public void Close()
        {
            server.Stop();
            Log.Info("KCP 服务已停止");
            // 清理事件处理器，避免 GC 无法回收订阅者
            onConnected = null;
            onDisconnected = null;
            onDataReceived = null;
            onError = null;
        }
        void OnErrorHandler(int connectionId, ErrorCode error, string reason)
        {
            onError?.Invoke(connectionId, $"{error}-{reason}");
        }
        void OnReceiveDataHandler(int conv, ArraySegment<byte> arrSeg, KcpChannel Channel)
        {
            var rcvLen = arrSeg.Count;
            var rcvData = new byte[rcvLen];
            Array.Copy(arrSeg.Array, arrSeg.Offset, rcvData, 0, rcvLen);
            onDataReceived?.Invoke(conv, rcvData);
        }
    }
}