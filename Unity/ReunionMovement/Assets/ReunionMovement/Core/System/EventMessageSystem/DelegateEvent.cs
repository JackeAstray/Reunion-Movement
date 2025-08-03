using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Core.EventMessage
{
    /// <summary>
    /// 事件数据
    /// </summary>
    public class EventData
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public EventMessageType type;
        /// <summary>
        /// 事件传递的数据
        /// </summary>
        public object data;
    }

    /// <summary>
    /// 委托事件类
    /// </summary>
    public class DelegateEvent
    {
        /// <summary>
        /// 定义委托函数
        /// </summary>
        /// <param name="data"></param>
        public delegate void EventHandler(EventData data);

        // 用 List 存储监听器，便于扩展和管理
        private readonly List<EventHandler> listeners = new List<EventHandler>();
        private readonly List<EventHandler> onceListeners = new List<EventHandler>();

        /// <summary>
        /// 触发监听事件
        /// </summary>
        public void Handle(EventData data)
        {
            // 普通监听
            foreach (var listener in listeners)
            {
                listener?.Invoke(data);
            }
            // 一次性监听
            foreach (var listener in onceListeners)
            {
                listener?.Invoke(data);
            }
            onceListeners.Clear();
        }

        /// <summary>
        /// 添加监听函数
        /// </summary>
        public void AddListener(EventHandler addHandle)
        {
            if (addHandle == null) return;
            if (!listeners.Contains(addHandle))
            {
                listeners.Add(addHandle);
            }
        }

        /// <summary>
        /// 添加一次性监听函数
        /// </summary>
        public void AddOnceListener(EventHandler addHandle)
        {
            if (addHandle == null) return;
            if (!onceListeners.Contains(addHandle))
            {
                onceListeners.Add(addHandle);
            }
        }

        /// <summary>
        /// 删除监听函数
        /// </summary>
        public void RemoveListener(EventHandler removeHandle)
        {
            if (removeHandle == null) return;
            listeners.Remove(removeHandle);
            onceListeners.Remove(removeHandle);
        }

        /// <summary>
        /// 清空所有监听
        /// </summary>
        public void Clear()
        {
            listeners.Clear();
            onceListeners.Clear();
        }
    }
}