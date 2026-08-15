using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

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
    /// 事件消息系统 —— 基于 R3 Subject&lt;T&gt; 的类型安全事件总线。
    /// 主线程分发 + 后台线程自动入队（ISystemUpdatable.Update 排空），
    /// 网络/下载回调线程可直接 DispatchEvent 而无需手动切主线程。
    /// </summary>
    public class EventMessageSystem : ICustomSystem, ISystemDisposable, ISystemUpdatable
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
        //  使用复合键 (EventMessageType, Type)：同一事件类型可绑定不同数据类型而不会互相覆盖，
        //  避免静默丢弃旧 Subject 导致事件丢失
        // ============================================================
        private readonly Dictionary<(EventMessageType, System.Type), object> typedSubjects
            = new Dictionary<(EventMessageType, System.Type), object>();
        private readonly Dictionary<(EventMessageType, System.Type), object> typedTrackers
            = new Dictionary<(EventMessageType, System.Type), object>();

        /// <summary>
        /// 字典访问锁：网络/下载回调线程可能 DispatchEvent，主线程同时订阅/清除会损坏字典。
        /// 只保护字典结构，OnNext 在锁外执行（R3 Subject 内部自带并发保护）。
        /// </summary>
        private readonly object syncGate = new object();

        // ============================================================
        //  主线程投递队列（后台线程 DispatchEvent 自动入队，Update 排空）
        // ============================================================
        private readonly struct QueuedEvent
        {
            public readonly EventMessageType type;
            public readonly EventData data;
            public QueuedEvent(EventMessageType type, EventData data) { this.type = type; this.data = data; }
        }

        private readonly struct QueuedTypedEvent
        {
            public readonly (EventMessageType, System.Type) key;
            public readonly object data;
            public QueuedTypedEvent((EventMessageType, System.Type) key, object data) { this.key = key; this.data = data; }
        }

        private readonly ConcurrentQueue<QueuedEvent> pendingEvents = new ConcurrentQueue<QueuedEvent>();
        private readonly ConcurrentQueue<QueuedTypedEvent> pendingTypedEvents = new ConcurrentQueue<QueuedTypedEvent>();

        // ============================================================
        //  泛型通道调度器抽象：主线程路径通过 DispatchTyped<T>(T) 零装箱直投，
        //  后台线程入队路径通过 Dispatch(object) 在出队时转回强类型（仅入队时装箱一次）
        // ============================================================
        private interface ITypedDispatcher
        {
            void Dispatch(EventMessageType type, object data);
        }

        private sealed class TypedDispatcher<T> : ITypedDispatcher
        {
            private readonly Subject<EventData<T>> subject;
            public TypedDispatcher(Subject<EventData<T>> subject) { this.subject = subject; }

            /// <summary>主线程直投：T 不装箱</summary>
            public void DispatchTyped(EventMessageType type, T data)
            {
                subject.OnNext(new EventData<T>(type, data));
            }

            /// <summary>后台线程出队路径：object 转回强类型</summary>
            void ITypedDispatcher.Dispatch(EventMessageType type, object data)
            {
                subject.OnNext(new EventData<T>(type, (T)data));
            }
        }

        /// <summary>泛型通道的主线程调度器（key → 调度器），与 typedSubjects 同生命周期</summary>
        private readonly Dictionary<(EventMessageType, System.Type), ITypedDispatcher> typedDispatchers
            = new Dictionary<(EventMessageType, System.Type), ITypedDispatcher>();

        /// <summary>待分发队列上限（防止系统停摆时后台事件无限堆积）</summary>
        private const int MaxPendingEvents = 1024;

        /// <summary>主线程 ID（Init 记录；0=未初始化，视为主线程以保持旧同步语义）</summary>
        private int mainThreadId;

        private bool IsMainThread => mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == mainThreadId;

        /// <summary>包装单个监听器：异常只影响自身，不中断后续订阅者（Subject.OnNext 同步传播异常会中断订阅链）</summary>
        private static void SafeInvoke(EventMessageType type, Action<EventData> listener, EventData data)
        {
            try
            {
                listener(data);
            }
            catch (Exception ex)
            {
                Log.Error("EventMessageSystem 监听器异常（已隔离，不影响其他订阅者）: {0}, {1}", type, ex.Message);
            }
        }

        /// <summary>泛型通道的单监听器异常隔离（与 SafeInvoke 对应）</summary>
        private static void SafeInvokeTyped<T>(EventMessageType type, Action<EventData<T>> listener, EventData<T> data)
        {
            try
            {
                listener(data);
            }
            catch (Exception ex)
            {
                Log.Error("EventMessageSystem 监听器异常（已隔离，不影响其他订阅者）: {0}, {1}", type, ex.Message);
            }
        }

        public UniTask Init()
        {
            initProgress = 100;
            isInited = true;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Log.Debug("EventMessageSystem 初始化完成 (R3 + 主线程投递队列)");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 主线程排空投递队列（由 GameEngine 以 ISystemUpdatable 驱动）。
        /// 后台线程入队的事件在此按序同步分发，订阅者运行在主线程。
        /// </summary>
        public void Update(float logicTime, float realTime)
        {
            // 每帧分发预算：订阅者处理中“再入队”新事件时，防止单帧无限分发卡死主循环，
            // 余量留待下一帧处理（事件顺序仍由队列保证）
            const int MaxDispatchPerFrame = 1024;

            int budget = MaxDispatchPerFrame;
            while (budget-- > 0 && pendingEvents.TryDequeue(out var e))
            {
                DispatchEventCore(e.type, e.data);
            }
            budget = MaxDispatchPerFrame;
            while (budget-- > 0 && pendingTypedEvents.TryDequeue(out var e))
            {
                DispatchTypedCore(e.key, e.data);
            }
        }

        public void Clear()
        {
            Log.Debug("EventMessageSystem 清除数据");

            lock (syncGate)
            {
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
                // typedTrackers 的 value 是 Dictionary<Action<EventData<T>>, IDisposable>，
                // 必须遍历内层字典的 Values 逐一 Dispose，不能直接将 Dictionary 当作 IDisposable。
                foreach (var obj in typedTrackers.Values)
                {
                    if (obj is System.Collections.IDictionary dict)
                    {
                        foreach (var disposable in dict.Values)
                        {
                            if (disposable is IDisposable disp) disp.Dispose();
                        }
                        dict.Clear();
                    }
                }
                typedTrackers.Clear();

                foreach (var kvp in eventSubjects)
                {
                    // OnCompleted 而非 Dispose：通知旧订阅者流结束。
                    // 已完成 Subject 再 Subscribe 不会抛 ObjectDisposedException（Dispose 会抛）。
                    kvp.Value?.OnCompleted();
                }
                eventSubjects.Clear();

                // 释放泛型零装箱 Subjects（泛型无法直接调用 OnCompleted，保持 Dispose；
                // 通过 AddEventListenerTyped 追踪的订阅已在上方释放）
                foreach (var obj in typedSubjects.Values)
                {
                    if (obj is IDisposable disp) disp.Dispose();
                }
                typedSubjects.Clear();
                typedDispatchers.Clear();

                // 清空待投递队列（避免残留闭包引用旧数据）
                while (pendingEvents.TryDequeue(out _)) { }
                while (pendingTypedEvents.TryDequeue(out _)) { }
            }

            isInited = false;
        }

        /// <summary>
        /// 获取或创建指定事件类型的 Subject
        /// </summary>
        private Subject<EventData> GetOrCreateSubject(EventMessageType type)
        {
            lock (syncGate)
            {
                if (!eventSubjects.TryGetValue(type, out var subject))
                {
                    subject = new Subject<EventData>();
                    eventSubjects[type] = subject;
                }
                return subject;
            }
        }

        /// <summary>
        /// 获取或创建订阅追踪字典（handler → IDisposable，O(1) 查重与移除）
        /// </summary>
        private Dictionary<Action<EventData>, IDisposable> GetOrCreateTracker(EventMessageType type)
        {
            lock (syncGate)
            {
                if (!subscriptionTrackers.TryGetValue(type, out var tracker))
                {
                    tracker = new Dictionary<Action<EventData>, IDisposable>(4);
                    subscriptionTrackers[type] = tracker;
                }
                return tracker;
            }
        }

        #region 公共 API（保持向后兼容）

        /// <summary>
        /// 添加事件监听。返回订阅句柄：Dispose 即取消订阅（与 RemoveEventListener 等价），
        /// 配合 using 声明或 R3 CompositeDisposable 自动管理生命周期，无需手动 Remove。
        /// 重复订阅同一 handler 被拒绝时返回默认句柄（Dispose 无操作，不会误删首次订阅）。
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public EventSubscription AddEventListener(EventMessageType type, Action<EventData> listenerFunc)
        {
            if (listenerFunc == null) return default;

            lock (syncGate)
            {
                var subject = GetOrCreateSubject(type);
                var tracker = GetOrCreateTracker(type);

                // O(1) 查重，避免重复订阅同一 handler
                if (tracker.ContainsKey(listenerFunc)) return default;

                // 逐监听器异常隔离：SafeInvoke 保证单个坏监听器不中断其他订阅者
                var disposable = subject.Subscribe(data => SafeInvoke(type, listenerFunc, data));
                tracker[listenerFunc] = disposable;
                return new EventSubscription(this, type, listenerFunc, disposable);
            }
        }

        /// <summary>
        /// 取消由 EventSubscription 句柄持有的订阅（内部使用）。
        /// 校验 disposable 引用一致性：句柄过期（重复订阅被拒/已移除）时 Dispose 无操作，
        /// 不会误删同一 handler 的其他订阅。
        /// </summary>
        internal void RemoveSubscription(EventMessageType type, Action<EventData> listener, IDisposable expected)
        {
            if (listener == null) return;
            lock (syncGate)
            {
                if (subscriptionTrackers.TryGetValue(type, out var tracker)
                    && tracker.TryGetValue(listener, out var current)
                    && ReferenceEquals(current, expected))
                {
                    current?.Dispose();
                    tracker.Remove(listener);
                }
            }
        }

        /// <summary>
        /// 删除事件监听
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public void RemoveEventListener(EventMessageType type, Action<EventData> listenerFunc)
        {
            if (listenerFunc == null) return;

            lock (syncGate)
            {
                if (subscriptionTrackers.TryGetValue(type, out var tracker)
                    && tracker.TryGetValue(listenerFunc, out var disposable))
                {
                    disposable?.Dispose();
                    tracker.Remove(listenerFunc);
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
            DispatchEvent(eventType, (object)eventData);
        }

        /// <summary>
        /// 分发事件（主线程同步分发；后台线程自动入队由主线程 Update 分发）。
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        public void DispatchEvent(EventMessageType eventType, object eventData)
        {
            if (IsMainThread)
            {
                DispatchEventCore(eventType, new EventData(eventType, eventData));
                return;
            }
            // 后台线程：入队待主线程分发（避免非主线程触碰 Unity 对象/字典）
            if (pendingEvents.Count >= MaxPendingEvents)
            {
                pendingEvents.TryDequeue(out _);
            }
            pendingEvents.Enqueue(new QueuedEvent(eventType, new EventData(eventType, eventData)));
        }

        /// <summary>主通道核心分发（调用方已确认主线程；订阅者由 SafeInvoke 隔离，异常不传播）</summary>
        private void DispatchEventCore(EventMessageType eventType, EventData data)
        {
            Subject<EventData> subject;
            lock (syncGate)
            {
                if (!eventSubjects.TryGetValue(eventType, out subject)) return;
            }
            subject.OnNext(data);
        }

        /// <summary>
        /// 清除某一类型的事件监听器
        /// </summary>
        /// <param name="type">事件类型</param>
        public void ClearEventTypeListeners(EventMessageType type)
        {
            bool removedAny = false;
            lock (syncGate)
            {
                if (subscriptionTrackers.TryGetValue(type, out var tracker))
                {
                    foreach (var disposable in tracker.Values)
                    {
                        disposable?.Dispose();
                    }
                    tracker.Clear();
                    subscriptionTrackers.Remove(type);
                    removedAny = true;
                }

                if (eventSubjects.TryGetValue(type, out var subject))
                {
                    // 与 Clear() 语义一致：OnCompleted 而非 Dispose。
                    // 外部持有旧 AsObservable 引用再 Subscribe 不会抛 ObjectDisposedException
                    subject?.OnCompleted();
                    eventSubjects.Remove(type);
                    Log.Debug("清除事件类型 {0} 的所有监听器", type);
                    removedAny = true;
                }

                // 泛型零装箱通道（typedTrackers/typedSubjects/typedDispatchers 以 (type, T) 为键）：
                // 必须同步清理，否则调用方以为清空后 typed 监听仍在派发（已销毁对象被回调）
                var typedKeys = new HashSet<(EventMessageType, System.Type)>();
                foreach (var kvp in typedTrackers)      if (kvp.Key.Item1 == type) typedKeys.Add(kvp.Key);
                foreach (var kvp in typedSubjects)      if (kvp.Key.Item1 == type) typedKeys.Add(kvp.Key);
                foreach (var kvp in typedDispatchers)   if (kvp.Key.Item1 == type) typedKeys.Add(kvp.Key);
                foreach (var key in typedKeys)
                {
                    if (typedTrackers.TryGetValue(key, out var trackerObj)
                        && trackerObj is System.Collections.IDictionary dict)
                    {
                        foreach (var v in dict.Values)
                        {
                            if (v is IDisposable disp) disp.Dispose();
                        }
                        dict.Clear();
                    }
                    typedTrackers.Remove(key);

                    if (typedSubjects.TryGetValue(key, out var subjectObj))
                    {
                        // 与 Clear() 保持一致：object 无法泛型 OnCompleted，直接 Dispose
                        if (subjectObj is IDisposable disp) disp.Dispose();
                        typedSubjects.Remove(key);
                    }
                    typedDispatchers.Remove(key);
                    removedAny = true;
                }
            }

            // 仅当全部字典都不存在该类型时才告警（避免误报）
            if (!removedAny)
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
            // 返回只读包装：外部只能 Subscribe，不能 OnNext；
            // 且 Clear 后旧引用（已完成 Subject）Subscribe 不会抛 ObjectDisposedException
            return GetOrCreateSubject(type).AsObservable();
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
            var key = (type, typeof(T));
            lock (syncGate)
            {
                if (typedSubjects.TryGetValue(key, out var obj) && obj is Subject<EventData<T>> existing)
                {
                    return existing;
                }
                var subject = new Subject<EventData<T>>();
                typedSubjects[key] = subject;
                // 注册主线程调度器：后台线程入队的 object 数据在主线程转回强类型并分发
                typedDispatchers[key] = new TypedDispatcher<T>(subject);
                return subject;
            }
        }

        /// <summary>
        /// 获取或创建泛型订阅追踪字典（零装箱通道）。
        /// </summary>
        private Dictionary<Action<EventData<T>>, IDisposable> GetOrCreateTypedTracker<T>(EventMessageType type)
        {
            var key = (type, typeof(T));
            lock (syncGate)
            {
                if (typedTrackers.TryGetValue(key, out var obj) && obj is Dictionary<Action<EventData<T>>, IDisposable> existing)
                {
                    return existing;
                }
                var tracker = new Dictionary<Action<EventData<T>>, IDisposable>(4);
                typedTrackers[key] = tracker;
                return tracker;
            }
        }

        /// <summary>
        /// 零装箱添加事件监听（值类型不会产生 GC 分配）。
        /// 返回订阅句柄：Dispose 即取消订阅（与 RemoveEventListenerTyped 等价）。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="type">事件类型</param>
        /// <param name="listenerFunc">监听函数</param>
        public TypedEventSubscription<T> AddEventListenerTyped<T>(EventMessageType type, Action<EventData<T>> listenerFunc)
        {
            if (listenerFunc == null) return default;

            lock (syncGate)
            {
                var subject = GetOrCreateTypedSubject<T>(type);
                var tracker = GetOrCreateTypedTracker<T>(type);

                if (tracker.ContainsKey(listenerFunc)) return default;

                // 逐监听器异常隔离（与主通道一致）
                var disposable = subject.Subscribe(data => SafeInvokeTyped(type, listenerFunc, data));
                tracker[listenerFunc] = disposable;
                return new TypedEventSubscription<T>(this, type, listenerFunc, disposable);
            }
        }

        /// <summary>
        /// 取消由 TypedEventSubscription 句柄持有的泛型订阅（内部使用，校验引用一致性防误删）。
        /// </summary>
        internal void RemoveSubscriptionTyped<T>(EventMessageType type, Action<EventData<T>> listener, IDisposable expected)
        {
            if (listener == null) return;
            var key = (type, typeof(T));
            lock (syncGate)
            {
                if (typedTrackers.TryGetValue(key, out var obj)
                    && obj is Dictionary<Action<EventData<T>>, IDisposable> tracker
                    && tracker.TryGetValue(listener, out var current)
                    && ReferenceEquals(current, expected))
                {
                    current?.Dispose();
                    tracker.Remove(listener);
                }
            }
        }

        /// <summary>
        /// 零装箱移除事件监听。
        /// </summary>
        public void RemoveEventListenerTyped<T>(EventMessageType type, Action<EventData<T>> listenerFunc)
        {
            if (listenerFunc == null) return;

            var key = (type, typeof(T));
            lock (syncGate)
            {
                if (typedTrackers.TryGetValue(key, out var obj)
                    && obj is Dictionary<Action<EventData<T>>, IDisposable> tracker
                    && tracker.TryGetValue(listenerFunc, out var disposable))
                {
                    disposable?.Dispose();
                    tracker.Remove(listenerFunc);
                }
            }
        }

        /// <summary>
        /// 零装箱分发事件（主线程路径 T 不装箱；后台线程仅在入队时装箱一次）。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        public void DispatchEventTyped<T>(EventMessageType eventType, T eventData)
        {
            if (IsMainThread)
            {
                DispatchTypedMain(eventType, eventData);
                return;
            }
            // 后台线程：入队待主线程分发（此处装箱一次，入队后由 TypedDispatcher 转回强类型）
            if (pendingTypedEvents.Count >= MaxPendingEvents)
            {
                pendingTypedEvents.TryDequeue(out _);
            }
            pendingTypedEvents.Enqueue(new QueuedTypedEvent((eventType, typeof(T)), eventData));
        }

        /// <summary>泛型通道主线程直投：DispatchTyped(T) 无装箱</summary>
        private void DispatchTypedMain<T>(EventMessageType eventType, T eventData)
        {
            ITypedDispatcher dispatcher;
            lock (syncGate)
            {
                if (!typedDispatchers.TryGetValue((eventType, typeof(T)), out dispatcher)) return;
            }
            ((TypedDispatcher<T>)dispatcher).DispatchTyped(eventType, eventData);
        }

        /// <summary>泛型通道核心分发（后台线程出队路径，object → 强类型）</summary>
        private void DispatchTypedCore((EventMessageType, System.Type) key, object data)
        {
            ITypedDispatcher dispatcher;
            lock (syncGate)
            {
                if (!typedDispatchers.TryGetValue(key, out dispatcher)) return;
            }
            dispatcher.Dispatch(key.Item1, data);
        }

        /// <summary>
        /// 获取泛型事件的可观测序列（零装箱，支持 LINQ 操作符）。
        /// </summary>
        public Observable<EventData<T>> AsObservableTyped<T>(EventMessageType type)
        {
            return GetOrCreateTypedSubject<T>(type).AsObservable();
        }

        #endregion
    }

    /// <summary>
    /// 事件订阅句柄（struct 零分配）：Dispose 即取消订阅，等价 RemoveEventListener。
    /// 支持 using 声明（C# 8+）：`using var sub = EventMessageSystem.Instance.AddEventListener(...);`
    /// 默认值（default）/过期句柄 Dispose 无操作，不会误删同一 handler 的其他订阅。
    /// </summary>
    public readonly struct EventSubscription : IDisposable
    {
        private readonly EventMessageSystem system;
        private readonly EventMessageType type;
        private readonly Action<EventData> listener;
        private readonly IDisposable disposable;

        internal EventSubscription(EventMessageSystem system, EventMessageType type, Action<EventData> listener, IDisposable disposable)
        {
            this.system = system;
            this.type = type;
            this.listener = listener;
            this.disposable = disposable;
        }

        public void Dispose()
        {
            system?.RemoveSubscription(type, listener, disposable);
        }
    }

    /// <summary>
    /// 泛型事件订阅句柄（struct 零分配）：Dispose 即取消订阅，等价 RemoveEventListenerTyped。
    /// </summary>
    public readonly struct TypedEventSubscription<T> : IDisposable
    {
        private readonly EventMessageSystem system;
        private readonly EventMessageType type;
        private readonly Action<EventData<T>> listener;
        private readonly IDisposable disposable;

        internal TypedEventSubscription(EventMessageSystem system, EventMessageType type, Action<EventData<T>> listener, IDisposable disposable)
        {
            this.system = system;
            this.type = type;
            this.listener = listener;
            this.disposable = disposable;
        }

        public void Dispose()
        {
            system?.RemoveSubscriptionTyped(type, listener, disposable);
        }
    }
}