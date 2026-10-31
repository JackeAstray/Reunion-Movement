using System;

namespace Gear.Framework.Modules
{
    /// <summary>
    /// RPC 调用被服务端拒绝时抛出的异常。
    /// <para>
    /// 与普通响应区分开的原因：改造前未鉴权 / 未注册 / 处理器异常都回空响应，
    /// 客户端只能拿到一个"成功但为空"的结果，强类型接口直到反序列化才炸，失败原因完全丢失。
    /// </para>
    /// </summary>
    public sealed class NetworkRpcException : Exception
    {
        /// <summary>服务端错误码（<see cref="NetworkRpcFrames.ErrorCodeUnauthenticated"/> 等）。</summary>
        public byte Code { get; }

        public NetworkRpcException(byte code, string message)
            : base($"[RPC {Describe(code)}] {message}")
        {
            Code = code;
        }

        /// <summary>把错误码转成可读名称，便于日志与 UI 展示。</summary>
        public static string Describe(byte code)
        {
            switch (code)
            {
                case NetworkRpcFrames.ErrorCodeUnauthenticated: return "未鉴权";
                case NetworkRpcFrames.ErrorCodeHandlerMissing: return "处理器未注册";
                case NetworkRpcFrames.ErrorCodeHandlerFailed: return "处理器异常";
                case NetworkRpcFrames.ErrorCodeBadRequest: return "请求帧错误";
                default: return "错误码 " + code;
            }
        }
    }
}
