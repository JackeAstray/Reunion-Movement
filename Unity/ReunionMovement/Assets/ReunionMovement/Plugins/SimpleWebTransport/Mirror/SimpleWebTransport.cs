#if MIRROR
using System;
using System.Net;
using System.Security.Authentication;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.SimpleWeb
{
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/websockets-transport")]
    public class SimpleWebTransport : Transport, PortTransport
    {
        public const string NormalScheme = "ws";
        public const string SecureScheme = "wss";

        [Tooltip("最大消息大小（字节）。设置较小值可防止分配攻击——攻击者可能发送多个带有2GB头部的伪造数据包，导致服务器在分配大量数据包后耗尽内存。")]
        public int maxMessageSize = 16 * 1024;

        [FormerlySerializedAs("handshakeMaxSize")]
        [Tooltip("WebSocket 握手时发送的 HTTP 头的最大大小（字节）")]
        public int maxHandshakeSize = 16 * 1024;

        [FormerlySerializedAs("serverMaxMessagesPerTick")]
        [Tooltip("限制服务器每帧处理的消息数量。在 LateUpdate 中执行，保证 Unity 持续响应，防止消息积压过多。")]
        public int serverMaxMsgsPerTick = 10000;

        [FormerlySerializedAs("clientMaxMessagesPerTick")]
        [Tooltip("限制客户端每帧处理的消息数量。在 LateUpdate 中执行，保证 Unity 持续响应，防止消息积压过多。")]
        public int clientMaxMsgsPerTick = 1000;

        [Tooltip("发送超时（毫秒）。若网络在发送过程中断开，发送操作将无限期停滞，需要此超时来中断。")]
        public int sendTimeout = 5000;

        [Tooltip("接收超时（毫秒）。在此时间内未收到任何消息则断开连接。")]
        public int receiveTimeout = 20000;

        [Tooltip("禁用 Nagle 算法。可降低 CPU 占用和延迟，但会增加带宽消耗。")]
        public bool noDelay = true;

        [Header("SSL 设置")]

        [Tooltip("启用 wss 连接到服务器。仅与 SSL cert.json 配合使用，切勿与反向代理同时使用。\n注意：若 sslEnabled 为 true，则 clientUseWss 强制为 true，即使未勾选。")]
        public bool sslEnabled;

        [Tooltip("SSL 证书支持的协议版本。")]
        public SslProtocols sslProtocols = SslProtocols.Tls12;

        [Tooltip("包含证书及密钥路径的 JSON 文件路径。\n使用 JSON 文件可确保证书私钥不会被打包到客户端构建中。\n示例：Assets/Mirror/Transports/.cert.example.Json")]
        public string sslCertJson = "./cert.json";

        [Header("服务器设置")]

        [Tooltip("服务器监听的端口号")]
        public ushort port = 27777;
        public ushort Port
        {
            get
            {
#if UNITY_WEBGL
                if (clientWebsocketSettings.ClientPortOption == WebsocketPortOption.SpecifyPort)
                    return clientWebsocketSettings.CustomClientPort;
                else
                    return port;
#else
                return port;
#endif
            }
            set
            {
#if UNITY_WEBGL
                if (clientWebsocketSettings.ClientPortOption == WebsocketPortOption.SpecifyPort)
                    clientWebsocketSettings.CustomClientPort = value;
                else
                    port = value;
#else
                port = value;
#endif
            }
        }

        [Tooltip("在调用 Stream.Send 之前将消息批量打包到队列中")]
        public bool batchSend = true;

        [Tooltip("在发送消息前等待1秒再执行批量发送。\n" +
            "这为 Mirror 提供了将消息添加到队列的缓冲时间，从而减少所需的发送次数。\n" +
            "若 WaitBeforeSend 为 true，则 BatchSend 也会被强制设为 true")]
        public bool waitBeforeSend = true;

        [Header("客户端设置")]

        [Tooltip("将连接协议设为 wss。当 TLS 位于传输层外部，客户端需要以 wss 方式连接时非常有用（如 WebGL）。\n注意：若 sslEnabled 为 true，则 clientUseWss 也为 true")]
        public bool clientUseWss;
        public ClientWebsocketSettings clientWebsocketSettings = new ClientWebsocketSettings { ClientPortOption = WebsocketPortOption.DefaultSameAsServer, CustomClientPort = 7777 };

        [Header("日志记录")]

        [Tooltip("选择要启用的最低日志级别\nFlood 级别需要 Debug 版本")]
        [SerializeField] Log.Levels minimumLogLevel = Log.Levels.Warn;

        /// <summary>
        /// <para>获取/设置日志级别</para>
        /// <para>同时更新 Log.minLogLevel 字段</para>
        /// </summary>
        public Log.Levels LogLevels
        {
            get => minimumLogLevel;
            set
            {
                minimumLogLevel = value;
                Log.minLogLevel = minimumLogLevel;
            }
        }

        SimpleWebClient client;
        SimpleWebServer server;

        TcpConfig TcpConfig => new TcpConfig(noDelay, sendTimeout, receiveTimeout);

        void Awake()
        {
            Log.minLogLevel = minimumLogLevel;
        }

        public override string ToString() => $"SWT [{port}]";

        void OnValidate()
        {
            Log.minLogLevel = minimumLogLevel;
        }

        public override bool Available() => true;

        public override int GetMaxPacketSize(int channelId = 0) => maxMessageSize;

        public override void Shutdown()
        {
            client?.Disconnect();
            client = null;
            server?.Stop();
            server = null;
        }

        #region Client

        string GetClientScheme() => (sslEnabled || clientUseWss) ? SecureScheme : NormalScheme;

        public override bool IsEncrypted => ClientConnected() && (clientUseWss || sslEnabled) || ServerActive() && sslEnabled;

        // 虽然不完全准确，但在浏览器中很难获取实际的加密套件信息。
        // 使用反向代理时，代理与服务器之间的连接可能未加密。
        public override string EncryptionCipher => "TLS";

        public override bool ClientConnected()
        {
            // 不为 null 且不是 NotConnected（正在连接或正在断开也视为已连接）
            return client != null && client.ConnectionState != ClientState.NotConnected;
        }

        public override void ClientConnect(string hostname)
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = GetClientScheme(),
                Host = hostname,
            };

            switch (clientWebsocketSettings.ClientPortOption)
            {
                case WebsocketPortOption.SpecifyPort:
                    builder.Port = clientWebsocketSettings.CustomClientPort;
                    break;
                case WebsocketPortOption.MatchWebpageProtocol:
                    // 不在构建器中指定端口，由网页端控制端口
                    // https://github.com/MirrorNetworking/Mirror/pull/3477
                    break;
                default: // default case handles ClientWebsocketPortOption.DefaultSameAsServerPort
                    builder.Port = port;
                    break;
            }

            ClientConnect(builder.Uri);
        }

        public override void ClientConnect(Uri uri)
        {
            // 正在连接或已连接
            if (ClientConnected())
            {
                Log.Warn("[SWT-ClientConnect]: 已经连接");
                return;
            }

            client = SimpleWebClient.Create(maxMessageSize, clientMaxMsgsPerTick, TcpConfig);
            if (client == null)
                return;

            client.onConnect += OnClientConnected.Invoke;

            client.onDisconnect += () =>
            {
                OnClientDisconnected.Invoke();
                // 在断开连接事件发送后清空 client 引用
                // 断开后不应再处理任何消息
                client = null;
            };

            client.onData += (ArraySegment<byte> data) => OnClientDataReceived.Invoke(data, Channels.Reliable);

            // 若 minLogLevel 设为 None，则不会触发 OnClientError
            // 仅当 minLogLevel 设为 Verbose 时才发送完整异常信息
            switch (Log.minLogLevel)
            {
                case Log.Levels.Flood:
                case Log.Levels.Verbose:
                    client.onError += (Exception e) =>
                    {
                        OnClientError.Invoke(TransportError.Unexpected, e.ToString());
                        ClientDisconnect();
                    };
                    break;
                case Log.Levels.Info:
                case Log.Levels.Warn:
                case Log.Levels.Error:
                    client.onError += (Exception e) =>
                    {
                        OnClientError.Invoke(TransportError.Unexpected, e.Message);
                        ClientDisconnect();
                    };
                    break;
            }

            client.Connect(uri);
        }

        public override void ClientDisconnect()
        {
            // 此处不将 client 置为 null，否则待处理的消息无法被消费
            client?.Disconnect();
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            if (!ClientConnected())
            {
                Log.Error("[SWT-ClientSend]: 未连接");
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SWT-ClientSend]: 消息超过最大允许大小");
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SWT-ClientSend]: 消息长度为零");
                return;
            }

            client.Send(segment);

            // 触发事件（若无统计监听器注册则可能为 null）
            OnClientDataSent?.Invoke(segment, Channels.Reliable);
        }

        // 消息应始终在 EarlyUpdate 中处理
        public override void ClientEarlyUpdate()
        {
            client?.ProcessMessageQueue(this);
        }

        #endregion

        #region Server

        string GetServerScheme() => sslEnabled ? SecureScheme : NormalScheme;

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = GetServerScheme(),
                Host = Dns.GetHostName(),
                Port = port
            };
            return builder.Uri;
        }

        public override bool ServerActive()
        {
            return server != null && server.Active;
        }

        public override void ServerStart()
        {
            if (ServerActive())
                Log.Warn("[SWT-ServerStart]: 服务器已在运行");

            SslConfig config = SslConfigLoader.Load(sslEnabled, sslCertJson, sslProtocols);
            server = new SimpleWebServer(serverMaxMsgsPerTick, TcpConfig, maxMessageSize, maxHandshakeSize, config);

            server.onConnect += OnServerConnectedWithAddress.Invoke;
            server.onDisconnect += OnServerDisconnected.Invoke;
            server.onData += (int connId, ArraySegment<byte> data) => OnServerDataReceived.Invoke(connId, data, Channels.Reliable);

            // 若 minLogLevel 设为 None，则不会触发 OnServerError
            // 仅当 minLogLevel 设为 Verbose 时才发送完整异常信息
            switch (Log.minLogLevel)
            {
                case Log.Levels.Flood:
                case Log.Levels.Verbose:
                    server.onError += (connId, exception) =>
                    {
                        OnServerError(connId, TransportError.Unexpected, exception.ToString());
                        ServerDisconnect(connId);
                    };
                    break;
                case Log.Levels.Info:
                case Log.Levels.Warn:
                case Log.Levels.Error:
                    server.onError += (connId, exception) =>
                    {
                        OnServerError(connId, TransportError.Unexpected, exception.Message);
                        ServerDisconnect(connId);
                    };
                    break;
            }

            SendLoopConfig.batchSend = batchSend || waitBeforeSend;
            SendLoopConfig.sleepBeforeSend = waitBeforeSend;

            server.Start(port);
        }

        public override void ServerStop()
        {
            if (ServerActive())
            {
                server.Stop();
                server = null;
            }
        }

        public override void ServerDisconnect(int connectionId)
        {
            if (ServerActive())
                server.KickClient(connectionId);
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (!ServerActive())
            {
                Log.Error("[SWT-ServerSend]: 服务器未激活");
                return;
            }

            if (segment.Count > maxMessageSize)
            {
                Log.Error("[SWT-ServerSend]: 消息超过最大允许大小");
                return;
            }

            if (segment.Count == 0)
            {
                Log.Error("[SWT-ServerSend]: 消息长度为零");
                return;
            }

            server.SendOne(connectionId, segment);

            // 触发事件（若无统计监听器注册则可能为 null）
            OnServerDataSent?.Invoke(connectionId, segment, Channels.Reliable);
        }

        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);

        public Request ServerGetClientRequest(int connectionId) => server.GetClientRequest(connectionId);

        // 消息应始终在 EarlyUpdate 中处理
        public override void ServerEarlyUpdate()
        {
            server?.ProcessMessageQueue(this);
        }

        #endregion
    }
}
#endif
