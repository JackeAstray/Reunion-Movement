using System;

namespace Gear.Framework.Modules
{
    /// <summary>
    /// 可选能力：直接从内存反序列化，省掉 <c>byte[]</c> 入参带来的整段复制。
    ///
    /// <para>
    /// 为什么单独一个接口而不是给 <see cref="INetworkSerializer"/> 加方法：
    /// 加方法会让所有既有实现编译失败。做成可选能力后，实现了它的序列化器自动走上零拷贝路径，
    /// 没实现的仍然按原方式工作（<see cref="NetworkSerializerExtensions"/> 负责分派）。
    /// </para>
    ///
    /// <para>
    /// JSON 实现仍会分配一个字符串（<see cref="JsonUtility"/> 只吃 string），
    /// 但至少省掉了每条报文的 byte[] 分配 + 复制；MessagePack / Protobuf 这类
    /// 能直接读 Span 的序列化器可以做到真正零分配。
    /// </para>
    /// </summary>
    public interface INetworkSpanSerializer
    {
        /// <summary>反序列化（从只读内存，不复制入参）。</summary>
        T Deserialize<T>(ReadOnlyMemory<byte> data);

        /// <summary>按运行时类型反序列化（从只读内存，不复制入参）。</summary>
        object Deserialize(ReadOnlyMemory<byte> data, Type type);
    }

    /// <summary>
    /// <see cref="INetworkSerializer"/> 的内存入参重载：
    /// 序列化器实现了 <see cref="INetworkSpanSerializer"/> 就零拷贝，否则退回 <c>ToArray()</c> 复制。
    /// </summary>
    public static class NetworkSerializerExtensions
    {
        /// <summary>反序列化对象（优先零拷贝路径）。</summary>
        public static T Deserialize<T>(this INetworkSerializer serializer, ReadOnlyMemory<byte> data)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (serializer is INetworkSpanSerializer spanSerializer)
            {
                return spanSerializer.Deserialize<T>(data);
            }
            return serializer.Deserialize<T>(data.ToArray());
        }

        /// <summary>按运行时类型反序列化（优先零拷贝路径）。</summary>
        public static object Deserialize(this INetworkSerializer serializer, ReadOnlyMemory<byte> data, Type type)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (serializer is INetworkSpanSerializer spanSerializer)
            {
                return spanSerializer.Deserialize(data, type);
            }
            return serializer.Deserialize(data.ToArray(), type);
        }
    }
}
