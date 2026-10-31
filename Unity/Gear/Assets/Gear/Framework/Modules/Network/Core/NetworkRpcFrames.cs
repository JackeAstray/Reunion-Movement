using System;
using System.Text;

using Gear.Common;
using Gear.Utilities;
namespace Gear.Framework.Modules
{
    /// <summary>
    /// RPC 请求/响应帧封装。
    /// 请求帧：[4 字节关联 ID（小端）][2 字节目标消息 ID（小端）][负载]，整体作为负载放入
    /// 消息 ID = NetworkConstants.ReservedRequestMessageId 的消息中发送；
    /// 响应帧：[4 字节关联 ID][负载]，放入 ReservedResponseMessageId 中返回。
    /// 错误帧：[4 字节关联 ID][1 字节错误码][UTF8 消息]，放入 ReservedRpcErrorMessageId 中返回。
    /// 与业务消息 ID 空间完全隔离，支持并发多个在途请求（关联 ID 区分）。
    /// </summary>
    public static class NetworkRpcFrames
    {
        public const int CorrelationSize = sizeof(int);
        public const int TargetIdSize = sizeof(ushort);
        public const int ErrorHeaderSize = CorrelationSize + 1;

        /// <summary>错误码：连接未通过鉴权，RPC 被拒绝。</summary>
        public const byte ErrorCodeUnauthenticated = 1;
        /// <summary>错误码：目标消息 ID 没有注册处理器。</summary>
        public const byte ErrorCodeHandlerMissing = 2;
        /// <summary>错误码：处理器执行时抛异常。</summary>
        public const byte ErrorCodeHandlerFailed = 3;
        /// <summary>错误码：服务端内部错误（如请求帧无法解析）。</summary>
        public const byte ErrorCodeBadRequest = 4;

        /// <summary>组装请求帧：[4B 关联 ID][2B 目标 ID][负载]</summary>
        public static byte[] EncodeRequest(int correlationId, ushort targetMessageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var frame = new byte[CorrelationSize + TargetIdSize + payload.Length];
            WriteInt32LE(frame, 0, correlationId);
            frame[CorrelationSize] = (byte)(targetMessageId & 0xFF);
            frame[CorrelationSize + 1] = (byte)((targetMessageId >> 8) & 0xFF);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, CorrelationSize + TargetIdSize, payload.Length);
            }
            return frame;
        }

        /// <summary>解析请求帧</summary>
        public static bool TryDecodeRequest(ArraySegment<byte> data, out int correlationId, out ushort targetMessageId, out ArraySegment<byte> payload)
        {
            correlationId = 0;
            targetMessageId = 0;
            payload = default;
            if (data.Array == null || data.Count < CorrelationSize + TargetIdSize) return false;
            correlationId = ReadInt32LE(data.Array, data.Offset);
            targetMessageId = (ushort)(data.Array[data.Offset + CorrelationSize] | (data.Array[data.Offset + CorrelationSize + 1] << 8));
            payload = new ArraySegment<byte>(data.Array, data.Offset + CorrelationSize + TargetIdSize, data.Count - CorrelationSize - TargetIdSize);
            return true;
        }

        /// <summary>组装响应帧：[4B 关联 ID][负载]</summary>
        public static byte[] EncodeResponse(int correlationId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var frame = new byte[CorrelationSize + payload.Length];
            WriteInt32LE(frame, 0, correlationId);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, CorrelationSize, payload.Length);
            }
            return frame;
        }

        /// <summary>解析响应帧</summary>
        public static bool TryDecodeResponse(ArraySegment<byte> data, out int correlationId, out ArraySegment<byte> payload)
        {
            correlationId = 0;
            payload = default;
            if (data.Array == null || data.Count < CorrelationSize) return false;
            correlationId = ReadInt32LE(data.Array, data.Offset);
            payload = new ArraySegment<byte>(data.Array, data.Offset + CorrelationSize, data.Count - CorrelationSize);
            return true;
        }

        /// <summary>组装错误帧：[4B 关联 ID][1B 错误码][UTF8 消息]</summary>
        public static byte[] EncodeError(int correlationId, byte code, string message)
        {
            byte[] text = string.IsNullOrEmpty(message) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(message);
            var frame = new byte[ErrorHeaderSize + text.Length];
            WriteInt32LE(frame, 0, correlationId);
            frame[CorrelationSize] = code;
            if (text.Length > 0)
            {
                Buffer.BlockCopy(text, 0, frame, ErrorHeaderSize, text.Length);
            }
            return frame;
        }

        /// <summary>解析错误帧；消息可为空</summary>
        public static bool TryDecodeError(ArraySegment<byte> data, out int correlationId, out byte code, out string message)
        {
            correlationId = 0;
            code = 0;
            message = string.Empty;
            if (data.Array == null || data.Count < ErrorHeaderSize) return false;
            correlationId = ReadInt32LE(data.Array, data.Offset);
            code = data.Array[data.Offset + CorrelationSize];
            int textLength = data.Count - ErrorHeaderSize;
            if (textLength > 0)
            {
                message = Encoding.UTF8.GetString(data.Array, data.Offset + ErrorHeaderSize, textLength);
            }
            return true;
        }

        static void WriteInt32LE(byte[] dst, int pos, int value)
        {
            dst[pos] = (byte)(value & 0xFF);
            dst[pos + 1] = (byte)((value >> 8) & 0xFF);
            dst[pos + 2] = (byte)((value >> 16) & 0xFF);
            dst[pos + 3] = (byte)((value >> 24) & 0xFF);
        }

        static int ReadInt32LE(byte[] src, int pos)
        {
            return src[pos] | (src[pos + 1] << 8) | (src[pos + 2] << 16) | (src[pos + 3] << 24);
        }
    }
}
