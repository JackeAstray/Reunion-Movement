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
        public IntEvent onServerClientConnected; // 客户端 ID
        public IntEvent onServerClientDisconnected;
        public StringEvent onServerDataReceived; // 字符串化的数据
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
        CancellationTokenSource reconnectCts;
        CancellationTokenSource heartbeatCts;
        // TCP 是否曾成功连接（用于区分"连接失败"与"正常断开",补齐 TCP 分支的 ClientError 契约）
        bool tcpEverConnected;

        void Start()
        {
            // 默认不自动启动；调用 StartAsConfigured() 开始
        }

        void Update()
        {
            // 网络 Tick 始终在主线程驱动（NetworkMgr 无后台线程模式），
            // 避免并发调用非线程安全的 kcp2k/Telepathy Tick 导致协议状态损坏
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
        /// 停止所有客户端和服务器，清理所有事件订阅和通道引用。
        /// </summary>
        public void StopAll()
        {
            Log.Info("停止所有网络连接...");

            // 停止 UniTask 异步任务
            reconnectCts?.Cancel();
            reconnectCts?.Dispose();
            reconnectCts = null;
            heartbeatCts?.Cancel();
            heartbeatCts?.Dispose();
            heartbeatCts = null;
            reconnectAttempts = 0;

            // 客户端关闭并从 NetworkMgr 移除通道
            if (tcpClient != null)
            {
                SafeScheduleRemove(tcpClient);
                try { tcpClient.Close(); } catch (System.Exception ex) { Log.Warning("TCP客户端关闭异常: {0}", ex.Message); }
                tcpClient = null;
            }
            if (kcpClient != null)
            {
                SafeScheduleRemove(kcpClient);
                try { kcpClient.Close(); } catch (System.Exception ex) { Log.Warning("KCP客户端关闭异常: {0}", ex.Message); }
                kcpClient = null;
            }
            if (swtClient != null)
            {
                try { swtClient.Disconnect(); } catch (System.Exception ex) { Log.Warning("WS客户端断开异常: {0}", ex.Message); }
                swtClient = null;
            }

            // 服务端关闭并从 NetworkMgr 移除通道
            if (tcpServer != null)
            {
                SafeScheduleRemove(tcpServer);
                try { tcpServer.Close(); } catch (System.Exception ex) { Log.Warning("TCP服务端关闭异常: {0}", ex.Message); }
                tcpServer = null;
            }
            if (kcpServer != null)
            {
                SafeScheduleRemove(kcpServer);
                try { kcpServer.Close(); } catch (System.Exception ex) { Log.Warning("KCP服务端关闭异常: {0}", ex.Message); }
                kcpServer = null;
            }
            if (swtServer != null)
            {
                try { swtServer.Stop(); } catch (System.Exception ex) { Log.Warning("WS服务端停止异常: {0}", ex.Message); }
                swtServer = null;
            }

            clientIds.Clear();
        }

        #region Client
        /// <summary>
        /// 启动客户端（外部主动调用：先取消进行中的重连/心跳任务，再全新启动）
        /// </summary>
        public void StartClient()
        {
            // 清除上一次的异步任务
            reconnectCts?.Cancel();
            reconnectCts?.Dispose();
            reconnectCts = null;
            heartbeatCts?.Cancel();
            heartbeatCts?.Dispose();
            heartbeatCts = null;

            StartClientCore();
        }

        /// <summary>
        /// 重连循环专用启动：不清除 reconnectCts。
        /// 注意：ReconnectRoutineAsync 由 reconnectCts 驱动，若在循环内调用 StartClient()
        /// 会取消驱动自身的令牌，导致自动重连永远只尝试一次。
        /// </summary>
        private void StartClientForReconnect()
        {
            heartbeatCts?.Cancel();
            heartbeatCts?.Dispose();
            heartbeatCts = null;

            StartClientCore();
        }

        /// <summary>客户端连接核心流程（关闭旧通道并建立新连接）</summary>
        private void StartClientCore()
        {

            // 注意：不要在此处清零 reconnectAttempts。
            // StopAll()（主动启动前调用）与 ReconnectRoutineAsync 开头负责清零；
            // 若在此清零，重连循环内每次调用 StartClient() 都会重置计数，
            // 导致 maxReconnectAttempts 失效、无限重连。

            // 先关闭旧的客户端通道，避免重连时泄漏连接/线程
            if (tcpClient != null)
            {
                SafeScheduleRemove(tcpClient);
                try { tcpClient.Close(); } catch (System.Exception ex) { Log.Warning("TCP客户端关闭异常: {0}", ex.Message); }
                tcpClient = null;
            }
            if (kcpClient != null)
            {
                SafeScheduleRemove(kcpClient);
                try { kcpClient.Close(); } catch (System.Exception ex) { Log.Warning("KCP客户端关闭异常: {0}", ex.Message); }
                kcpClient = null;
            }
            if (swtClient != null)
            {
                try { swtClient.Disconnect(); } catch (System.Exception ex) { Log.Warning("WS客户端断开异常: {0}", ex.Message); }
                swtClient = null;
            }

            switch (transport)
            {
                case Transport.TCP:
                    tcpClient = new TcpClientChannel(channelName);
                    tcpClient.OnConnected += () =>
                    {
                        Log.Info("TCP 客户端已连接");
                        reconnectAttempts = 0;
                        tcpEverConnected = true;
                        ClientConnected?.Invoke();
                        onClientConnected?.Invoke();
                        // 无条件重建心跳：先取消旧心跳，避免旧协程退出时清空 heartbeatCts
                        // 导致心跳停摆，或重连后多个心跳协程并发发包
                        if (enableHeartbeat)
                        {
                            heartbeatCts?.Cancel();
                            heartbeatCts?.Dispose();
                            heartbeatCts = new CancellationTokenSource();
                            HeartbeatRoutineAsync(heartbeatCts, heartbeatCts.Token).Forget();
                        }
                    };
                    tcpClient.OnDataReceived += (data) =>
                    {
                        OnClientDataReceived(data);
                        ClientDataReceived?.Invoke(data);
                        try { onClientDataReceived?.Invoke(Encoding.UTF8.GetString(data)); } catch (System.Exception ex) { Log.Warning("onClientDataReceived(TCP) 回调异常: {0}", ex.Message); }
                    };
                    tcpClient.OnDisconnected += () =>
                    {
                        Log.Info("TCP 客户端已断开连接");
                        // TCP 连接失败也表现为 OnDisconnected；若从未成功连接，视为连接失败，
                        // 触发 ClientError 以与 KCP/WS 分支的错误事件契约保持一致
                        if (!tcpEverConnected)
                        {
                            const string tcpErrMsg = "TCP 连接失败";
                            ClientError?.Invoke(tcpErrMsg);
                            try { onClientError?.Invoke(tcpErrMsg); } catch (System.Exception ex) { Log.Warning("onClientError(TCP) 回调异常: {0}", ex.Message); }
                        }
                        ClientDisconnected?.Invoke();
                        onClientDisconnected?.Invoke();
                        if (reconnectCts == null && autoReconnect)
                        {
                            reconnectCts = new CancellationTokenSource();
                            ReconnectRoutineAsync(reconnectCts, reconnectCts.Token).Forget();
                        }
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
                        // 无条件重建心跳：先取消旧心跳，避免旧协程退出时清空 heartbeatCts
                        // 导致心跳停摆，或重连后多个心跳协程并发发包
                        if (enableHeartbeat)
                        {
                            heartbeatCts?.Cancel();
                            heartbeatCts?.Dispose();
                            heartbeatCts = new CancellationTokenSource();
                            HeartbeatRoutineAsync(heartbeatCts, heartbeatCts.Token).Forget();
                        }
                    };
                    kcpClient.OnDataReceived += (data) =>
                    {
                        OnClientDataReceived(data);
                        ClientDataReceived?.Invoke(data);
                        try { onClientDataReceived?.Invoke(Encoding.UTF8.GetString(data)); } catch (System.Exception ex) { Log.Warning("onClientDataReceived(KCP) 回调异常: {0}", ex.Message); }
                    };
                    kcpClient.OnDisconnected += () =>
                    {
                        Log.Info("KCP 客户端已断开连接");
                        ClientDisconnected?.Invoke();
                        onClientDisconnected?.Invoke();
                        if (reconnectCts == null && autoReconnect)
                        {
                            reconnectCts = new CancellationTokenSource();
                            ReconnectRoutineAsync(reconnectCts, reconnectCts.Token).Forget();
                        }
                    };
                    kcpClient.OnError += (err) =>
                    {
                        ClientError?.Invoke(err);
                        try { onClientError?.Invoke(err); } catch (System.Exception ex) { Log.Warning("onClientError 回调异常: {0}", ex.Message); }
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
                            // 无条件重建心跳：先取消旧心跳，避免旧协程退出时清空 heartbeatCts
                            // 导致心跳停摆，或重连后多个心跳协程并发发包
                            if (enableHeartbeat)
                            {
                                heartbeatCts?.Cancel();
                                heartbeatCts?.Dispose();
                                heartbeatCts = new CancellationTokenSource();
                                HeartbeatRoutineAsync(heartbeatCts, heartbeatCts.Token).Forget();
                            }
                        };
                        swtClient.onDisconnect += () =>
                        {
                            Log.Info("WebSocket 客户端已断开连接");
                            ClientDisconnected?.Invoke();
                            onClientDisconnected?.Invoke();
                            if (reconnectCts == null && autoReconnect)
                            {
                                reconnectCts = new CancellationTokenSource();
                                ReconnectRoutineAsync(reconnectCts, reconnectCts.Token).Forget();
                            }
                        };
                        swtClient.onData += (seg) =>
                        {
                            try
                            {
                                var arr = new byte[seg.Count];
                                Array.Copy(seg.Array, seg.Offset, arr, 0, seg.Count);
                                OnClientDataReceived(arr);
                                ClientDataReceived?.Invoke(arr);
                                try { onClientDataReceived?.Invoke(Encoding.UTF8.GetString(arr)); } catch (System.Exception ex) { Log.Warning("onClientDataReceived 回调异常: {0}", ex.Message); }
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
                            try { onClientError?.Invoke(ex.ToString()); } catch (System.Exception ex2) { Log.Warning("onClientError 回调异常: {0}", ex2.Message); }
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

        private async UniTaskVoid ReconnectRoutineAsync(CancellationTokenSource owner, CancellationToken ct)
        {
            reconnectAttempts = 0;
            while (autoReconnect && (maxReconnectAttempts < 0 || reconnectAttempts < maxReconnectAttempts) && !ct.IsCancellationRequested)
            {
                reconnectAttempts++;
                Log.Info("尝试第 {0} 次重连...", reconnectAttempts);
                try
                {
                    StartClientForReconnect();
                }
                catch (Exception ex)
                {
                    Log.Warning("重连尝试异常：" + ex);
                }

                // 等待 reconnectInterval 秒，同时检查连接状态
                float waited = 0f;
                while (waited < reconnectInterval && !ct.IsCancellationRequested)
                {
                    bool connected = false;
                    if (transport == Transport.TCP && tcpClient != null) connected = tcpClient.IsConnect;
                    if (transport == Transport.KCP && kcpClient != null) connected = kcpClient.IsConnect;
                    if (transport == Transport.WebSocket && swtClient != null) connected = (swtClient.ConnectionState == ClientState.Connected);
                    if (connected)
                    {
                        // 仅当字段仍指向自己时才清空，防止旧协程退出时误清新重连协程的 CTS
                        if (ReferenceEquals(reconnectCts, owner)) reconnectCts = null;
                        return;
                    }
                    waited += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            // 仅当字段仍指向自己时才清空（同上：防止覆盖新协程的 CTS）
            if (ReferenceEquals(reconnectCts, owner)) reconnectCts = null;
        }

        private async UniTaskVoid HeartbeatRoutineAsync(CancellationTokenSource owner, CancellationToken ct)
        {
            while (enableHeartbeat && !ct.IsCancellationRequested)
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

                // 使用 UniTask.Delay 替代忙等循环（零 GC）
                bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(heartbeatInterval), ignoreTimeScale: false, PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
                if (canceled) break;
            }
            // 仅当字段仍指向自己时才清空：旧协程被取消唤醒时，新心跳协程的 CTS 已被 OnConnected 重建，
            // 无条件置 null 会丢失新 CTS 引用 → StopAll 无法取消新协程 → 销毁后心跳永久存活
            if (ReferenceEquals(heartbeatCts, owner)) heartbeatCts = null;
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
            Log.Info("客户端接收 ({0})：{1}", transport, s);
        }
        #endregion

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
