using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Gear.Framework.Modules
{
    public sealed partial class NetworkClient
    {
        #region RPC 请求/响应

        /// <summary>
        /// 发送请求并等待响应（服务端需用 NetworkServer.RegisterRequestHandler 注册同 ID 处理器）。
        /// 超时抛 TimeoutException；连接断开/关闭抛 InvalidOperationException；
        /// 服务端拒绝（未鉴权/未注册/处理器异常）抛 <see cref="NetworkRpcException"/>。
        /// </summary>
        public UniTask<byte[]> RequestAsync(ushort messageId, byte[] payload, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (state != ClientState.Connected || channel == null || !channel.IsConnect)
            {
                return UniTask.FromException<byte[]>(new InvalidOperationException("未连接，无法发起请求"));
            }
            int correlationId = Interlocked.Increment(ref rpcCorrelation);
            var requestFrame = NetworkRpcFrames.EncodeRequest(correlationId, messageId, payload);
            var tcs = new UniTaskCompletionSource<byte[]>();
            // 必须先注册再做 Send：Send 在同步派发的传输层上可能立刻触发响应处理，
            // 那时 pendingRequests 里还没有这个 correlationId，响应会被当成"未知关联 ID"丢弃，
            // 调用方只能白等一个超时（且失败原因完全不可见）。
            pendingRequests[correlationId] = tcs;
            if (!Send(NetworkConstants.ReservedRequestMessageId, requestFrame))
            {
                pendingRequests.Remove(correlationId);
                return UniTask.FromException<byte[]>(new InvalidOperationException("发送请求失败"));
            }
            return WaitResponseAsync(correlationId, tcs, timeout, cancellationToken);
        }

        /// <summary>RequestAsync 的秒数便捷重载</summary>
        public UniTask<byte[]> RequestAsync(ushort messageId, byte[] payload, float timeoutSeconds, CancellationToken cancellationToken = default)
        {
            return RequestAsync(messageId, payload, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        }

        /// <summary>强类型请求/响应（类型需先注册消息 ID）</summary>
        public async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (!typedProtocol.TryGetId(typeof(TRequest), out var messageId))
            {
                throw new InvalidOperationException($"类型 {typeof(TRequest).Name} 未注册消息 ID（先调用 RegisterObjectMessage）");
            }
            var response = await RequestAsync(messageId, serializer.Serialize(request), timeout, cancellationToken);
            return serializer.Deserialize<TResponse>(response);
        }

        async UniTask<byte[]> WaitResponseAsync(int correlationId, UniTaskCompletionSource<byte[]> tcs, TimeSpan timeout, CancellationToken ct)
        {
            try
            {
                var (hasResultLeft, result) = await UniTask.WhenAny(
                    tcs.Task,
                    // 超时计时不受 timeScale 影响：游戏暂停（timeScale=0）时 RPC 不会悬挂到恢复
                    UniTask.Delay(timeout, ignoreTimeScale: true, PlayerLoopTiming.Update, ct));
                if (hasResultLeft) return result;
                throw new TimeoutException($"请求超时（{timeout.TotalSeconds:0.0}s）");
            }
            finally
            {
                pendingRequests.Remove(correlationId);
            }
        }

        #endregion
    }
}
