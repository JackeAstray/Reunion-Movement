using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util.Timer
{
    /// <summary>
    /// 通用计时器管理器（用于管理多个计时器实例）
    /// </summary>
    public class TimerMgr : SingletonMgr<TimerMgr>
    {
        private readonly List<Timer> timers = new List<Timer>();

        /// <summary>
        /// 创建并注册一个计时器
        /// </summary>
        /// <param name="duration">持续时间</param>
        /// <param name="isCountingDown">是否倒计时</param>
        /// <param name="isLoop">是否循环</param>
        /// <param name="maxLoop">最大循环次数</param>
        /// <returns></returns>
        public Timer CreateTimer(float duration, bool isCountingDown = true, bool isLoop = false, int maxLoop = 0)
        {
            var timer = new Timer(duration, isCountingDown, isLoop, maxLoop);
            timers.Add(timer);
            return timer;
        }

        /// <summary>
        /// 移除计时器
        /// </summary>
        /// <param name="timer"></param>
        public void RemoveTimer(Timer timer)
        {
            timers.Remove(timer);
        }

        /// <summary>
        /// 取消所有计时器
        /// </summary>
        public void CancelAllTimers()
        {
            foreach (var timer in timers.ToArray())
            {
                timer.Cancel();
            }
            timers.Clear();
        }

        /// <summary>
        /// 暂停所有计时器
        /// </summary>
        public void PauseAllTimers()
        {
            foreach (Timer timer in timers)
            {
                timer.Pause();
            }
        }

        /// <summary>
        /// 恢复所有计时器
        /// </summary>
        public void ResumeAllTimers()
        {
            foreach (Timer timer in timers)
            {
                timer.Resume();
            }
        }

        /// <summary>
        /// 更新所有计时器
        /// </summary>
        private void Update()
        {
            // 用ToArray防止遍历时移除
            foreach (var timer in timers.ToArray())
            {
                timer.Update(Time.deltaTime);
                // 自动移除已完成或取消的计时器（可选）
                if (timer.state == Timer.TimerState.Finished || timer.state == Timer.TimerState.Cancelled)
                {
                    timers.Remove(timer);
                }
            }
        }

        /// <summary>
        /// 可选：清空所有计时器
        /// </summary>
        public void ClearAll()
        {
            timers.Clear();
        }

        public void OnDestroy()
        {
            // 清理所有计时器
            ClearAll();
        }
    }
}