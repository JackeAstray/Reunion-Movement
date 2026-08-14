using System;
using System.Text;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// UniversalNetworkBehaviour partial part: Server（基于 NetworkServer 的统一实现）
    /// </summary>
    public partial class UniversalNetworkBehaviour
    {
        #region Server

        /// <summary>
        /// 启动服务器（先停止旧服务，再按当前 Inspector 配置全新监听）。
        /// 启动失败（如端口占用）会派发 ServerError(-1) 事件。
        /// </summary>
        public void StartServer()
        {
            StopAll();

            var cfg = new NetworkServerConfig
            {
                channelName = channelName,
                transport = ToTransportType(transport),
                port = port,
                codec = codec,
            };

            networkServer = new NetworkServer(cfg);
            networkServer.OnStarted += () =>
            {
                ServerStarted?.Invoke();
                onServerStarted?.Invoke();
            };
            networkServer.OnClientConnected += (id, ip) =>
            {
                clientIds.Add(id);
                Log.Info("{0} 客户端已连接 id={1} ip={2}", transport, id, ip);
                ServerClientConnected?.Invoke(id);
                onServerClientConnected?.Invoke(id);
            };
            networkServer.OnClientDisconnected += (id) =>
            {
                clientIds.Remove(id);
                Log.Info("{0} 客户端已断开 id={1}", transport, id);
                ServerClientDisconnected?.Invoke(id);
                onServerClientDisconnected?.Invoke(id);
            };
            networkServer.OnMessage += (id, messageId, payload) => OnServerDataReceived(id, payload.ToArray());
            networkServer.OnError += (id, err) =>
            {
                Log.Warning("{0} 服务端错误 id={1} 异常={2}", transport, id, err);
                ServerError?.Invoke(id, err);
                try { onServerError?.Invoke(err); } catch (System.Exception ex) { Log.Warning("onServerError 回调异常: {0}", ex.Message); }
            };

            if (!networkServer.Start())
            {
                // 启动失败：错误事件已由 NetworkServer 内部派发，清理引用
                networkServer.Stop();
                networkServer = null;
            }
        }

        /// <summary>
        /// 发送字符串数据到所有已连接客户端
        /// </summary>
        public void SendToAllClientsString(string text)
        {
            networkServer?.Broadcast(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// 发送数据到所有已连接客户端
        /// </summary>
        public void SendToAllClientsBytes(byte[] data)
        {
            networkServer?.Broadcast(data);
        }

        /// <summary>
        /// 发送数据到指定客户端
        /// </summary>
        public void SendToClientBytes(int clientId, byte[] data)
        {
            networkServer?.Send(clientId, data);
        }

        /// <summary>发送字符串到指定客户端</summary>
        public void SendToClientString(int clientId, string text)
        {
            networkServer?.Send(clientId, Encoding.UTF8.GetBytes(text));
        }

        /// <summary>断开指定客户端</summary>
        public bool DisconnectClient(int clientId)
        {
            return networkServer != null && networkServer.DisconnectClient(clientId);
        }

        /// <summary>注册服务端 RPC 处理器（响应客户端 ClientRequestAsync 的请求）</summary>
        public void RegisterServerRequestHandler(ushort messageId, Func<int, byte[], byte[]> handler)
        {
            networkServer?.RegisterRequestHandler(messageId, handler);
        }

        /// <summary>注册强类型 RPC 处理器（先 RegisterServerObjectMessage 绑定类型）</summary>
        public void RegisterServerRequestHandler<TRequest, TResponse>(ushort messageId, Func<int, TRequest, TResponse> handler)
        {
            networkServer?.RegisterRequestHandler(messageId, handler);
        }

        /// <summary>注册强类型对象消息（类型 ↔ 消息 ID 绑定）</summary>
        public void RegisterServerObjectMessage<T>(ushort messageId)
        {
            networkServer?.RegisterObjectMessage<T>(messageId);
        }

        /// <summary>注册强类型对象处理器（先 RegisterServerObjectMessage 绑定 ID）</summary>
        public void RegisterServerObjectHandler<T>(Action<int, T> handler)
        {
            networkServer?.RegisterObjectHandler(handler);
        }

        /// <summary>广播强类型对象到全部客户端</summary>
        public void BroadcastObject<T>(T obj)
        {
            networkServer?.BroadcastObject(obj);
        }

        /// <summary>
        /// 服务器收到客户端数据时调用，默认回显给发送者；可重写或订阅扩展。
        /// 注意：payload 为按编解码器解码后的负载（不含帧头）。
        /// </summary>
        void OnServerDataReceived(int clientId, byte[] payload)
        {
            var s = Encoding.UTF8.GetString(payload);
            Log.Info("服务器收到来自 {0} 的消息 ({1})：{2}", clientId, transport, s);
            ServerDataReceived?.Invoke(clientId, payload);
            try { onServerDataReceived?.Invoke(s); } catch (System.Exception ex) { Log.Warning("onServerDataReceived 回调异常: {0}", ex.Message); }
            // 默认回显
            SendToClientBytes(clientId, payload);
        }
        #endregion
    }
}
