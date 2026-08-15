using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ReunionMovement.Common.Util
{
    [Serializable]
    public class StringEvent : UnityEvent<string> { }
    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    /// <summary>
    /// 通用网络组件：可在 Inspector 切换 客户端/服务端 与 传输类型（TCP / KCP / WebSocket / RawTCP）。
    /// 内部基于 NetworkClient / NetworkServer（状态机、自动重连、心跳、消息分发、RPC），
    /// 高级用法可通过 Client / Server 属性直接访问底层 API。
    /// </summary>
    public partial class UniversalNetworkBehaviour : MonoBehaviour
    {
        public enum Mode { Client, Server }
        public enum Transport { TCP, KCP, WebSocket, RawTCP }

        [Header("模式")]
        public Mode mode = Mode.Client;
        public Transport transport = Transport.TCP;

        [Header("公共")]
        public string channelName = "UNET_CHANNEL";
        public string host = "127.0.0.1";
        public int port = 7778;

        [Header("协议")]
        [Tooltip("消息帧格式：MessageId(默认) / LengthPrefixed / LengthPrefixedWithId / Passthrough；RawTCP 建议 LengthPrefixed")]
        public NetworkCodecType codec = NetworkCodecType.MessageId;

        [Header("自动重连/心跳")]
        public bool autoReconnect = true;
        public int maxReconnectAttempts = 5; // -1 表示无限重连
        public float reconnectInterval = 3f;

        public bool enableHeartbeat = false;
        public float heartbeatInterval = 5f;
        public string heartbeatText = "PING";

        [Tooltip("服务端收到消息后是否回显给发送者（调试用；默认关闭，回显会使流量翻倍）")]
        public bool echoToSender = false;

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
        readonly HashSet<int> clientIds = new HashSet<int>();

        // 底层高级客户端 / 服务端（由 StartClient / StartServer 创建）
        NetworkClient networkClient;
        NetworkServer networkServer;

        /// <summary>底层客户端 API（未启动客户端时为 null；含消息分发、RPC、重连配置等高级能力）</summary>
        public NetworkClient Client => networkClient;

        /// <summary>底层服务端 API（未启动服务端时为 null）</summary>
        public NetworkServer Server => networkServer;

        /// <summary>服务端模式下已连接的客户端 ID 集合（只读）</summary>
        public IReadOnlyCollection<int> ClientIds => clientIds;

        void Start()
        {
            // 默认不自动启动；调用 StartAsConfigured() 开始
        }

        void Update()
        {
            // 网络 Tick 始终在主线程驱动（底层通道非线程安全），
            // 事件（连接/断开/数据）在本帧内派发
            if (networkClient != null) networkClient.Tick();
            if (networkServer != null) networkServer.Tick();
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

            if (networkClient != null)
            {
                networkClient.Close();
                networkClient = null;
            }
            if (networkServer != null)
            {
                networkServer.Stop();
                networkServer = null;
            }
            clientIds.Clear();
        }

        /// <summary>Inspector 枚举 → 底层传输类型</summary>
        internal static NetworkTransportType ToTransportType(Transport transport)
        {
            switch (transport)
            {
                case Transport.KCP: return NetworkTransportType.Kcp;
                case Transport.WebSocket: return NetworkTransportType.WebSocket;
                case Transport.RawTCP: return NetworkTransportType.RawTcp;
                default: return NetworkTransportType.Tcp;
            }
        }
    }
}

