using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Mirror.SimpleWeb
{
    public enum ClientState
    {
        NotConnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
    }

    /// <summary>
    /// 用于控制 WebSocket 的客户端
    /// <para>WebSocketClientWebGl 和 WebSocketClientStandAlone 的基类</para>
    /// </summary>
    public abstract class SimpleWebClient
    {
        readonly int maxMessagesPerTick;

        protected ClientState state;
        protected readonly int maxMessageSize;
        protected readonly BufferPool bufferPool;

        public readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

        public ClientState ConnectionState => state;

        public event Action onConnect;
        public event Action onDisconnect;
        public event Action<ArraySegment<byte>> onData;
        public event Action<Exception> onError;

        public abstract void Connect(Uri serverAddress);
        public abstract void Disconnect();
        public abstract void Send(ArraySegment<byte> segment);

        public static SimpleWebClient Create(int maxMessageSize, int maxMessagesPerTick, TcpConfig tcpConfig)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebSocketClientWebGl(maxMessageSize, maxMessagesPerTick);
#else
            return new WebSocketClientStandAlone(maxMessageSize, maxMessagesPerTick, tcpConfig);
#endif
        }

        protected SimpleWebClient(int maxMessageSize, int maxMessagesPerTick)
        {
            this.maxMessageSize = maxMessageSize;
            this.maxMessagesPerTick = maxMessagesPerTick;
            bufferPool = new BufferPool(5, 20, maxMessageSize);
        }

        /// <summary>
        /// 处理所有新的消息
        /// </summary>
        public void ProcessMessageQueue()
        {
            ProcessMessageQueue(null);
        }

        /// <summary>
        /// 仅在指定的 <paramref name="behaviour"/> 启用时处理消息队列中的消息
        /// </summary>
        /// <param name="behaviour">用于检查启用状态的 MonoBehaviour（为 null 则忽略检查）</param>
        public void ProcessMessageQueue(MonoBehaviour behaviour)
        {
            int processedCount = 0;
            bool skipEnabled = behaviour == null;
            // 每次循环都检查 enabled，以防 behaviour 在收到数据后被禁用
            while (
                (skipEnabled || behaviour.enabled) &&
                processedCount < maxMessagesPerTick &&
                // 从队列中弹出一条
                receiveQueue.TryDequeue(out Message next)
                )
            {
                processedCount++;

                switch (next.type)
                {
                    case EventType.Connected:
                        onConnect?.Invoke();
                        break;
                    case EventType.Data:
                        onData?.Invoke(next.data.ToSegment());
                        next.data.Release();
                        break;
                    case EventType.Disconnected:
                        onDisconnect?.Invoke();
                        break;
                    case EventType.Error:
                        onError?.Invoke(next.exception);
                        break;
                }
            }
            if (receiveQueue.Count > 0)
                Log.Warn("[SWT-SimpleWebClient]: ProcessMessageQueue 剩余 {0} 条消息。", receiveQueue.Count);
        }
    }
}
