using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror.SimpleWeb;
using UnityEngine.Events;

namespace ReunionMovement.Common.Util
{
    [Serializable]
    public class StringEvent : UnityEvent<string> { }
    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    /// <summary>
    /// 通用网络组件：可在 Inspector 切换 客户端/服务端 与 传输类型（TCP / KCP / WebSocket）。
    /// 设计为轻量可扩展的封装，示例用途；生产环境请根据需求扩展错误处理、重连、线程安全等。
    /// </summary>
    public class UniversalNetworkBehaviour : MonoBehaviour
    {
        public enum Mode { Client, Server }
        public enum Transport { TCP, KCP, WebSocket }

        [Header("模式")]
        public Mode mode = Mode.Client;
        public Transport transport = Transport.TCP;

        [Header("公共")]
        public string channelName = "UNET_CHANNEL";
        public string host = "127.0.0.1";
        public int port = 7778;

        [Header("自动重连/心跳")]
        public bool autoReconnect = true;
        public int maxReconnectAttempts = 5; // -1 表示无限重连
        public float reconnectInterval = 3f;

        public bool enableHeartbeat = false;
        public float heartbeatInterval = 5f;
        public string heartbeatText = "PING";

        [Header("Inspector 控制")]
        public string inspectorSendText = "Hello from Inspector";

        // 便于在 Inspector 中订阅的 UnityEvent
        [Header("事件")]
        public UnityEvent onClientConnected;
        public UnityEvent onClientDisconnected;
        public StringEvent onClientDataReceived;
        public StringEvent onClientError;

        public UnityEvent onServerStarted;
        public IntEvent onServerClientConnected; // client id
        public IntEvent onServerClientDisconnected;
        public StringEvent onServerDataReceived; // stringified data
        public StringEvent onServerError;

        // 供代码订阅的 C# 事件
        public event Action ClientConnected;
        public event Action ClientDisconnected;
        public event Action<byte[]> ClientDataReceived;
        public event Action<string> ClientError;

        public event Action ServerStarted;
        public event Action<int> ServerClientConnected;
        public event Action<int> ServerClientDisconnected;
        public event Action<int, byte[]> ServerDataReceived;
        public event Action<int, string> ServerError;

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

        // 内部状态
        int reconnectAttempts = 0;
        Coroutine reconnectCoroutine;
        Coroutine heartbeatCoroutine;

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

            // stop coroutines
            if (reconnectCoroutine != null) StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
            if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
            reconnectAttempts = 0;

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
            // ensure previous coroutines cleared
            if (reconnectCoroutine != null) { StopCoroutine(reconnectCoroutine); reconnectCoroutine = null; }
            if (heartbeatCoroutine != null) { StopCoroutine(heartbeatCoroutine); heartbeatCoroutine = null; }
            reconnectAttempts = 0;

            switch (transport)
            {
                case Transport.TCP:
                    tcpClient = new TcpClientChannel(channelName);
                    tcpClient.OnConnected += () =>
                    {
                        Log.Info("TCP 客户端已连接");
                        reconnectAttempts = 0;
                        ClientConnected?.Invoke();
                        onClientConnected?.Invoke();
                        // start heartbeat if enabled
                        if (enableHeartbeat && heartbeatCoroutine == null)
                            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
                    };
                    tcpClient.OnDataReceived += (data) =>
                    {
                        OnClientDataReceived(data);
                        ClientDataReceived?.Invoke(data);
                        try { onClientDataReceived?.Invoke(Encoding.UTF8.GetString(data)); } catch { }
                    };
                    tcpClient.OnDisconnected += () =>
                    {
                        Log.Info("TCP 客户端已断开连接");
                        ClientDisconnected?.Invoke();
                        onClientDisconnected?.Invoke();
                        if (reconnectCoroutine == null && autoReconnect)
                            reconnectCoroutine = StartCoroutine(ReconnectRoutine());
                    };
                    tcpClient.Connect(host, port);
                    NetworkMgr.Instance?.AddChannel(tcpClient);
                    break;
                case Transport.KCP:
                    kcpClient = new KcpClientChannel(channelName);
                    kcpClient.OnConnected += () =>
                    {
                        Log.Info("KCP 客户端已连接");
                        reconnectAttempts = 0;
                        ClientConnected?.Invoke();
                        onClientConnected?.Invoke();
                        if (enableHeartbeat && heartbeatCoroutine == null)
                            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
                    };
                    kcpClient.OnDataReceived += (data) =>
                    {
                        OnClientDataReceived(data);
                        ClientDataReceived?.Invoke(data);
                        try { onClientDataReceived?.Invoke(Encoding.UTF8.GetString(data)); } catch { }
                    };
                    kcpClient.OnDisconnected += () =>
                    {
                        Log.Info("KCP 客户端已断开连接");
                        ClientDisconnected?.Invoke();
                        onClientDisconnected?.Invoke();
                        if (reconnectCoroutine == null && autoReconnect)
                            reconnectCoroutine = StartCoroutine(ReconnectRoutine());
                    };
                    kcpClient.OnError += (err) =>
                    {
                        ClientError?.Invoke(err);
                        try { onClientError?.Invoke(err); } catch { }
                    };
                    kcpClient.Connect(host, port);
                    NetworkMgr.Instance?.AddChannel(kcpClient);
                    break;
                case Transport.WebSocket:
                    try
                    {
                        var tcpConfig = new TcpConfig(true, 5000, 5000);
                        swtClient = SimpleWebClient.Create(32000, 500, tcpConfig);
                        swtClient.onConnect += () =>
                        {
                            Log.Info("WebSocket 客户端已连接");
                            reconnectAttempts = 0;
                            ClientConnected?.Invoke();
                            onClientConnected?.Invoke();
                            if (enableHeartbeat && heartbeatCoroutine == null)
                                heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
                        };
                        swtClient.onDisconnect += () =>
                        {
                            Log.Info("WebSocket 客户端已断开连接");
                            ClientDisconnected?.Invoke();
                            onClientDisconnected?.Invoke();
                            if (reconnectCoroutine == null && autoReconnect)
                                reconnectCoroutine = StartCoroutine(ReconnectRoutine());
                        };
                        swtClient.onData += (seg) =>
                        {
                            try
                            {
                                var arr = new byte[seg.Count];
                                Array.Copy(seg.Array, seg.Offset, arr, 0, seg.Count);
                                OnClientDataReceived(arr);
                                ClientDataReceived?.Invoke(arr);
                                try { onClientDataReceived?.Invoke(Encoding.UTF8.GetString(arr)); } catch { }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("swtClient.onData 处理错误：" + ex);
                            }
                        };
                        swtClient.onError += (ex) =>
                        {
                            Log.Warning("WebSocket 客户端错误：" + ex);
                            ClientError?.Invoke(ex.ToString());
                            try { onClientError?.Invoke(ex.ToString()); } catch { }
                        };

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

        System.Collections.IEnumerator ReconnectRoutine()
        {
            reconnectAttempts = 0;
            while (autoReconnect && (maxReconnectAttempts < 0 || reconnectAttempts < maxReconnectAttempts))
            {
                reconnectAttempts++;
                Log.Info($"尝试第 {reconnectAttempts} 次重连...");
                try
                {
                    StartClient();
                }
                catch (Exception ex)
                {
                    Log.Warning("重连尝试异常：" + ex);
                }

                // wait for reconnectInterval seconds while giving chance for connection events to reset attempts
                float waited = 0f;
                while (waited < reconnectInterval)
                {
                    // if connected, finish
                    bool connected = false;
                    if (transport == Transport.TCP && tcpClient != null) connected = tcpClient.IsConnect;
                    if (transport == Transport.KCP && kcpClient != null) connected = kcpClient.IsConnect;
                    if (transport == Transport.WebSocket && swtClient != null) connected = (swtClient.ConnectionState == ClientState.Connected);
                    if (connected)
                    {
                        reconnectCoroutine = null;
                        yield break;
                    }
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            // ended attempts
            reconnectCoroutine = null;
        }

        System.Collections.IEnumerator HeartbeatRoutine()
        {
            while (enableHeartbeat)
            {
                bool connected = false;
                if (transport == Transport.TCP && tcpClient != null) connected = tcpClient.IsConnect;
                if (transport == Transport.KCP && kcpClient != null) connected = kcpClient.IsConnect;
                if (transport == Transport.WebSocket && swtClient != null) connected = (swtClient.ConnectionState == ClientState.Connected);

                if (connected)
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(heartbeatText);
                        SendClientBytes(bytes);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("心跳发送失败：" + ex);
                    }
                }

                float waited = 0f;
                while (waited < heartbeatInterval)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }
            heartbeatCoroutine = null;
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
                        ServerClientConnected?.Invoke(id);
                        onServerClientConnected?.Invoke(id);
                    };
                    tcpServer.OnDisconnected += (id) =>
                    {
                        clientIds.Remove(id);
                        Log.Info($"TCP 客户端已断开 id={id}");
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
                        try { onServerError?.Invoke(msg); } catch { }
                    };
                    tcpServer.Start();
                    ServerStarted?.Invoke();
                    onServerStarted?.Invoke();
                    break;
                case Transport.KCP:
                    kcpServer = new KcpServerChannel(channelName, (ushort)port);
                    kcpServer.OnConnected += (id, ip) =>
                    {
                        clientIds.Add(id);
                        Log.Info($"KCP 客户端已连接 id={id} ip={ip}");
                        ServerClientConnected?.Invoke(id);
                        onServerClientConnected?.Invoke(id);
                    };
                    kcpServer.OnDisconnected += (id) =>
                    {
                        clientIds.Remove(id);
                        Log.Info($"KCP 客户端已断开 id={id}");
                        ServerClientDisconnected?.Invoke(id);
                        onServerClientDisconnected?.Invoke(id);
                    };
                    kcpServer.OnDataReceived += (id, data) => OnServerDataReceived(id, data);
                    // attach KCP error handler
                    kcpServer.OnError += (id, err) =>
                    {
                        Log.Warning($"KCP 服务错误 id={id} 异常={err}");
                        ServerError?.Invoke(id, err);
                        try { onServerError?.Invoke(err); } catch { }
                    };
                    kcpServer.Start();
                    ServerStarted?.Invoke();
                    onServerStarted?.Invoke();
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
                            ServerClientConnected?.Invoke(id);
                            onServerClientConnected?.Invoke(id);
                        };
                        swtServer.onDisconnect += (id) =>
                        {
                            clientIds.Remove(id);
                            Log.Info($"WebSocket 客户端已断开 id={id}");
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
                            Log.Warning($"WebSocket 服务错误 id={id} 异常={ex}");
                            var msg = ex?.ToString() ?? "WebSocket 服务错误";
                            ServerError?.Invoke(id, msg);
                            try { onServerError?.Invoke(msg); } catch { }
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
            Log.Info($"服务器收到来自 {clientId} 的消息 ({transport})：{s}");
            ServerDataReceived?.Invoke(clientId, data);
            try { onServerDataReceived?.Invoke(s); } catch { }
            // 默认回显
            SendToClientBytes(clientId, data);
        }
        #endregion
    }
}
