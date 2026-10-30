using Gear.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

using Gear.Common;
namespace Gear.Framework.Modules.Timer
{
    /// <summary>
    /// 通用计时器管理器 —— 纯 C# 单例，由 GameEngine 每帧驱动（<see cref="ISystemUpdatable"/>）。
    /// <para>
    /// 不再依赖 MonoBehaviour Update：没有引擎时（工具/测试）可显式调用 <see cref="Tick"/>。
    /// 这样计时器逻辑不依赖场景与 PlayerLoop，也就能在没有 Unity 的环境里验证。
    /// </para>
    /// </summary>
    [ModuleOrder(80)]
    public sealed class TimerMgr : ICustomSystem, ISystemUpdatable, ISystemDisposable
    {
        private static Lazy<TimerMgr> instance = new Lazy<TimerMgr>(() => new TimerMgr());

        /// <summary>单例（懒创建，不创建 GameObject）。</summary>
        public static TimerMgr Instance => instance.Value;

        /// <summary>是否已创建实例（不会触发懒加载）。</summary>
        public static bool IsInitialized => instance.IsValueCreated;

        /// <summary>复位跨 Play 会话的静态实例（关闭 Domain Reload 时由 SingletonResets 调用）。</summary>
        public static void ResetStatics()
        {
            // 池化策略不跨 Play 会话保留：每次进入运行都从默认（关闭）开始，
            // 避免上一次会话里打开的开关悄悄影响下一次
            ReuseInstances = false;
            instance = new Lazy<TimerMgr>(() => new TimerMgr());
        }

        private TimerMgr() { }

        private readonly List<Timer> timers = new List<Timer>();
        // 复用快照/待移除缓冲：防止 Tick 回调内调用 RemoveTimer/CancelAllTimers
        // 重入修改 timers 导致遍历越界（同时保持零分配）
        private readonly List<Timer> tickSnapshot = new List<Timer>();
        private readonly Stack<Timer> pool = new Stack<Timer>();

        /// <summary>
        /// 待移除表：Timer → 标记时的租约号。用租约号而不是纯引用，
        /// 是因为回调里可能已经把同一个实例重新租出去（池化开启时），
        /// 这时不能把它从 timers 里删掉、更不能放回池子。
        /// </summary>
        private readonly Dictionary<Timer, int> toRemove = new Dictionary<Timer, int>();

        /// <summary>
        /// 是否复用已结束的计时器实例。默认关闭。
        /// <para>
        /// 关闭是有意的：池化会把同一个 <see cref="Timer"/> 实例重新租给下一次调用，
        /// 任何仍持有旧引用的代码（常见写法是 <c>timer.OnCompleted += ...</c> 之后又读
        /// <c>timer.loopCount</c>）会静默作用到新计时器上，属于最难查的一类 bug。
        /// 只有当确实在一帧内创建上百个短计时器、并且代码不跨租约持有引用时才建议开启。
        /// </para>
        /// <para>开启后归还的实例会清空全部事件订阅，所以旧回调不会误触发。</para>
        /// </summary>
        public static bool ReuseInstances { get; set; }

        /// <summary>池上限（防止峰值后长期占用内存）。</summary>
        private const int MaxPooledInstances = 64;

        private Timer RentTimer(float duration, bool countingDown, bool loop, int maxLoop)
        {
            if (ReuseInstances && pool.Count > 0)
            {
                Timer pooled = pool.Pop();
                pooled.ResetForPool(duration, countingDown, loop, maxLoop);
                return pooled;
            }
            return new Timer(duration, countingDown, loop, maxLoop);
        }

        private void ReturnTimer(Timer timer)
        {
            if (!ReuseInstances || timer == null) return;
            if (!timer.CanRecycle) return;
            if (pool.Count >= MaxPooledInstances) return;
            // 入池即清空订阅者：即使外部还拿着引用也不会再被回调
            timer.ResetForPool(0f, true, false, 0);
            pool.Push(timer);
        }

        #region ICustomSystem 实现（GameEngine 驱动时使用）

        private double initProgress = 0;
        public double InitProgress => initProgress;

        public UniTask Init()
        {
            initProgress = 100;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// GameEngine 驱动入口。logicTime 受 timeScale 影响，与旧版 MonoBehaviour.Update 语义一致。
        /// </summary>
        void ISystemUpdatable.Update(float logicTime, float realTime)
        {
            TickTimers(logicTime);
        }

        /// <summary>手动推进计时器（无引擎的工具/测试场景使用；正常游戏由 GameEngine 驱动）。</summary>
        public void Tick(float deltaTime)
        {
            TickTimers(deltaTime);
        }

        public void Clear()
        {
            CancelAllTimers();
        }

        #endregion

        /// <summary>
        /// 创建并注册一个计时器
        /// </summary>
        public Timer CreateTimer(float duration, bool isCountingDown = true, bool isLoop = false, int maxLoop = 0)
        {
            var timer = RentTimer(duration, isCountingDown, isLoop, maxLoop);
            timers.Add(timer);
            return timer;
        }

        /// <summary>移除计时器（先取消再移除；OnCancelled 异常隔离，不中断移除流程）</summary>
        public void RemoveTimer(Timer timer)
        {
            if (timer == null) return;
            try { timer.Cancel(); }
            catch (Exception ex) { Log.Error("[TimerMgr] RemoveTimer 取消回调异常（已隔离）: {0}", ex.Message); }
            if (timers.Remove(timer)) ReturnTimer(timer);
        }

        /// <summary>取消所有计时器（逐 timer 异常隔离：单个 OnCancelled 异常不得中断全局清理与列表清空）</summary>
        public void CancelAllTimers()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                try { timers[i].Cancel(); }
                catch (Exception ex) { Log.Error("[TimerMgr] CancelAllTimers 取消回调异常（已隔离）: {0}", ex.Message); }
                ReturnTimer(timers[i]);
            }
            timers.Clear();
        }

        /// <summary>暂停所有计时器</summary>
        public void PauseAllTimers()
        {
            for (int i = 0; i < timers.Count; i++)
                timers[i].Pause();
        }

        /// <summary>恢复所有计时器</summary>
        public void ResumeAllTimers()
        {
            for (int i = 0; i < timers.Count; i++)
                timers[i].Resume();
        }

        /// <summary>清空所有计时器（与 Clear() 语义一致：逐个 Cancel 触发 OnCancelled 后清空）</summary>
        public void ClearAll()
        {
            CancelAllTimers();
        }

        /// <summary>
        /// 更新所有计时器（倒序遍历，安全移除且零分配）。
        /// 同时被 MonoBehaviour Update 和 ICustomSystem.Update 调用。
        ///
        /// 使用快照遍历：回调（OnCompleted/OnTick）内可能调用 RemoveTimer/CancelAllTimers
        /// 重入修改 timers，直接遍历会越界或删错元素。
        /// </summary>
        private void TickTimers(float deltaTime)
        {
            if (timers.Count == 0) return;

            // 复用快照缓冲（零分配）
            tickSnapshot.Clear();
            tickSnapshot.AddRange(timers);
            toRemove.Clear();

            for (int i = 0; i < tickSnapshot.Count; i++)
            {
                var timer = tickSnapshot[i];
                try
                {
                    timer.Update(deltaTime);
                }
                catch (Exception ex)
                {
                    // 回调异常隔离：OnTick/OnCompleted 订阅者抛异常不中断本帧其余计时器，
                    // 且必须保证下方 Finished/Cancelled 标记与移除逻辑执行（否则完成计时器永久残留列表）
                    Log.Warning("TimerMgr: 计时器回调异常（已隔离并取消该计时器）: {0}", ex.Message);
                    try { timer.Cancel(); }
                    catch (Exception ex2) { Log.Warning("TimerMgr: OnCancelled 回调异常（已隔离）: {0}", ex2.Message); }
                }
                if (timer.state == Timer.TimerState.Finished || timer.state == Timer.TimerState.Cancelled)
                {
                    // 字典天然去重：快照中同一实例可能因回调重入被多次标记。
                    // 记下标记时的租约号，回收时用它确认"还是当初那个实例"
                    if (!toRemove.ContainsKey(timer)) toRemove.Add(timer, timer.Lease);
                }
            }

            // 遍历结束后统一移除（快照外新增的计时器不受影响）
            if (toRemove.Count > 0)
            {
                foreach (var pair in toRemove)
                {
                    Timer finished = pair.Key;
                    // 池化开启时，回调可能已把这个实例重新租出去（租约号变化）：
                    // 此时它是一条在跑的活计时器，既不能从列表里删，也不能放回池
                    if (finished.Lease != pair.Value) continue;
                    if (timers.Remove(finished)) ReturnTimer(finished);
                }
            }
        }

    }
}