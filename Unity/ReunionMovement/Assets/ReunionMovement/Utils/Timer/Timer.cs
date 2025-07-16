using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util.Timer
{
    /// <summary>
    /// 通用计时器（支持正计时与倒计时，支持暂停、继续、取消）
    /// </summary>
    public class Timer
    {
        public enum TimerState { Idle, Running, Paused, Finished, Cancelled }

        // 总时长（秒）
        public float duration { get; private set; }
        // 已经过的时间（秒）
        public float elapsed { get; private set; }
        // 是否为倒计时
        public bool isCountingDown { get; private set; }
        // 时间缩放
        public float timeScale { get; set; } = 1f;
        // 是否循环
        public bool isLoop { get; private set; }
        // 已循环次数
        public int loopCount { get; private set; } = 0;
        // 0为无限循环
        public int maxLoop { get; private set; } = 0;
        // 当前状态
        public TimerState state { get; private set; } = TimerState.Idle;

        // 完成事件，当计时器到达结束时触发
        public event Action onCompleted;
        // 循环完成事件，当计时器每次循环结束时触发
        public event Action<int> onLoopCompleted;
        // 取消事件，只有在计时器被取消时触发，不会触发OnCompleted事件
        public event Action onCancelled;
        // 参数为当前已用时间或剩余时间
        public event Action<float> onTick;

        /// <summary>
        /// 创建一个新的计时器实例
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="isCountingDown"></param>
        public Timer(float duration, bool isCountingDown = true, bool isLoop = false, int maxLoop = 0)
        {
            this.duration = Math.Max(0, duration);
            this.isCountingDown = isCountingDown;
            this.isLoop = isLoop;
            this.maxLoop = maxLoop;
            elapsed = 0f;
        }

        /// <summary>
        /// 开始计时器，设置状态为Running，如果之前是Idle或Paused状态，则从0开始计时
        /// </summary>
        public void Start()
        {
            if (state == TimerState.Running) return;
            state = TimerState.Running;
            elapsed = 0f;
        }

        /// <summary>
        /// 暂停计时器，设置状态为Paused，如果之前是Running状态，则停止计时
        /// </summary>
        public void Pause()
        {
            if (state != TimerState.Running) return;
            state = TimerState.Paused;
        }

        /// <summary>
        /// 继续计时器，设置状态为Running，如果之前是Paused状态，则从暂停的时间继续计时
        /// </summary>
        public void Resume()
        {
            if (state != TimerState.Paused) return;
            state = TimerState.Running;
        }

        /// <summary>
        /// 取消计时器，设置状态为Cancelled，不会触发OnCompleted事件
        /// </summary>
        public void Cancel()
        {
            if (state == TimerState.Finished || state == TimerState.Cancelled) return;
            state = TimerState.Cancelled;
            onCancelled?.Invoke();
        }

        /// <summary>
        /// 重置计时器，设置状态为Idle，已用时间和循环次数归零
        /// </summary>
        public void Reset()
        {
            elapsed = 0f;
            state = TimerState.Idle;
            loopCount = 0;
        }

        /// <summary>
        /// 每帧调用，deltaTime为Time.deltaTime
        /// </summary>
        public void Update(float deltaTime)
        {
            if (state != TimerState.Running)
            {
                return;
            }

            elapsed += deltaTime * timeScale;

            float time = isCountingDown ? duration - elapsed : elapsed;

            onTick?.Invoke(time);

            if ((isCountingDown && time <= 0f) || (!isCountingDown && elapsed >= duration))
            {
                loopCount++;
                if (isLoop && (maxLoop == 0 || loopCount < maxLoop))
                {
                    elapsed = 0f;
                    onLoopCompleted?.Invoke(loopCount);
                }
                else
                {
                    state = TimerState.Finished;
                    onCompleted?.Invoke();
                }
            }
        }

        /// <summary>
        /// 获取当前进度（0-1），0表示开始，1表示结束
        /// </summary>
        /// <returns></returns>
        public float GetProgress()
        {
            return Math.Clamp(elapsed / duration, 0f, 1f);
        }

        /// <summary>
        /// 获取当前剩余时间（秒）
        /// </summary>
        public float GetRemainingTime()
        {
            return isCountingDown ? duration - elapsed : elapsed;
        }
    }
}
