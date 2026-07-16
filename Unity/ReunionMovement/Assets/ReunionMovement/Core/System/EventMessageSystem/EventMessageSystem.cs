using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReunionMovement.Core.EventMessage
{
    /// <summary>
    /// 事件数据
    /// </summary>
    public class EventData
    {
        /// <summary>事件类型</summary>
        public EventMessageType type;
        /// <summary>事件传递的数据</summary>
        public object data;
    }

    /// <summary>
    /// 事件消息系统 —— 基于 R3 Subject&lt;T&gt; 的类型安全事件总线
    /// </summary>
    public class EventMessageSystem : ICustomSystem
    {
        #region 单例与初始化
        private static readonly Lazy<EventMessageSystem> instance = new(() => new EventMessageSystem());
        public static EventMessageSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        /// <summary>R3 Subject 字典 —— 每种事件类型对应一个 Subject，支持多播和 LINQ 操作</summary>
        private readonly Dictionary<EventMessageType, Subject<EventData>> eventSubjects
            = new Dictionary<EventMessageType, Subject<EventData>>();

        /// <summary>订阅追踪列表 —— 使用结构体包装避免 delegate 作为字典 Key 的哈希不稳定问题</summary>
        private readonly Dictionary<EventMessageType, List<SubscriptionEntry>> subscriptionTrackers
            = new Dictionary<EventMessageType, List<SubscriptionEntry>>();

        /// <summary>订阅条目（handler + 对应的 IDisposable）</summary>
        private struct SubscriptionEntry
        {
            public Action<EventData> Handler;
            public IDisposable Disposable;
        }

        public UniTask Init()
        {
            initProgress = 100;
            isInited = true;
            Log.Debug("EventMessageSystem 初始化完成 (R3)");
            return UniTask.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {
            // 这里可以添加定时任务或其他逻辑
        }

        public void Clear()
        {
            Log.Debug("EventMessageSystem 清除数据");

            // 释放所有订阅追踪
            foreach (var kvp in subscriptionTrackers)
            {
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    kvp.Value[i].Disposable?.Dispose();
                }
                kvp.Value.Clear();
            }
            subscriptionTrackers.Clear();

            foreach (var kvp in eventSubjects)
            {
                kvp.Value?.Dispose();
            }
            eventSubjects.Clear();

            isInited = false;
        }

        /// <summary>
        /// 获取或创建指定事件类型的 Subject
        /// </summary>
        private Subject<EventData> GetOrCreateSubject(EventMessageType type)
        {
            if (!eventSubjects.TryGetValue(type, out var subject))
            {
                subject = new Subject<EventData>();
                eventSubjects[type] = subject;
            }
            return subject;
        }

        /// <summary>
        /// 获取或创建订阅追踪列表
        /// </summary>
        private List<SubscriptionEntry> GetOrCreateTracker(EventMessageType type)
        {
            if (!subscriptionTrackers.TryGetValue(type, out var tracker))
            {
                tracker = new List<SubscriptionEntry>(4);
                subscriptionTrackers[type] = tracker;
            }
            return tracker;
        }

        #region 公共 API（保持向后兼容）

        /// <summary>
        /// 添加事件监听
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void AddEventListener(EventMessageType type, Action<EventData> listenerFunc)
        {
            if (listenerFunc == null) return;

            var subject = GetOrCreateSubject(type);
            var tracker = GetOrCreateTracker(type);

            // 避免重复订阅
            for (int i = 0; i < tracker.Count; i++)
            {
                if (tracker[i].Handler == listenerFunc) return;
            }

            var disposable = subject.Subscribe(data => listenerFunc(data));
            tracker.Add(new SubscriptionEntry { Handler = listenerFunc, Disposable = disposable });
        }

        /// <summary>
        /// 删除事件监听
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void RemoveEventListener(EventMessageType type, Action<EventData> listenerFunc)
        {
            if (listenerFunc == null) return;

            if (subscriptionTrackers.TryGetValue(type, out var tracker))
            {
                for (int i = tracker.Count - 1; i >= 0; i--)
                {
                    if (tracker[i].Handler == listenerFunc)
                    {
                        tracker[i].Disposable?.Dispose();
                        tracker.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 泛型分发事件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="type">事件类型</param>
        /// <param name="data">事件数据</param>
        public void DispatchEvent<T>(EventMessageType eventType, T eventData)
        {
            if (eventSubjects.TryGetValue(eventType, out var subject))
            {
                subject.OnNext(new EventData
                {
                    type = eventType,
                    data = eventData
                });
            }
        }

        /// <summary>
        /// 分发事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        public void DispatchEvent(EventMessageType eventType, object eventData)
        {
            if (eventSubjects.TryGetValue(eventType, out var subject))
            {
                subject.OnNext(new EventData
                {
                    type = eventType,
                    data = eventData
                });
            }
        }

        /// <summary>
        /// 清除某一类型的事件监听器
        /// </summary>
        /// <param name="type">事件类型</param>
        public void ClearEventTypeListeners(EventMessageType type)
        {
            if (subscriptionTrackers.TryGetValue(type, out var tracker))
            {
                for (int i = 0; i < tracker.Count; i++)
                {
                    tracker[i].Disposable?.Dispose();
                }
                tracker.Clear();
                subscriptionTrackers.Remove(type);
            }

            if (eventSubjects.TryGetValue(type, out var subject))
            {
                subject?.Dispose();
                eventSubjects.Remove(type);
                Log.Debug("清除事件类型 {0} 的所有监听器", type);
            }
            else
            {
                Log.Warning("尝试清除不存在的事件类型 {0} 的监听器", type);
            }
        }

        /// <summary>
        /// 清除所有事件监听器（委托给 Clear()，保留方法以兼容旧调用方）
        /// </summary>
        public void ClearAllEventListeners()
        {
            Clear();
        }

        #endregion

        #region R3 原生 API（推荐新代码使用）

        /// <summary>
        /// 获取某个事件类型的 IObservable，支持 LINQ 操作符（推荐）
        /// </summary>
        /// <example>
        /// EventMessageSystem.Instance.AsObservable(EventMessageType.ButtonClick)
        ///     .Where(e => e.data is int id && id > 0)
        ///     .Subscribe(e => HandleClick(e));
        /// </example>
        /// <param name="type">事件类型</param>
        /// <returns>可观测序列</returns>
        public Observable<EventData> AsObservable(EventMessageType type)
        {
            return GetOrCreateSubject(type);
        }

        #endregion
    }
}