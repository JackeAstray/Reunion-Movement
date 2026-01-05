// server needs to store a separate KcpPeer for each connection.
// as well as remoteEndPoint so we know where to send data to.
using System;
using System.Net;

namespace kcp2k
{
    public class KcpServerConnection : KcpPeer
    {
        public readonly EndPoint remoteEndPoint;

        // 回调
        // 即使发生错误也会回调，以便库显示对话框等。
        // 而不是直接记录日志。
        // （使用 string 而不是 Exception 以便易用并避免用户恐慌）
        //
        // 事件为只读，在构造函数中设置。
        // 这可以确保在使用时它们已经初始化。
        // 解决了 https://github.com/MirrorNetworking/Mirror/issues/3337 等问题
        protected readonly Action<KcpServerConnection> OnConnectedCallback;
        protected readonly Action<ArraySegment<byte>, KcpChannel> OnDataCallback;
        protected readonly Action OnDisconnectedCallback;
        protected readonly Action<ErrorCode, string> OnErrorCallback;
        protected readonly Action<ArraySegment<byte>> RawSendCallback;

        public KcpServerConnection(
            Action<KcpServerConnection> OnConnected,
            Action<ArraySegment<byte>, KcpChannel> OnData,
            Action OnDisconnected,
            Action<ErrorCode, string> OnError,
            Action<ArraySegment<byte>> OnRawSend,
            KcpConfig config,
            uint cookie,
            EndPoint remoteEndPoint)
                : base(config, cookie)
        {
            OnConnectedCallback = OnConnected;
            OnDataCallback = OnData;
            OnDisconnectedCallback = OnDisconnected;
            OnErrorCallback = OnError;
            RawSendCallback = OnRawSend;

            this.remoteEndPoint = remoteEndPoint;
        }

        // 回调 ///////////////////////////////////////////////////////////
        protected override void OnAuthenticated()
        {
            // 一旦收到客户端的第一个 hello，立即回复 hello，使客户端知道安全 cookie。
            SendHello();
            OnConnectedCallback(this);
        }

        protected override void OnData(ArraySegment<byte> message, KcpChannel channel) =>
            OnDataCallback(message, channel);

        protected override void OnDisconnected() =>
            OnDisconnectedCallback();

        protected override void OnError(ErrorCode error, string message) =>
            OnErrorCallback(error, message);

        protected override void RawSend(ArraySegment<byte> data) =>
            RawSendCallback(data);
        ////////////////////////////////////////////////////////////////////////

        // 插入原始 IO。通常来自 socket.Receive。
        // offset 对于中继很有用，我们可能解析头部然后把其余数据喂给 kcp。
        public void RawInput(ArraySegment<byte> segment)
        {
            // 确保有效大小：至少 1 字节通道 + 4 字节 cookie
            if (segment.Count <= 5) return;

            // 解析通道
            // byte channel = segment[0]; ArraySegment[i] 在旧版 Unity Mono 中不受支持
            byte channel = segment.Array[segment.Offset + 0];

            // 所有 server->client 消息都包含服务器的安全 cookie。
            // 所有 client->server 消息（除初始 hello）也应包含 cookie。
            // 解析 cookie 并确保匹配（初始 hello 除外）。
            Utils.Decode32U(segment.Array, segment.Offset + 1, out uint messageCookie);

            // 安全性：认证后消息应包含 cookie，以防 UDP 欺骗。
            // 如果 cookie 不匹配则直接丢弃消息。
            if (state == KcpState.Authenticated)
            {
                if (messageCookie != cookie)
                {
                    // 用 Info 记录足够，不要惊动用户。
                    // => 这可能是恶意消息
                    // => 也可能是客户端的 Hello 消息被多次重传，这是正常的。
                    Log.Info($"[KCP] ServerConnection: 丢弃具有无效 cookie 的消息: {messageCookie} 来自 {remoteEndPoint} 期望: {cookie} 状态: {state}。这可能是客户端的 Hello 被多次发送，或是尝试的 UDP 欺骗。");
                    return;
                }
            }

            // 解析消息
            ArraySegment<byte> message = new ArraySegment<byte>(segment.Array, segment.Offset + 1+4, segment.Count - 1-4);

            switch (channel)
            {
                case (byte)KcpChannel.Reliable:
                {
                    OnRawInputReliable(message);
                    break;
                }
                case (byte)KcpChannel.Unreliable:
                {
                    OnRawInputUnreliable(message);
                    break;
                }
                default:
                {
                    // 无效通道通常表示随机互联网噪声。
                    // 服务器可能收到随机 UDP 数据。
                    // 忽略，但记录以便调试。
                    Log.Warning($"[KCP] ServerConnection: 无效的通道头: {channel} 来自 {remoteEndPoint}，可能是互联网噪声");
                    break;
                }
            }
        }
    }
}
