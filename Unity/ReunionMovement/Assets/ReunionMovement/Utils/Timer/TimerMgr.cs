using ReunionMovement.Core;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util.Timer
{
    /// <summary>
    /// 通用计时器管理器 —— 同时作为 MonoBehaviour 单例（独立场景）和 ICustomSystem（GameEngine 驱动）。
    /// </summary>
    public class TimerMgr : SingletonMgr<TimerMgr>, ICustomSystem, ISystemUpdatable, ISystemDisposable
    {
        /// <summary>
        /// 已注册为 GameEngine 模块（StartGame.CreateModules），由持久化的引擎每帧驱动；
        /// 必须跨场景存活，否则引擎持有的引用会失效（fake null）。
        /// </summary>
        protected override bool IsPersistentAcrossScenes => true;

        private readonly List<Timer> timers = new List<Timer>();
        // 复用快照/待移除缓冲：防止 Tick 回调内调用 RemoveTimer/CancelAllTimers
        // 重入修改 timers 导致遍历越界（同时保持零分配）
        private readonly List<Timer> tickSnapshot = new List<Timer>();
        private readonly List<Timer> toRemove = new List<Timer>();

        #region ICustomSystem 实现（GameEngine 驱动时使用）

        private double initProgress = 0;
        public double InitProgress => initProgress;

        public UniTask Init()
        {
            initProgress = 100;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// GameEngine 驱动的 Update（与 MonoBehaviour Update 逻辑相同，避免双重调用）
        /// </summary>
        void ISystemUpdatable.Update(float logicTime, float realTime)
        {
            TickTimers(logicTime);
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
            var timer = new Timer(duration, isCountingDown, isLoop, maxLoop);
            timers.Add(timer);
            return timer;
        }

        /// <summary>移除计时器（先取消再移除，确保回调被正确清理）</summary>
        public void RemoveTimer(Timer timer)
        {
            timer?.Cancel();
            timers.Remove(timer);
        }

        /// <summary>取消所有计时器</summary>
        public void CancelAllTimers()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                timers[i].Cancel();
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
                    // 防御：快照中同一实例可能因回调重入被多次标记，Contains 防止重复添加
                    if (!toRemove.Contains(timer))
                    {
                        toRemove.Add(timer);
                    }
                }
            }

            // 遍历结束后统一移除（引用移除，快照外新增的计时器不受影响）
            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    timers.Remove(toRemove[i]);
                }
            }
        }

        /// <summary>MonoBehaviour Update（独立场景兜底）</summary>
        private void Update()
        {
            // 仅在 GameEngine 未运行时使用 MonoBehaviour Update 驱动
            // GameEngine 运行时会通过 ISystemUpdatable.Update 驱动，避免双重调用
            if (!ModuleRuntime.IsEngineRunning)
            {
                TickTimers(Time.deltaTime);
            }
        }

        protected override void OnDestroy()
        {
            CancelAllTimers();
            base.OnDestroy();
        }
    }
}