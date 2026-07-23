using System;
using System.Buffers;
using Telepathy;

namespace ReunionMovement.Common.Util
{
    public class TcpClientChannel : INetworkClientChannel
    {
        Client client;
        Action onAbort;

        public string ChannelName { get; set; }

        public bool IsConnect { get { return client.Connected; } }
        public event Action OnAbort
        {
            add { onAbort += value; }
            remove { onAbort -= value; }
        }
        public event Action OnConnected
        {
            add { client.OnConnected += value; }
            remove { client.OnConnected -= value; }
        }
        event Action<byte[]> onDataReceived;
        public event Action<byte[]> OnDataReceived
        {
            add { onDataReceived += value; }
            remove { onDataReceived -= value; }
        }
        public event Action OnDisconnected
        {
            add { client.OnDisconnected += value; }
            remove { client.OnDisconnected -= value; }
        }

        public int Port { get; private set; }

        public string Host { get; private set; }
        public TcpClientChannel(string channelName)
        {
            this.ChannelName = channelName;
            client = new Client(TcpConstants.MaxMessageSize);
            Telepathy.Log.Info = (s) => Log.Info(s);
            Telepathy.Log.Warning = (s) => Log.Warning(s);
            Telepathy.Log.Error = (s) => Log.Error(s);
        }

        public void Connect(string host, int port)
        {
            this.Host = host;
            this.Port = port;
            client.Connect(Host, Port);
            client.OnData = OnReceiveDataHandler;
        }

        public void TickRefresh()
        {
            client.Tick(100);
        }

        public bool SendMessage(byte[] data)
        {
            var segment = new ArraySegment<byte>(data);
            return client.Send(segment);
        }

        public void Disconnect()
        {
            client.Disconnect();
            client.OnData = null;
            // 清理所有事件订阅，防止内存泄漏和重复回调
            client.OnConnected = null;
            client.OnDisconnected = null;
            onDataReceived = null;
        }

        public void Close()
        {
            Disconnect();
            onAbort?.Invoke();
            onAbort = null;
        }
        /// <summary>
        /// 接收数据回调 —— 使用 ArrayPool&lt;byte&gt; 池化缓冲区，消除每次接收的堆分配。
        /// ⚠️ 消费者（OnDataReceived 订阅者）不得持有 data 引用超出回调范围；
        /// 如需持久化数据，请在回调内自行复制。
        /// </summary>
        void OnReceiveDataHandler(ArraySegment<byte> arrSeg)
        {
            int length = arrSeg.Count;
            if (length == 0) return;

            byte[] data = ArrayPool<byte>.Shared.Rent(length);
            Buffer.BlockCopy(arrSeg.Array, arrSeg.Offset, data, 0, length);
            onDataReceived?.Invoke(data);
            ArrayPool<byte>.Shared.Return(data);
        }
    }
}
