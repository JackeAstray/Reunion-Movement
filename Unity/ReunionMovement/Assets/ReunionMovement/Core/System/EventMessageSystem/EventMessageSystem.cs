using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;

namespace ReunionMovement.Core.EventMessage
{
    /// <summary>
    /// 事件数据（struct 避免堆分配，但 object data 字段对值类型仍会产生装箱）。
    /// 若需完全零装箱，请使用 EventData&lt;T&gt; 泛型变体配合 AddEventListenerTyped / DispatchEventTyped。
    /// </summary>
    public readonly struct EventData
    {
        /// <summary>事件类型</summary>
        public readonly EventMessageType type;
        /// <summary>事件传递的数据（引用类型不装箱，值类型会发生装箱）</summary>
        public readonly object data;

        public EventData(EventMessageType type, object data)
        {
            this.type = type;
            this.data = data;
        }
    }

    /// <summary>
    /// 泛型事件数据 —— 真正零装箱（值类型不会被包装为 object）。
    /// 推荐新代码使用，搭配 AddEventListenerTyped / DispatchEventTyped。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public readonly struct EventData<T>
    {
        public readonly EventMessageType type;
        public readonly T data;

        public EventData(EventMessageType type, T data)
        {
            this.type = type;
            this.data = data;
        }
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

        /// <summary>订阅追踪 —— 每个事件类型对应 handler→IDisposable 映射，O(1) 查重与移除</summary>
        private readonly Dictionary<EventMessageType, Dictionary<Action<EventData>, IDisposable>> subscriptionTrackers
            = new Dictionary<EventMessageType, Dictionary<Action<EventData>, IDisposable>>();

        // ============================================================
        //  泛型零装箱通道（推荐新代码使用）
        //  使用 object 作为字典值存储不同类型的 Subject，运行时强转
        // ============================================================
        private readonly Dictionary<EventMessageType, object> typedSubjects
            = new Dictionary<EventMessageType, object>();
        private readonly Dictionary<EventMessageType, object> typedTrackers
            = new Dictionary<EventMessageType, object>();

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
                foreach (var disposable in kvp.Value.Values)
                {
                    disposable?.Dispose();
                }
                kvp.Value.Clear();
            }
            subscriptionTrackers.Clear();

            // 释放泛型零装箱通道的订阅追踪
            foreach (var obj in typedTrackers.Values)
            {
                if (obj is IDisposable disp) disp.Dispose();
            }
            typedTrackers.Clear();

            foreach (var kvp in eventSubjects)
            {
                kvp.Value?.Dispose();
            }
            eventSubjects.Clear();

            // 释放泛型零装箱 Subjects
            foreach (var obj in typedSubjects.Values)
            {
                if (obj is IDisposable disp) disp.Dispose();
            }
            typedSubjects.Clear();

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
        /// 获取或创建订阅追踪字典（handler → IDisposable，O(1) 查重与移除）
        /// </summary>
        private Dictionary<Action<EventData>, IDisposable> GetOrCreateTracker(EventMessageType type)
        {
            if (!subscriptionTrackers.TryGetValue(type, out var tracker))
            {
                tracker = new Dictionary<Action<EventData>, IDisposable>(4);
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

            // O(1) 查重，避免重复订阅同一 handler
            if (tracker.ContainsKey(listenerFunc)) return;

            var disposable = subject.Subscribe(data => listenerFunc(data));
            tracker[listenerFunc] = disposable;
        }

        /// <summary>
        /// 删除事件监听
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void RemoveEventListener(EventMessageType type, Action<EventData> listenerFunc)
        {
            if (listenerFunc == null) return;

            if (subscriptionTrackers.TryGetValue(type, out var tracker)
                && tracker.TryGetValue(listenerFunc, out var disposable))
            {
                disposable?.Dispose();
                tracker.Remove(listenerFunc);
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
                subject.OnNext(new EventData(eventType, eventData));
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
                subject.OnNext(new EventData(eventType, eventData));
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
                foreach (var disposable in tracker.Values)
                {
                    disposable?.Dispose();
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

        // ============================================================
        //  泛型零装箱 API（推荐新代码使用，值类型不会装箱）
        // ============================================================

        /// <summary>
        /// 获取或创建泛型 Subject（零装箱通道）。
        /// 使用 object 字典存储不同类型的 Subject&lt;EventData&lt;T&gt;&gt;，运行时强转。
        /// </summary>
        private Subject<EventData<T>> GetOrCreateTypedSubject<T>(EventMessageType type)
        {
            if (typedSubjects.TryGetValue(type, out var obj) && obj is Subject<EventData<T>> existing)
            {
                return existing;
            }
            var subject = new Subject<EventData<T>>();
            typedSubjects[type] = subject;
            return subject;
        }

        /// <summary>
        /// 获取或创建泛型订阅追踪字典（零装箱通道）。
        /// </summary>
        private Dictionary<Action<EventData<T>>, IDisposable> GetOrCreateTypedTracker<T>(EventMessageType type)
        {
            if (typedTrackers.TryGetValue(type, out var obj) && obj is Dictionary<Action<EventData<T>>, IDisposable> existing)
            {
                return existing;
            }
            var tracker = new Dictionary<Action<EventData<T>>, IDisposable>(4);
            typedTrackers[type] = tracker;
            return tracker;
        }

        /// <summary>
        /// 零装箱添加事件监听（值类型不会产生 GC 分配）。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void AddEventListenerTyped<T>(EventMessageType type, Action<EventData<T>> listenerFunc)
        {
            if (listenerFunc == null) return;

            var subject = GetOrCreateTypedSubject<T>(type);
            var tracker = GetOrCreateTypedTracker<T>(type);

            if (tracker.ContainsKey(listenerFunc)) return;

            var disposable = subject.Subscribe(data => listenerFunc(data));
            tracker[listenerFunc] = disposable;
        }

        /// <summary>
        /// 零装箱移除事件监听。
        /// </summary>
        public void RemoveEventListenerTyped<T>(EventMessageType type, Action<EventData<T>> listenerFunc)
        {
            if (listenerFunc == null) return;

            if (typedTrackers.TryGetValue(type, out var obj)
                && obj is Dictionary<Action<EventData<T>>, IDisposable> tracker
                && tracker.TryGetValue(listenerFunc, out var disposable))
            {
                disposable?.Dispose();
                tracker.Remove(listenerFunc);
            }
        }

        /// <summary>
        /// 零装箱分发事件（值类型不会装箱，推荐高频事件使用）。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        public void DispatchEventTyped<T>(EventMessageType eventType, T eventData)
        {
            if (typedSubjects.TryGetValue(eventType, out var obj)
                && obj is Subject<EventData<T>> subject)
            {
                subject.OnNext(new EventData<T>(eventType, eventData));
            }
        }

        /// <summary>
        /// 获取泛型事件的可观测序列（零装箱，支持 LINQ 操作符）。
        /// </summary>
        public Observable<EventData<T>> AsObservableTyped<T>(EventMessageType type)
        {
            return GetOrCreateTypedSubject<T>(type);
        }

        #endregion
    }
}