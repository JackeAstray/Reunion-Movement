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

        [Tooltip("通过将最大消息大小设置得较小来防止分配攻击。否则攻击者可能会发送多个带有2GB头的伪造数据包，导致服务器在分配多个大数据包后耗尽内存。")]
        public int maxMessageSize = 16 * 1024;

        [FormerlySerializedAs("handshakeMaxSize")]
        [Tooltip("作为WebSocket握手发送的http头的最大大小")]
        public int maxHandshakeSize = 16 * 1024;

        [FormerlySerializedAs("serverMaxMessagesPerTick")]
        [Tooltip("限制服务器每个帧处理的消息数量。允许LateUpdate完成，以便重置 Unity 继续进行，以防在处理之前到达更多消息")]
        public int serverMaxMsgsPerTick = 10000;

        [FormerlySerializedAs("clientMaxMessagesPerTick")]
        [Tooltip("限制客户端每个帧处理的消息数量。允许LateUpdate完成，以便重置 Unity 继续进行，以防在处理之前到达更多消息")]
        public int clientMaxMsgsPerTick = 1000;

        [Tooltip("发送时如果网络在发送过程中被切断，将无限期停滞，因此我们需要一个超时（以毫秒为单位）")]
        public int sendTimeout = 5000;

        [Tooltip("在断开连接之前没有消息的时间（以毫秒为单位）")]
        public int receiveTimeout = 20000;

        [Tooltip("禁用nagle算法。降低CPU%和延迟，但增加带宽")]
        public bool noDelay = true;

        [Header("过时的SSL设置")]

        [Tooltip("需要wss连接到服务器，仅与SSL cert.json一起使用，切勿与反向代理一起使用。\n注意：如果sslEnabled为true，则clientUseWss强制为true，即使未选中。")]
        public bool sslEnabled;

        [Tooltip("SSL证书创建以支持的协议。")]
        public SslProtocols sslProtocols = SslProtocols.Tls12;

        [Tooltip("包含证书及其密码路径的json文件路径\n使用Json文件，以便证书密码不包含在客户端构建中\n请参阅Assets/Mirror/Transports/.cert.example.Json")]
        public string sslCertJson = "./cert.json";

        [Header("服务器设置")]

        [Tooltip("服务器使用的端口")]
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

        [Tooltip("在调用Stream.Send之前将消息分组到队列中")]
        public bool batchSend = true;

        [Tooltip("在分组和发送消息之前等待1毫秒。\n" +
            "这为镜像提供了时间，以完成将消息添加到队列中，以便可以减少需要的组数。\n" +
            "如果WaitBeforeSend为true，则BatchSend也将被设置为true")]
        public bool waitBeforeSend = true;

        [Header("客户端设置")]

        [Tooltip("将连接方案设置为wss。当客户端需要在TLS位于传输外部时使用wss进行连接时，十分有用。\n注意：如果sslEnabled为true，则clientUseWss也是true")]
        public bool clientUseWss;
        public ClientWebsocketSettings clientWebsocketSettings = new ClientWebsocketSettings { ClientPortOption = WebsocketPortOption.DefaultSameAsServer, CustomClientPort = 7777 };

        [Header("日志记录")]

        [Tooltip("选择日志记录的最低严重性级别\nFlood级别需要Debug版本")]
        [SerializeField] Log.Levels minimumLogLevel = Log.Levels.Warn;

        /// <summary>
        /// <para>获取 _logLevels 字段</para>
        /// <para>设置 _logLevels 和 Log.level 字段</para>
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

        // 虽然不完全准确，但在浏览器中很难获取实际的加密套件
        // 使用反向代理时，代理与服务器之间的连接可能未加密。
        public override string EncryptionCipher => "TLS";

        public override bool ClientConnected()
        {
            // 不为 null 且不是 NotConnected（我们希望在连接或断开过程中也返回 true）
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
                    // 在构建器中不包含端口允许网页控制端口
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
                // 在断开连接事件发送后清除 client
                // 断开后不应再有消息
                client = null;
            };

            client.onData += (ArraySegment<byte> data) => OnClientDataReceived.Invoke(data, Channels.Reliable);

            // 如果 minLogLevel 设置为 None，则不会调用 OnClientError
            // 仅当 minLogLevel 设置为 Verbose 时才发送完整异常信息
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
            // 不在此处将 client 置为 null，否则消息将无法被处理
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

            // 触发事件。若无统计监听则可能为 null。
            OnClientDataSent?.Invoke(segment, Channels.Reliable);
        }

        // 消息应始终在早期更新中处理
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
                Log.Warn("[SWT-ServerStart]: 服务器已启动");

            SslConfig config = SslConfigLoader.Load(sslEnabled, sslCertJson, sslProtocols);
            server = new SimpleWebServer(serverMaxMsgsPerTick, TcpConfig, maxMessageSize, maxHandshakeSize, config);

            server.onConnect += OnServerConnectedWithAddress.Invoke;
            server.onDisconnect += OnServerDisconnected.Invoke;
            server.onData += (int connId, ArraySegment<byte> data) => OnServerDataReceived.Invoke(connId, data, Channels.Reliable);

            // 如果 minLogLevel 设置为 None，则不会调用 OnServerError
            // 仅当 minLogLevel 设置为 Verbose 时才发送完整异常信息
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

            // 触发事件。若无统计监听则可能为 null。
            OnServerDataSent?.Invoke(connectionId, segment, Channels.Reliable);
        }

        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);

        public Request ServerGetClientRequest(int connectionId) => server.GetClientRequest(connectionId);

        // 消息应始终在早期更新中处理
        public override void ServerEarlyUpdate()
        {
            server?.ProcessMessageQueue(this);
        }

        #endregion
    }
}
#endif
