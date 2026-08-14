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
    public partial class UniversalNetworkBehaviour : MonoBehaviour
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

    }
}
