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
        /// 移除计时器（先取消再移除，确保回调被正确清理）
        /// </summary>
        /// <param name="timer"></param>
        public void RemoveTimer(Timer timer)
        {
            timer?.Cancel();
            timers.Remove(timer);
        }

        /// <summary>
        /// 取消所有计时器
        /// </summary>
        public void CancelAllTimers()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                timers[i].Cancel();
            }
            timers.Clear();
        }

        /// <summary>
        /// 暂停所有计时器
        /// </summary>
        public void PauseAllTimers()
        {
            for (int i = 0; i < timers.Count; i++)
            {
                timers[i].Pause();
            }
        }

        /// <summary>
        /// 恢复所有计时器
        /// </summary>
        public void ResumeAllTimers()
        {
            for (int i = 0; i < timers.Count; i++)
            {
                timers[i].Resume();
            }
        }

        /// <summary>
        /// 更新所有计时器（倒序遍历，安全移除且零分配）
        /// </summary>
        private void Update()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                var timer = timers[i];
                timer.Update(Time.deltaTime);
                if (timer.state == Timer.TimerState.Finished || timer.state == Timer.TimerState.Cancelled)
                {
                    timers.RemoveAt(i);
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
            // 取消所有计时器（触发 OnCancelled 回调，让订阅者清理引用）
            CancelAllTimers();
        }
    }
}