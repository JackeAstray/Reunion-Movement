using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// UniversalNetworkBehaviour partial part: Client（基于 NetworkClient 的统一实现）
    /// </summary>
    public partial class UniversalNetworkBehaviour
    {
        #region Client

        /// <summary>
        /// 启动客户端（先停止旧连接，再按当前 Inspector 配置全新启动）。
        /// 自动重连 / 心跳 / 连接超时由底层 NetworkClient 状态机管理。
        /// </summary>
        public void StartClient()
        {
            StopAll();

            var cfg = new NetworkClientConfig
            {
                channelName = channelName,
                transport = ToTransportType(transport),
                host = host,
                port = port,
                codec = codec,
                autoReconnect = autoReconnect,
                maxReconnectAttempts = maxReconnectAttempts,
                reconnectBaseDelay = reconnectInterval,
                reconnectBackoffFactor = 1f, // 与旧版一致的固定间隔重连
                reconnectMaxDelay = reconnectInterval,
                reconnectJitter = 0f,
                enableHeartbeat = enableHeartbeat,
                heartbeatInterval = heartbeatInterval,
                heartbeatText = heartbeatText,
            };

            networkClient = new NetworkClient(cfg);
            networkClient.OnConnected += () =>
            {
                Log.Info("{0} 客户端已连接", transport);
                ClientConnected?.Invoke();
                onClientConnected?.Invoke();
            };
            networkClient.OnDisconnected += () =>
            {
                Log.Info("{0} 客户端已断开连接", transport);
                ClientDisconnected?.Invoke();
                onClientDisconnected?.Invoke();
            };
            networkClient.OnMessage += (messageId, payload) => OnClientDataReceived(payload.ToArray());
            networkClient.OnError += (err) =>
            {
                Log.Warning("{0} 客户端错误: {1}", transport, err);
                ClientError?.Invoke(err);
                try { onClientError?.Invoke(err); } catch (System.Exception ex) { Log.Warning("onClientError 回调异常: {0}", ex.Message); }
            };
            networkClient.Connect();
        }

        /// <summary>
        /// 发送字符串数据到服务器（消息 ID = 0）
        /// </summary>
        public void SendClientString(string text)
        {
            networkClient?.SendString(text);
        }

        /// <summary>
        /// 发送数据到服务器（消息 ID = 0）
        /// </summary>
        public void SendClientBytes(byte[] data)
        {
            networkClient?.Send(data);
        }

        /// <summary>注册客户端消息处理器（按消息 ID，负载段零拷贝，回调内勿长期持有）</summary>
        public void RegisterClientMessageHandler(ushort messageId, Action<ArraySegment<byte>> handler)
        {
            networkClient?.Dispatcher.RegisterHandler(messageId, handler);
        }

        /// <summary>注册强类型对象消息（类型 ↔ 消息 ID 绑定）</summary>
        public void RegisterClientObjectMessage<T>(ushort messageId)
        {
            networkClient?.RegisterObjectMessage<T>(messageId);
        }

        /// <summary>注册强类型对象处理器（先 RegisterClientObjectMessage 绑定 ID）</summary>
        public void RegisterClientObjectHandler<T>(Action<T> handler)
        {
            networkClient?.RegisterObjectHandler(handler);
        }

        /// <summary>发送强类型对象（先 RegisterClientObjectMessage 绑定 ID）</summary>
        public bool SendClientObject<T>(T obj)
        {
            return networkClient != null && networkClient.SendObject(obj);
        }

        /// <summary>
        /// 请求/响应 RPC（服务端需用 RegisterServerRequestHandler 注册同 ID 处理器）。
        /// 超时抛 TimeoutException。
        /// </summary>
        public UniTask<byte[]> ClientRequestAsync(ushort messageId, byte[] payload, float timeoutSeconds = 5f)
        {
            if (networkClient == null)
            {
                return UniTask.FromException<byte[]>(new InvalidOperationException("客户端未启动"));
            }
            return networkClient.RequestAsync(messageId, payload, TimeSpan.FromSeconds(timeoutSeconds));
        }

        /// <summary>
        /// 客户端收到数据时调用，默认在日志中回显；可重写或订阅扩展。
        /// 注意：payload 为按编解码器解码后的负载（不含帧头）。
        /// </summary>
        void OnClientDataReceived(byte[] payload)
        {
            var s = Encoding.UTF8.GetString(payload);
            Log.Info("客户端接收 ({0})：{1}", transport, s);
            ClientDataReceived?.Invoke(payload);
            try { onClientDataReceived?.Invoke(s); } catch (System.Exception ex) { Log.Warning("onClientDataReceived 回调异常: {0}", ex.Message); }
        }
        #endregion
    }
}
