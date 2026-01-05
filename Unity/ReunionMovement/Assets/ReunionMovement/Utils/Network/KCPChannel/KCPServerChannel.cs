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

        protected KcpConfig config = new KcpConfig(
            // 强制使用 NoDelay 并设置最小间隔。
            // 这样 UpdateSeveralTimes() 不需要等待很长时间，
            // 测试运行得更快。
            NoDelay: true,
            // 并非所有平台都支持 DualMode。
            // 不启用它以便在所有平台上都能运行测试。
            DualMode: false,
            Interval: 1, // 1 毫秒，确保定期间隔代码至少会运行。
            Timeout: 2000,

            // 增大窗口大小使大型消息可以用很少的刷新调用被发送，
            // 否则测试会花费太长时间。
            SendWindowSize: Kcp.WND_SND * 1000,
            ReceiveWindowSize: Kcp.WND_RCV * 1000,

            // 拥塞窗口会严重限制发送/接收窗口大小，
            // 发送最大长度消息将需要成千上万次更新。
            CongestionWindow: false,

            // 最大重传尝试次数，超过则被判定为 dead_link。
            // 将默认值乘以 2 来检查配置是否正常工作。
            MaxRetransmits: Kcp.DEADLINK * 2
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
                config
            );
        }

        public bool Start()
        {
            if (Active)
                return false;
            server.Start((ushort)Port);
            return true;
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
            return server.GetClientEndPoint(connectionId).Address.ToString();
        }

        public void Close()
        {
            server.Stop();
            Log.Info("KCP 服务已停止");
        }
        void OnErrorHandler(int connectionId, ErrorCode error, string reason)
        {
            onError?.Invoke(connectionId, $"{error}-{reason}");
        }
        void OnReceiveDataHandler(int conv, ArraySegment<byte> arrSeg, KcpChannel Channel)
        {
            var rcvLen = arrSeg.Count;
            var rcvData = new byte[rcvLen];
            Array.Copy(arrSeg.Array, 1, rcvData, 0, rcvLen);
            onDataReceived?.Invoke(conv, rcvData);
        }
    }
}