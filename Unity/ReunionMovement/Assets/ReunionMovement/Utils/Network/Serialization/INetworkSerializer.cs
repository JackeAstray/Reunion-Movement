using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络对象序列化器 —— 强类型消息（SendObject / RegisterObjectHandler / RPC）的负载序列化抽象。
    /// 可自行实现 Protobuf / MessagePack 等替换默认 JSON 实现。
    /// </summary>
    public interface INetworkSerializer
    {
        /// <summary>序列化对象为字节</summary>
        byte[] Serialize<T>(T obj);

        /// <summary>反序列化字节为对象</summary>
        T Deserialize<T>(byte[] data);

        /// <summary>按运行时类型反序列化（对象处理器分发使用）</summary>
        object Deserialize(byte[] data, Type type);
    }
}
