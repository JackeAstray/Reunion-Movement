using System;
using System.Collections.Generic;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 类型 ↔ 消息 ID 注册表：支撑 SendObject&lt;T&gt; / RegisterObjectHandler&lt;T&gt; 等强类型 API。
    /// 同一消息 ID 只能绑定一个类型（双向映射，含冲突检测）。
    /// </summary>
    public sealed class NetworkTypedProtocol
    {
        readonly Dictionary<Type, ushort> typeToId = new Dictionary<Type, ushort>();
        readonly Dictionary<ushort, Type> idToType = new Dictionary<ushort, Type>();

        /// <summary>注册类型与消息 ID 的绑定；ID 冲突或类型重复注册返回 false</summary>
        public bool Register<T>(ushort messageId)
        {
            var type = typeof(T);
            if (idToType.TryGetValue(messageId, out var existing))
            {
                if (existing == type) return true;
                Log.Error("[NetworkTypedProtocol] 消息 ID {0} 已注册给类型 {1}，无法再注册给 {2}", messageId, existing.Name, type.Name);
                return false;
            }
            if (typeToId.ContainsKey(type))
            {
                Log.Error("[NetworkTypedProtocol] 类型 {0} 已注册过消息 ID", type.Name);
                return false;
            }
            typeToId[type] = messageId;
            idToType[messageId] = type;
            return true;
        }

        /// <summary>查询类型对应的消息 ID</summary>
        public bool TryGetId(Type type, out ushort messageId)
        {
            return typeToId.TryGetValue(type, out messageId);
        }

        /// <summary>查询消息 ID 对应的类型</summary>
        public bool TryGetType(ushort messageId, out Type type)
        {
            return idToType.TryGetValue(messageId, out type);
        }
    }
}
