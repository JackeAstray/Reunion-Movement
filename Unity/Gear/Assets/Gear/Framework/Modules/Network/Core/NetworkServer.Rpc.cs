using System;

using Gear.Common;

namespace Gear.Framework.Modules
{
    public sealed partial class NetworkServer
    {
        /// <summary>
        /// 处理 RPC 请求：鉴权 → 找处理器 → 执行 → 回执。
        /// 失败路径一律回错误帧（0xFFF7），让客户端能区分"未鉴权/未注册/处理器异常"，
        /// 而不是拿到一个空响应当成功。
        /// </summary>
        void HandleRpcRequest(int connectionId, ArraySegment<byte> payload)
        {
            if (!NetworkRpcFrames.TryDecodeRequest(payload, out int correlationId, out ushort targetMessageId, out var requestPayload))
            {
                TryNotifyError(connectionId, "RPC 请求帧格式错误");
                return;
            }

            // 鉴权闸门：开启 requireRpcAuthentication 后，未鉴权连接的 RPC 一律拒绝（防越权调用）
            if (config.requireRpcAuthentication && !authenticatedConnections.Contains(connectionId))
            {
                Log.Warning("[NetworkServer] 未鉴权连接 {0} 调用 RPC {1}，已拒绝", connectionId, targetMessageId);
                TryNotifyError(connectionId, $"连接未鉴权，无法调用 RPC {targetMessageId}");
                SendRpcError(connectionId, correlationId, NetworkRpcFrames.ErrorCodeUnauthenticated,
                    $"连接未鉴权，无法调用 RPC {targetMessageId}");
                return;
            }

            if (!requestHandlers.TryGetValue(targetMessageId, out var handler))
            {
                Log.Warning("[NetworkServer] 未注册的 RPC 请求 ID: {0}", targetMessageId);
                TryNotifyError(connectionId, $"未注册的 RPC 请求 ID: {targetMessageId}");
                SendRpcError(connectionId, correlationId, NetworkRpcFrames.ErrorCodeHandlerMissing,
                    $"未注册的 RPC 请求 ID: {targetMessageId}");
                return;
            }

            byte[] response;
            try
            {
                response = handler(connectionId, requestPayload.ToArray()) ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkServer] RPC 请求 {0} 处理器异常: {1}", targetMessageId, ex.Message);
                TryNotifyError(connectionId, $"RPC 请求 {targetMessageId} 处理器异常: " + ex.Message);
                SendRpcError(connectionId, correlationId, NetworkRpcFrames.ErrorCodeHandlerFailed, ex.Message);
                return;
            }

            // 只有成功才发正常响应；失败一律走错误帧，客户端不会再拿到"空成功"。
            Send(connectionId, NetworkConstants.ReservedResponseMessageId,
                NetworkRpcFrames.EncodeResponse(correlationId, response));
        }

        /// <summary>回执 RPC 错误帧；发送失败只记日志，不能因为回执失败影响连接主循环。</summary>
        void SendRpcError(int connectionId, int correlationId, byte code, string message)
        {
            if (!Send(connectionId, NetworkConstants.ReservedRpcErrorMessageId,
                    NetworkRpcFrames.EncodeError(correlationId, code, message)))
            {
                Log.Warning("[NetworkServer] RPC 错误回执发送失败: conn={0}, corr={1}, code={2}",
                    connectionId, correlationId, code);
            }
        }
    }
}
