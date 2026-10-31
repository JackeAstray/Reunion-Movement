using Gear.Common;
using R3;
using System;
using System.Collections.Generic;

namespace Gear.Framework.Runtime.EventMessage
{
    /// <summary>
    /// <see cref="EventMessageSystem"/> 的泛型零装箱通道（按职责从主文件拆出，避免单文件继续膨胀）：
    /// 订阅/取消/分发/AsObservable，键为 (EventMessageType, T)，同一事件类型可绑定不同数据类型。
    /// </summary>
    public partial class EventMessageSystem
    {
        #region 泛型零装箱 API（推荐新代码使用，值类型不会装箱）

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
            try
            {
                ((TypedDispatcher<T>)dispatcher).DispatchTyped(eventType, eventData);
            }
            catch (Exception ex)
            {
                // AsObservableTyped 的订阅者异常不能传到 DispatchEventTyped 的调用方
                Log.Error("EventMessageSystem 泛型事件 {0}/{1} 的订阅者异常（已隔离）: {2}",
                    eventType, typeof(T).Name, ex.Message);
            }
        }

        /// <summary>泛型通道核心分发（后台线程出队路径，object → 强类型）</summary>
        private void DispatchTypedCore((EventMessageType, System.Type) key, object data)
        {
            ITypedDispatcher dispatcher;
            lock (syncGate)
            {
                if (!typedDispatchers.TryGetValue(key, out dispatcher)) return;
            }
            try
            {
                dispatcher.Dispatch(key.Item1, data);
            }
            catch (Exception ex)
            {
                // 同 DispatchEventCore：AsObservableTyped 的订阅者异常不能传到调用方
                Log.Error("EventMessageSystem 泛型事件 {0}/{1} 的订阅者异常（已隔离）: {2}",
                    key.Item1, key.Item2?.Name, ex.Message);
            }
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
}
