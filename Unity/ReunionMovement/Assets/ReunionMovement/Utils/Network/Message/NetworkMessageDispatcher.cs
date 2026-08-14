using System;
using System.Collections.Generic;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络消息分发器 —— 按消息 ID 注册处理回调，收到帧后解码并分发（异常隔离）。
    /// 用法（客户端，配合 INetworkClientChannel）：
    ///   var dispatcher = new NetworkMessageDispatcher();
    ///   dispatcher.RegisterHandler(1, payload => { ... });
    ///   channel.OnDataReceived += dispatcher.Dispatch;
    ///   channel.SendMessage(NetworkMessageCodec.Encode(1, payloadBytes));
    /// 每个连接一个实例（处理器按连接隔离）；服务端按连接各自创建实例。
    /// </summary>
    public class NetworkMessageDispatcher
    {
        private readonly Dictionary<ushort, Action<ArraySegment<byte>>> handlers
            = new Dictionary<ushort, Action<ArraySegment<byte>>>();

        /// <summary>已注册的消息数量</summary>
        public int HandlerCount => handlers.Count;

        /// <summary>收到未注册消息 ID 时的兜底回调（可选，用于协议联调）</summary>
        public Action<ushort, ArraySegment<byte>> OnUnknownMessage;

        /// <summary>注册消息处理器（同一 ID 重复注册会覆盖）</summary>
        public void RegisterHandler(ushort messageId, Action<ArraySegment<byte>> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            handlers[messageId] = handler;
        }

        /// <summary>注销消息处理器；未注册返回 false</summary>
        public bool UnregisterHandler(ushort messageId)
        {
            return handlers.Remove(messageId);
        }

        /// <summary>清空全部处理器</summary>
        public void ClearHandlers()
        {
            handlers.Clear();
        }

        /// <summary>
        /// 分发一个收到的消息帧（可直接挂到 INetworkClientChannel.OnDataReceived）。
        /// 返回是否成功分发（帧无效 / 未注册 ID 返回 false）。
        /// </summary>
        public bool Dispatch(byte[] frame)
        {
            if (!MessageIdCodec.Instance.TryDecode(frame, 0, frame?.Length ?? 0, out var id, out var payload)) return false;
            return DispatchCore(id, payload);
        }

        /// <summary>
        /// 按已解码的（消息 ID + 负载段）分发 —— 由上层编解码层（NetworkClient / NetworkServer）调用。
        /// 返回是否成功分发（未注册 ID 返回 false）。
        /// </summary>
        public bool Dispatch(ushort messageId, ArraySegment<byte> payload)
        {
            return DispatchCore(messageId, payload);
        }

        bool DispatchCore(ushort id, ArraySegment<byte> payload)
        {
            if (handlers.TryGetValue(id, out var handler))
            {
                try
                {
                    handler(payload);
                }
                catch (Exception ex)
                {
                    Log.Warning("[NetworkMessageDispatcher] 消息 {0} 处理器异常: {1}", id, ex.Message);
                    return false;
                }
                return true;
            }

            // 与已注册 handler 一致的异常隔离：坏订阅者不应中断本帧后续消息处理
            try
            {
                OnUnknownMessage?.Invoke(id, payload);
            }
            catch (Exception ex)
            {
                Log.Warning("[NetworkMessageDispatcher] OnUnknownMessage 订阅者异常: {0} {1}", id, ex.Message);
            }
            return false;
        }
    }
}
