using System;
using Mirror.SimpleWeb;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// WebSocket 客户端通道（SimpleWebTransport）—— 统一到 INetworkClientChannel。
    /// host 支持 "example.com"、"ws://example.com"、"wss://example.com/path"（带 scheme 且无端口时按 port 参数补全）。
    /// 连接失败/从未成功连接即断开 → 触发 OnError，与 TCP/KCP 通道错误契约一致。
    /// </summary>
    public sealed class WebSocketClientChannel : INetworkClientChannel
    {
        public const int DefaultMaxMessageSize = 32000;

        readonly SimpleWebClient client;
        bool everConnected;
        bool disconnectRaised;

        public string ChannelName { get; set; }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public bool IsConnect => client.ConnectionState == ClientState.Connected;

        public bool IsOpen => client.ConnectionState == ClientState.Connected;

        public event Action OnConnected;
        public event Action<byte[]> OnDataReceived;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        public WebSocketClientChannel(string channelName, int maxMessageSize = DefaultMaxMessageSize)
        {
            ChannelName = channelName;
            client = SimpleWebClient.Create(maxMessageSize, 500, new TcpConfig(true, 5000, 5000));
            client.onConnect += HandleConnect;
            client.onDisconnect += HandleDisconnect;
            client.onData += HandleData;
            client.onError += HandleError;
        }

        public void Connect(string host, int port)
        {
            Host = host;
            Port = port;
            everConnected = false;
            disconnectRaised = false;
            client.Connect(BuildUri(host, port));
        }

        public void TickRefresh()
        {
            client.ProcessMessageQueue();
        }

        public bool SendMessage(byte[] data)
        {
            if (!IsConnect || data == null || data.Length == 0) return false;
            try
            {
                client.Send(new ArraySegment<byte>(data));
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke("WebSocket 发送失败: " + ex.Message);
                return false;
            }
        }

        public void Close()
        {
            client.onConnect -= HandleConnect;
            client.onDisconnect -= HandleDisconnect;
            client.onData -= HandleData;
            client.onError -= HandleError;
            try { client.Disconnect(); } catch (Exception ex) { Log.Warning("WebSocket 客户端断开异常: {0}", ex.Message); }
            OnConnected = null;
            OnDataReceived = null;
            OnDisconnected = null;
            OnError = null;
        }

        static Uri BuildUri(string host, int port)
        {
            if (host.StartsWith("ws", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(host, UriKind.Absolute, out var parsed))
            {
                var builder = new UriBuilder(parsed);
                if (parsed.Port == -1) builder.Port = port; // 未显式指定端口时补端口
                return builder.Uri;
            }
            return new UriBuilder("ws", host, port).Uri;
        }

        void HandleConnect()
        {
            everConnected = true;
            OnConnected?.Invoke();
        }

        void HandleDisconnect()
        {
            if (disconnectRaised) return;
            disconnectRaised = true;
            if (!everConnected)
            {
                OnError?.Invoke("WebSocket 连接失败");
            }
            OnDisconnected?.Invoke();
        }

        void HandleData(ArraySegment<byte> segment)
        {
            if (segment.Count <= 0) return;
            var data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
            OnDataReceived?.Invoke(data);
        }

        void HandleError(Exception exception)
        {
            var message = exception?.Message ?? "WebSocket 未知错误";
            OnError?.Invoke(message);
            // 连接阶段失败可能只触发 Error 不触发 Disconnected，补发以保证上层重连逻辑可感知
            if (!everConnected && !disconnectRaised)
            {
                disconnectRaised = true;
                OnDisconnected?.Invoke();
            }
        }
    }
}
