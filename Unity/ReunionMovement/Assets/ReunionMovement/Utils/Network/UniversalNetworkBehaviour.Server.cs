using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Mirror.SimpleWeb;
using UnityEngine.Events;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// UniversalNetworkBehaviour partial part: Server (same class, no behavior change)
    /// </summary>
    public partial class UniversalNetworkBehaviour
    {
        #region Server
        /// <summary>
        /// 从 NetworkMgr 移除通道。先检查 IsInitialized：场景卸载/引擎销毁时直接访问
        /// Instance 会懒创建单例（在销毁中的场景里实例化 GameObject），用 IsInitialized 阻断。
        /// </summary>
        private static void SafeScheduleRemove(INetworkChannel channel)
        {
            try
            {
                if (SingletonMgr<NetworkMgr>.IsInitialized)
                {
                    NetworkMgr.Instance.ScheduleRemove(channel);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("ScheduleRemove 异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void StartServer()
        {
            // 重复启动防护：先关闭旧服务器，避免旧 socket/线程泄漏
            if (tcpServer != null)
            {
                try { tcpServer.Close(); } catch (System.Exception ex) { Log.Warning("关闭旧 TCP 服务端异常: {0}", ex.Message); }
                tcpServer = null;
            }
            if (kcpServer != null)
            {
                try { kcpServer.Close(); } catch (System.Exception ex) { Log.Warning("关闭旧 KCP 服务端异常: {0}", ex.Message); }
                kcpServer = null;
            }
            if (swtServer != null)
            {
                try { swtServer.Stop(); } catch (System.Exception ex) { Log.Warning("关闭旧 WS 服务端异常: {0}", ex.Message); }
                swtServer = null;
            }

            clientIds.Clear();
            switch (transport)
            {
                case Transport.TCP:
                    tcpServer = new TcpServerChannel(channelName, port);
                    tcpServer.OnConnected += (id, ip) =>
                    {
                        clientIds.Add(id);
                        Log.Info("TCP 客户端已连接 id={0} ip={1}", id, ip);
                        ServerClientConnected?.Invoke(id);
                        onServerClientConnected?.Invoke(id);
                    };
                    tcpServer.OnDisconnected += (id) =>
                    {
                        clientIds.Remove(id);
                        Log.Info("TCP 客户端已断开 id={0}", id);
                        ServerClientDisconnected?.Invoke(id);
                        onServerClientDisconnected?.Invoke(id);
                    };
                    tcpServer.OnDataReceived += (id, data) => OnServerDataReceived(id, data);
                    // Hook TCP abort as a generic server error notification
                    tcpServer.OnAbort += () =>
                    {
                        var msg = "TCP 服务中止";
                        Log.Warning(msg);
                        ServerError?.Invoke(-1, msg);
                        try { onServerError?.Invoke(msg); } catch (System.Exception ex) { Log.Warning("onServerError 回调异常: {0}", ex.Message); }
                    };
                    bool tcpStarted = tcpServer.Start();
                    if (tcpStarted)
                    {
                        ServerStarted?.Invoke();
                        onServerStarted?.Invoke();
                    }
                    else
                    {
                        // 启动失败（如端口占用）：派发错误事件而不是 Started，与 WS 分支契约一致
                        var errMsg = "TCP 服务启动失败（端口可能被占用）";
                        Log.Error(errMsg);
                        ServerError?.Invoke(-1, errMsg);
                        try { onServerError?.Invoke(errMsg); } catch (System.Exception ex) { Log.Warning("onServerError 回调异常: {0}", ex.Message); }
                    }
                    break;
                case Transport.KCP:
                    kcpServer = new KcpServerChannel(channelName, (ushort)port);
                    kcpServer.OnConnected += (id, ip) =>
                    {
                        clientIds.Add(id);
                        Log.Info("KCP 客户端已连接 id={0} ip={1}", id, ip);
                        ServerClientConnected?.Invoke(id);
                        onServerClientConnected?.Invoke(id);
                    };
                    kcpServer.OnDisconnected += (id) =>
                    {
                        clientIds.Remove(id);
                        Log.Info("KCP 客户端已断开 id={0}", id);
                        ServerClientDisconnected?.Invoke(id);
                        onServerClientDisconnected?.Invoke(id);
                    };
                    kcpServer.OnDataReceived += (id, data) => OnServerDataReceived(id, data);
                    // attach KCP error handler
                    kcpServer.OnError += (id, err) =>
                    {
                        Log.Warning("KCP 服务错误 id={0} 异常={1}", id, err);
                        ServerError?.Invoke(id, err);
                        try { onServerError?.Invoke(err); } catch (System.Exception ex) { Log.Warning("onServerError 回调异常: {0}", ex.Message); }
                    };
                    bool kcpStarted = kcpServer.Start();
                    if (kcpStarted)
                    {
                        ServerStarted?.Invoke();
                        onServerStarted?.Invoke();
                    }
                    else
                    {
                        // 启动失败（如端口占用）：派发错误事件而不是 Started，与 WS 分支契约一致
                        var errMsg = "KCP 服务启动失败（端口可能被占用）";
                        Log.Error(errMsg);
                        ServerError?.Invoke(-1, errMsg);
                        try { onServerError?.Invoke(errMsg); } catch (System.Exception ex) { Log.Warning("onServerError 回调异常: {0}", ex.Message); }
                    }
                    break;
                case Transport.WebSocket:
                    try
                    {
                        var tcpConfig = new TcpConfig(true, 5000, 5000);
                        swtServer = new SimpleWebServer(500, tcpConfig, 32000, 5000, default);
                        swtServer.onConnect += (id, ip) =>
                        {
                            clientIds.Add(id);
                            Log.Info("WebSocket 客户端已连接 id={0} ip={1}", id, ip);
                            ServerClientConnected?.Invoke(id);
                            onServerClientConnected?.Invoke(id);
                        };
                        swtServer.onDisconnect += (id) =>
                        {
                            clientIds.Remove(id);
                            Log.Info("WebSocket 客户端已断开 id={0}", id);
                            ServerClientDisconnected?.Invoke(id);
                            onServerClientDisconnected?.Invoke(id);
                        };
                        swtServer.onData += (id, seg) =>
                        {
                            try
                            {
                                var arr = new byte[seg.Count];
                                Array.Copy(seg.Array, seg.Offset, arr, 0, seg.Count);
                                OnServerDataReceived(id, arr);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("swtServer.onData 处理错误：" + ex);
                            }
                        };
                        swtServer.onError += (id, ex) =>
                        {
                            Log.Warning("WebSocket 服务错误 id={0} 异常={1}", id, ex);
                            var msg = ex?.ToString() ?? "WebSocket 服务错误";
                            ServerError?.Invoke(id, msg);
                            try { onServerError?.Invoke(msg); } catch (System.Exception ex2) { Log.Warning("onServerError 回调异常: {0}", ex2.Message); }
                        };

                        swtServer.Start((ushort)port);
                        Log.Info("WebSocket 服务已启动...");
                        ServerStarted?.Invoke();
                        onServerStarted?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("启动 WebSocket 服务失败：" + ex);
                    }
                    break;
            }
        }

        /// <summary>
        /// 发送字符串数据到所有已连接客户端
        /// </summary>
        /// <param name="text"></param>
        public void SendToAllClientsString(string text)
        {
            SendToAllClientsBytes(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// 发送数据到所有已连接客户端
        /// </summary>
        /// <param name="data"></param>
        public void SendToAllClientsBytes(byte[] data)
        {
            switch (transport)
            {
                case Transport.TCP:
                    foreach (var id in clientIds)
                    {
                        tcpServer?.SendMessage(id, data);
                    }
                    break;
                case Transport.KCP:
                    foreach (var id in clientIds)
                    {
                        kcpServer?.SendMessage(id, data);
                    }
                    break;
                case Transport.WebSocket:
                    try
                    {
                        var seg = new ArraySegment<byte>(data);
                        foreach (var id in clientIds)
                        {
                            swtServer?.SendOne(id, seg);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("WebSocket 广播失败：" + ex);
                    }
                    break;
            }
        }

        /// <summary>
        /// 发送数据到指定客户端
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="data"></param>
        public void SendToClientBytes(int clientId, byte[] data)
        {
            switch (transport)
            {
                case Transport.TCP:
                    tcpServer?.SendMessage(clientId, data);
                    break;
                case Transport.KCP:
                    kcpServer?.SendMessage(clientId, data);
                    break;
                case Transport.WebSocket:
                    try
                    {
                        swtServer?.SendOne(clientId, new ArraySegment<byte>(data));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("WebSocket 发送失败：" + ex);
                    }
                    break;
            }
        }

        /// <summary>
        /// 服务器收到客户端数据时调用，默认回显；可重写或订阅扩展
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="data"></param>
        void OnServerDataReceived(int clientId, byte[] data)
        {
            var s = Encoding.UTF8.GetString(data);
            Log.Info("服务器收到来自 {0} 的消息 ({1})：{2}", clientId, transport, s);
            ServerDataReceived?.Invoke(clientId, data);
            try { onServerDataReceived?.Invoke(s); } catch (System.Exception ex) { Log.Warning("onServerDataReceived 回调异常: {0}", ex.Message); }
            // 默认回显
            SendToClientBytes(clientId, data);
        }
        #endregion
    }
}
