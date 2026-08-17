using System;
using System.Buffers;
using ReunionMovement.Telepathy;

namespace ReunionMovement.Common.Util
{
    public class TcpClientChannel : INetworkClientChannel
    {
        Client client;
        Action onAbort;
        bool everConnected;

        public string ChannelName { get; set; }

        public bool IsConnect { get { return client.Connected; } }
        public bool IsOpen { get { return client.Connected; } }
        public event Action OnAbort
        {
            add { onAbort += value; }
            remove { onAbort -= value; }
        }
        public event Action<string> OnError;
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
            ReunionMovement.Telepathy.Log.Info = (s) => Log.Info(s);
            ReunionMovement.Telepathy.Log.Warning = (s) => Log.Warning(s);
            ReunionMovement.Telepathy.Log.Error = (s) => Log.Error(s);
            // 内部跟踪：连接成功标记；从未成功连接即断开 → 视为连接失败并上报错误
            client.OnConnected += () => everConnected = true;
            client.OnDisconnected += () =>
            {
                if (!everConnected)
                {
                    OnError?.Invoke("TCP 连接失败");
                }
            };
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
            // 先清空错误订阅：Telepathy 的 Disconnect 会同步触发 OnDisconnected，
            // 若从未成功连接，内部跟踪会误报"连接失败"——主动断开不应产生该误报
            OnError = null;
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
        /// 接收数据回调 —— 使用 ArrayPool&lt;byte&gt; 池化缓冲区用于内部拷贝，
        /// 然后复制到精确大小的数组后再分发给订阅者，避免 Use-After-Free。
        /// ⚠️ 消费者（OnDataReceived 订阅者）持有 data 引用安全，
        /// 数据已是独立副本。
        /// </summary>
        void OnReceiveDataHandler(ArraySegment<byte> arrSeg)
        {
            int length = arrSeg.Count;
            if (length == 0) return;

            // 直接从源段复制到精确大小的结果数组（订阅者可安全持有引用）。
            // 移除多余的 ArrayPool 双重拷贝：池化缓冲区复制后即弃，纯属浪费。
            byte[] result = new byte[length];
            Buffer.BlockCopy(arrSeg.Array, arrSeg.Offset, result, 0, length);
            onDataReceived?.Invoke(result);
        }
    }
}
