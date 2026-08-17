using System;
using System.Text;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// JSON 序列化器（Unity JsonUtility）：
    /// 要求类型可被 Unity 序列化（public 字段 / [SerializeField]，[Serializable] 标注），
    /// 不支持 Dictionary、多态与顶层数组；如需这些能力请自行实现 INetworkSerializer 替换。
    /// </summary>
    public sealed class JsonNetSerializer : INetworkSerializer
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
    }
}
