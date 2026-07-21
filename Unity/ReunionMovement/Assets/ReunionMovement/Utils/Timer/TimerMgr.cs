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
    public class TimerMgr : SingletonMgr<TimerMgr>, ICustomSystem
    {
        private readonly List<Timer> timers = new List<Timer>();

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

        /// <summary>可选：清空所有计时器</summary>
        public void ClearAll()
        {
            timers.Clear();
        }

        /// <summary>
        /// 更新所有计时器（倒序遍历，安全移除且零分配）。
        /// 同时被 MonoBehaviour Update 和 ICustomSystem.Update 调用。
        /// </summary>
        private void TickTimers(float deltaTime)
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                var timer = timers[i];
                timer.Update(deltaTime);
                if (timer.state == Timer.TimerState.Finished || timer.state == Timer.TimerState.Cancelled)
                {
                    timers.RemoveAt(i);
                }
            }
        }

        /// <summary>MonoBehaviour Update（独立场景兜底）</summary>
        private void Update()
        {
            // 仅在 GameEngine 未运行时使用 MonoBehaviour Update 驱动
            // GameEngine 运行时会通过 ISystemUpdatable.Update 驱动，避免双重调用
            if (GameEngine.Current == null || GameEngine.Current.State != EngineState.Running)
            {
                TickTimers(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            CancelAllTimers();
        }
    }
}