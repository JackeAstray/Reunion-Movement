using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror.SimpleWeb;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 通用网络组件：可在 Inspector 切换 客户端/服务端 与 传输类型（TCP / KCP / WebSocket）。
    /// 设计为轻量可扩展的封装，示例用途；生产环境请根据需求扩展错误处理、重连、线程安全等。
    /// </summary>
    public class UniversalNetworkBehaviour : MonoBehaviour
    {
        public enum Mode { Client, Server }
        public enum Transport { TCP, KCP, WebSocket }

        [Header("Mode")]
        public Mode mode = Mode.Client;
        public Transport transport = Transport.TCP;

        [Header("Common")]
        public string channelName = "UNET_CHANNEL";
        public string host = "127.0.0.1";
        public int port = 7778;

        // 服务器端连接跟踪
        HashSet<int> clientIds = new HashSet<int>();

        // 客户端对象
        TcpClientChannel tcpClient;
        KcpClientChannel kcpClient;
        SimpleWebClient swtClient;

        // 服务端对象
        TcpServerChannel tcpServer;
        KcpServerChannel kcpServer;
        SimpleWebServer swtServer;

        void Start()
        {
            // 默认不自动启动；调用 StartAsConfigured() 开始
        }

        void Update()
        {
            // 根据所选传输类型调用对应的 Tick / Process，使网络在主线程执行
            switch (transport)
            {
                case Transport.TCP:
                    if (mode == Mode.Client && tcpClient != null) tcpClient.TickRefresh();
                    if (mode == Mode.Server && tcpServer != null) tcpServer.TickRefresh();
                    break;
                case Transport.KCP:
                    if (mode == Mode.Client && kcpClient != null) kcpClient.TickRefresh();
                    if (mode == Mode.Server && kcpServer != null) kcpServer.TickRefresh();
                    break;
                case Transport.WebSocket:
                    if (mode == Mode.Client && swtClient != null) swtClient.ProcessMessageQueue();
                    if (mode == Mode.Server && swtServer != null) swtServer.ProcessMessageQueue();
                    break;
            }
        }

        void OnDestroy()
        {
            StopAll();
        }

        /// <summary>
        /// 根据配置启动客户端或服务器
        /// </summary>
        public void StartAsConfigured()
        {
            StopAll();

            if (mode == Mode.Client)
            {
                StartClient();
            }
            else
            {
                StartServer();
            }
        }

        /// <summary>
        /// 停止所有客户端和服务器
        /// </summary>
        public void StopAll()
        {
            Log.Info("停止所有网络连接...");

            // 客户端关闭
            try { tcpClient?.Close(); } catch { }
            try { kcpClient?.Close(); } catch { }
            try { swtClient?.Disconnect(); } catch { }
            // 服务端关闭
            try { tcpServer?.Close(); } catch { }
            try { kcpServer?.Close(); } catch { }
            try { swtServer?.Stop(); } catch { }
            clientIds.Clear();
        }

        #region Client
        /// <summary>
        /// 启动客户端
        /// </summary>
        public void StartClient()
        {
            switch (transport)
            {
                case Transport.TCP:
                    tcpClient = new TcpClientChannel(channelName);
                    tcpClient.OnConnected += () => Log.Info("TCP 客户端已连接");
                    tcpClient.OnDataReceived += (data) => OnClientDataReceived(data);
                    tcpClient.OnDisconnected += () => Log.Info("TCP 客户端已断开连接");
                    tcpClient.Connect(host, port);
                    NetworkMgr.Instance?.AddChannel(tcpClient);
                    break;
                case Transport.KCP:
                    kcpClient = new KcpClientChannel(channelName);
                    kcpClient.OnConnected += () => Log.Info("KCP 客户端已连接");
                    kcpClient.OnDataReceived += (data) => OnClientDataReceived(data);
                    kcpClient.OnDisconnected += () => Log.Info("KCP 客户端已断开连接");
                    kcpClient.Connect(host, port);
                    NetworkMgr.Instance?.AddChannel(kcpClient);
                    break;
                case Transport.WebSocket:
                    try
                    {
                        var tcpConfig = new TcpConfig(true, 5000, 5000);
                        swtClient = SimpleWebClient.Create(32000, 500, tcpConfig);
                        swtClient.onConnect += () => Log.Info("WebSocket 客户端已连接");
                        swtClient.onDisconnect += () => Log.Info("WebSocket 客户端已断开连接");
                        swtClient.onData += (seg) =>
                        {
                            try
                            {
                                var arr = new byte[seg.Count];
                                Array.Copy(seg.Array, seg.Offset, arr, 0, seg.Count);
                                OnClientDataReceived(arr);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("swtClient.onData 处理错误：" + ex);
                            }
                        };
                        swtClient.onError += (ex) => Log.Warning("WebSocket 客户端错误：" + ex);

                        UriBuilder builder = new UriBuilder(host)
                        {
                            Scheme = host.StartsWith("ws", StringComparison.OrdinalIgnoreCase) ? (new Uri(host).Scheme) : "ws",
                            Port = port
                        };

                        swtClient.Connect(builder.Uri);
                        Log.Info("WebSocket 客户端已创建并开始连接...");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("启动 WebSocket 客户端失败：" + ex);
                    }
                    break;
            }
        }

        /// <summary>
        /// 发送字符串数据到服务器
        /// </summary>
        /// <param name="text"></param>
        public void SendClientString(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            SendClientBytes(bytes);
        }

        /// <summary>
        /// 发送数据到服务器
        /// </summary>
        /// <param name="data"></param>
        public void SendClientBytes(byte[] data)
        {
            switch (transport)
            {
                case Transport.TCP:
                    tcpClient?.SendMessage(data);
                    break;
                case Transport.KCP:
                    kcpClient?.SendMessage(data);
                    break;
                case Transport.WebSocket:
                    try
                    {
                        if (swtClient != null)
                            swtClient.Send(new ArraySegment<byte>(data));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("WebSocket 发送失败：" + ex);
                    }
                    break;
            }
        }

        /// <summary>
        /// 客户端收到数据时调用，默认在日志中回显；可重写或订阅扩展
        /// </summary>
        /// <param name="data"></param>
        void OnClientDataReceived(byte[] data)
        {
            // 默认在日志中回显接收到的数据；外部可以订阅或继承来处理消息
            var s = Encoding.UTF8.GetString(data);
            Log.Info($"客户端接收 ({transport})：{s}");
        }
        #endregion

        #region Server
        /// <summary>
        /// 启动服务器
        /// </summary>
        public void StartServer()
        {
            clientIds.Clear();
            switch (transport)
            {
                case Transport.TCP:
                    tcpServer = new TcpServerChannel(channelName, port);
                    tcpServer.OnConnected += (id, ip) =>
                    {
                        clientIds.Add(id);
                        Log.Info($"TCP 客户端已连接 id={id} ip={ip}");
                    };
                    tcpServer.OnDisconnected += (id) =>
                    {
                        clientIds.Remove(id);
                        Log.Info($"TCP 客户端已断开 id={id}");
                    };
                    tcpServer.OnDataReceived += (id, data) => OnServerDataReceived(id, data);
                    tcpServer.Start();
                    break;
                case Transport.KCP:
                    kcpServer = new KcpServerChannel(channelName, (ushort)port);
                    kcpServer.OnConnected += (id, ip) =>
                    {
                        clientIds.Add(id);
                        Log.Info($"KCP 客户端已连接 id={id} ip={ip}");
                    };
                    kcpServer.OnDisconnected += (id) =>
                    {
                        clientIds.Remove(id);
                        Log.Info($"KCP 客户端已断开 id={id}");
                    };
                    kcpServer.OnDataReceived += (id, data) => OnServerDataReceived(id, data);
                    kcpServer.Start();
                    break;
                case Transport.WebSocket:
                    try
                    {
                        var tcpConfig = new TcpConfig(true, 5000, 5000);
                        swtServer = new SimpleWebServer(500, tcpConfig, 32000, 5000, default);
                        swtServer.onConnect += (id, ip) =>
                        {
                            clientIds.Add(id);
                            Log.Info($"WebSocket 客户端已连接 id={id} ip={ip}");
                        };
                        swtServer.onDisconnect += (id) =>
                        {
                            clientIds.Remove(id);
                            Log.Info($"WebSocket 客户端已断开 id={id}");
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
                        swtServer.onError += (id, ex) => Log.Warning($"WebSocket 服务错误 id={id} 异常={ex}");

                        swtServer.Start((ushort)port);
                        Log.Info("WebSocket 服务已启动...");
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
            Log.Info($"服务器收到来自 {clientId} 的消息 ({transport})：{s}");
            // 默认回显
            SendToClientBytes(clientId, data);
        }
        #endregion
    }
}
