using System;
using Mirror.SimpleWeb;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// WebSocket 服务端通道（SimpleWebTransport）—— 统一到 INetworkServerChannel。
    /// 所有事件在 TickRefresh（主线程）派发。
    /// </summary>
    public sealed class WebSocketServerChannel : INetworkServerChannel
    {
        public const int DefaultMaxMessageSize = 32000;

        SimpleWebServer server;

        public string ChannelName { get; set; }

        public int Port { get; private set; }

        public string Host => string.Empty; // 监听所有网卡

        public bool Active => server != null && server.Active;

        public bool IsOpen => Active;

        public event Action<int, string> OnConnected;
        public event Action<int, byte[]> OnDataReceived;
        public event Action<int> OnDisconnected;
        public event Action<int, string> OnError;

        public WebSocketServerChannel(string channelName, int port, int maxMessageSize = DefaultMaxMessageSize)
        {
            ChannelName = channelName;
            Port = port;
            server = new SimpleWebServer(500, new TcpConfig(true, 5000, 5000), maxMessageSize, 5000, default);
            server.onConnect += HandleConnect;
            server.onDisconnect += HandleDisconnect;
            server.onData += HandleData;
            server.onError += HandleError;
        }

        public bool Start()
        {
            try
            {
                server.Start((ushort)Port);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("WebSocket 服务端启动失败: {0}", ex.Message);
                OnError?.Invoke(-1, "WebSocket 服务端启动失败: " + ex.Message);
                return false;
            }
        }

        public void TickRefresh()
        {
            server.ProcessMessageQueue();
        }

        public bool SendMessage(int connectionId, byte[] data)
        {
            if (!Active || data == null || data.Length == 0) return false;
            try
            {
                server.SendOne(connectionId, new ArraySegment<byte>(data));
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(connectionId, "WebSocket 发送失败: " + ex.Message);
                return false;
            }
        }

        public bool Disconnect(int connectionId)
        {
            return server.KickClient(connectionId);
        }

        public string GetConnectionAddress(int connectionId)
        {
            return server.GetClientAddress(connectionId) ?? string.Empty;
        }

        public void Close()
        {
            server.onConnect -= HandleConnect;
            server.onDisconnect -= HandleDisconnect;
            server.onData -= HandleData;
            server.onError -= HandleError;
            try { server.Stop(); } catch (Exception ex) { Log.Warning("WebSocket 服务端停止异常: {0}", ex.Message); }
            OnConnected = null;
            OnDataReceived = null;
            OnDisconnected = null;
            OnError = null;
        }

        void HandleConnect(int connectionId, string address)
        {
            OnConnected?.Invoke(connectionId, address);
        }

        void HandleDisconnect(int connectionId)
        {
            OnDisconnected?.Invoke(connectionId);
        }

        void HandleData(int connectionId, ArraySegment<byte> segment)
        {
            if (segment.Count <= 0) return;
            var data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
            OnDataReceived?.Invoke(connectionId, data);
        }

        void HandleError(int connectionId, Exception exception)
        {
            OnError?.Invoke(connectionId, exception?.Message ?? "WebSocket 未知错误");
        }
    }
}
