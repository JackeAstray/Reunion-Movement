using System;
using System.Text;
using UnityEngine;

using Gear.Common;
using Gear.Utilities;
namespace Gear.Framework.Modules
{
    /// <summary>
    /// JSON 序列化器（Unity JsonUtility）：
    /// 要求类型可被 Unity 序列化（public 字段 / [SerializeField]，[Serializable] 标注），
    /// 不支持 Dictionary、多态与顶层数组；如需这些能力请自行实现 INetworkSerializer 替换。
    /// </summary>
    public sealed class JsonNetSerializer : INetworkSerializer, INetworkSpanSerializer
    {
        public static readonly JsonNetSerializer Instance = new JsonNetSerializer();

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public byte[] Serialize<T>(T obj)
        {
            return Utf8.GetBytes(JsonUtility.ToJson(obj));
        }

        public T Deserialize<T>(byte[] data)
        {
            return JsonUtility.FromJson<T>(Utf8.GetString(data));
        }

        public object Deserialize(byte[] data, Type type)
        {
            return JsonUtility.FromJson(Utf8.GetString(data), type);
        }

        // 内存入参：跳过调用方的 ToArray() 拷贝。JsonUtility 只接受 string，
        // 所以这里仍会分配一个字符串，但少了一次 byte[] 分配 + 整段复制。
        public T Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            return JsonUtility.FromJson<T>(Utf8.GetString(data.Span));
        }

        public object Deserialize(ReadOnlyMemory<byte> data, Type type)
        {
            return JsonUtility.FromJson(Utf8.GetString(data.Span), type);
        }
    }
}
