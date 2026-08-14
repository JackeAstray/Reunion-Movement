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
    /// UniversalNetworkBehaviour partial part: Client (same class, no behavior change)
    /// </summary>
    public partial class UniversalNetworkBehaviour
    {
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
                        // 先重建心跳再触发用户事件：若订阅者在事件内调用 StopAll，
                        // 事件后才新建的 CTS 不会被字段追踪 → 僵尸心跳协程永久空转
                        if (enableHeartbeat)
                        {
                            heartbeatCts?.Cancel();
                            heartbeatCts?.Dispose();
                            heartbeatCts = new CancellationTokenSource();
                            HeartbeatRoutineAsync(heartbeatCts, heartbeatCts.Token).Forget();
                        }
                        ClientConnected?.Invoke();
                        onClientConnected?.Invoke();
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
                        // 先重建心跳再触发用户事件（同上：防事件内 StopAll 后僵尸心跳）
                        if (enableHeartbeat)
                        {
                            heartbeatCts?.Cancel();
                            heartbeatCts?.Dispose();
                            heartbeatCts = new CancellationTokenSource();
                            HeartbeatRoutineAsync(heartbeatCts, heartbeatCts.Token).Forget();
                        }
                        ClientConnected?.Invoke();
                        onClientConnected?.Invoke();
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
                            // 先重建心跳再触发用户事件（同上：防事件内 StopAll 后僵尸心跳）
                            if (enableHeartbeat)
                            {
                                heartbeatCts?.Cancel();
                                heartbeatCts?.Dispose();
                                heartbeatCts = new CancellationTokenSource();
                                HeartbeatRoutineAsync(heartbeatCts, heartbeatCts.Token).Forget();
                            }
                            ClientConnected?.Invoke();
                            onClientConnected?.Invoke();
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
    }
}
