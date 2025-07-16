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
    public class TimerMgr : MonoBehaviour
    {
        private readonly List<Timer> timers = new List<Timer>();

        // 创建并注册一个计时器
        public Timer CreateTimer(float duration, bool isCountingDown = true, bool isLoop = false, int maxLoop = 0)
        {
            var timer = new Timer(duration, isCountingDown, isLoop, maxLoop);
            timers.Add(timer);
            return timer;
        }

        // 移除计时器
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

        // 更新所有计时器
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

        // 可选：清空所有计时器
        public void ClearAll()
        {
            timers.Clear();
        }

        public void OnDestroy()
        {
            // 清理所有计时器
            ClearAll();
        }

        /// <summary>
        /// 示例：创建一个计时器并注册事件
        /// </summary>

        [ContextMenu("Example")]
        public void Example()
        {
            // 创建一个计时器，5秒后完成
            Timer timer = CreateTimer(5f, true);
            timer.onCompleted += () => Debug.Log("Timer 1 completed!");
            timer.onTick += (elapsed) => Debug.Log($"1 Elapsed time: {elapsed} seconds");
            // 启动计时器
            timer.Start();

            // 创建一个计时器，5秒后完成
            Timer timer2 = CreateTimer(5f, false);
            timer2.onCompleted += () => Debug.Log("Timer 2 completed!");
            timer2.onTick += (elapsed) => Debug.Log($"2 Elapsed time: {elapsed} seconds");
            // 启动计时器
            timer2.Start();
        }
    }
}
