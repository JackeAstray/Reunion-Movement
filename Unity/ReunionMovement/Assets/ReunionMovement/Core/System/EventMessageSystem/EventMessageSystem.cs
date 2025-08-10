using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Core.EventMessage
{
    /// <summary>
    /// 事件消息系统
    /// </summary>
    public class EventMessageSystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<EventMessageSystem> instance = new(() => new EventMessageSystem());
        public static EventMessageSystem Instance => instance.Value;
        public bool IsInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        private readonly Dictionary<EventMessageType, DelegateEvent> eventTypeListeners = new Dictionary<EventMessageType, DelegateEvent>();

        public async Task Init()
        {
            initProgress = 100;
            IsInited = true;
            Log.Debug("EventMessageSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {
            // 这里可以添加定时任务或其他逻辑
        }

        public void Clear()
        {
            Log.Debug("EventMessageSystem 清除数据");
            // 清理所有注册的事件监听器
            // 这里可以添加清理逻辑
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void AddEventListener(EventMessageType type, DelegateEvent.EventHandler listenerFunc)
        {
            if (!eventTypeListeners.TryGetValue(type, out var delegateEvent))
            {
                delegateEvent = new DelegateEvent();
                eventTypeListeners[type] = delegateEvent;
            }
            delegateEvent.AddListener(listenerFunc);
        }

        /// <summary>
        /// 删除事件
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void RemoveEventListener(EventMessageType type, DelegateEvent.EventHandler listenerFunc)
        {
            if (listenerFunc == null) return;
            if (eventTypeListeners.TryGetValue(type, out var delegateEvent))
            {
                delegateEvent.RemoveListener(listenerFunc);
            }
        }

        /// <summary>
        /// 泛型分发事件（可选）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <param name="data"></param>
        public void DispatchEvent<T>(EventMessageType type, T data)
        {
            if (eventTypeListeners.TryGetValue(type, out var delegateEvent))
            {
                var eventData = new EventData
                {
                    type = type,
                    data = data
                };
                delegateEvent.Handle(eventData);
            }
        }

        /// <summary>
        /// 分发方法
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        public void DispatchEvent(EventMessageType type, object data)
        {
            if (eventTypeListeners.TryGetValue(type, out var delegateEvent))
            {
                var eventData = new EventData
                {
                    type = type,
                    data = data
                };
                delegateEvent.Handle(eventData);
            }
        }

        /// <summary>
        /// 清除某一类型的事件监听器
        /// </summary>
        /// <param name="type"></param>
        public void ClearEventTypeListeners(EventMessageType type)
        {
            if (eventTypeListeners.Remove(type))
            {
                Log.Debug($"清除事件类型 {type} 的所有监听器");
            }
            else
            {
                Log.Warning($"尝试清除不存在的事件类型 {type} 的监听器");
            }
        }

        /// <summary>
        /// 清除所有事件监听器
        /// </summary>
        public void ClearAllEventListeners()
        {
            eventTypeListeners.Clear();
            Log.Debug("清除所有事件监听器");
        }
    }
}