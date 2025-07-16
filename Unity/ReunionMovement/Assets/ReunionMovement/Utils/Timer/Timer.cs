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
        public float Duration { get; private set; }
        // 已经过的时间（秒）
        public float Elapsed { get; private set; }
        // 是否为倒计时
        public bool IsCountingDown { get; private set; }
        // 时间缩放
        public float TimeScale { get; set; } = 1f;
        // 是否循环
        public bool IsLoop { get; private set; }
        // 已循环次数
        public int LoopCount { get; private set; } = 0;
        // 0为无限循环
        public int MaxLoop { get; private set; } = 0;
        // 当前状态
        public TimerState State { get; private set; } = TimerState.Idle;

        // 完成事件，当计时器到达结束时触发
        public event Action OnCompleted;
        // 循环完成事件，当计时器每次循环结束时触发
        public event Action<int> OnLoopCompleted;
        // 取消事件，只有在计时器被取消时触发，不会触发OnCompleted事件
        public event Action OnCancelled;
        // 参数为当前已用时间或剩余时间
        public event Action<float> OnTick;

        /// <summary>
        /// 创建一个新的计时器实例
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="isCountingDown"></param>
        public Timer(float duration, bool isCountingDown = true, bool isLoop = false, int maxLoop = 0)
        {
            Duration = Math.Max(0, duration);
            IsCountingDown = isCountingDown;
            IsLoop = isLoop;
            MaxLoop = maxLoop;
            Elapsed = 0f;
        }

        /// <summary>
        /// 开始计时器，设置状态为Running，如果之前是Idle或Paused状态，则从0开始计时
        /// </summary>
        public void Start()
        {
            if (State == TimerState.Running) return;
            State = TimerState.Running;
            Elapsed = 0f;
        }

        /// <summary>
        /// 暂停计时器，设置状态为Paused，如果之前是Running状态，则停止计时
        /// </summary>
        public void Pause()
        {
            if (State != TimerState.Running) return;
            State = TimerState.Paused;
        }

        /// <summary>
        /// 继续计时器，设置状态为Running，如果之前是Paused状态，则从暂停的时间继续计时
        /// </summary>
        public void Resume()
        {
            if (State != TimerState.Paused) return;
            State = TimerState.Running;
        }

        /// <summary>
        /// 取消计时器，设置状态为Cancelled，不会触发OnCompleted事件
        /// </summary>
        public void Cancel()
        {
            if (State == TimerState.Finished || State == TimerState.Cancelled) return;
            State = TimerState.Cancelled;
            OnCancelled?.Invoke();
        }

        /// <summary>
        /// 重置计时器，设置状态为Idle，已用时间和循环次数归零
        /// </summary>
        public void Reset()
        {
            Elapsed = 0f;
            State = TimerState.Idle;
            LoopCount = 0;
        }

        /// <summary>
        /// 每帧调用，deltaTime为Time.deltaTime
        /// </summary>
        public void Update(float deltaTime)
        {
            if (State != TimerState.Running)
            {
                return;
            }

            Elapsed += deltaTime * TimeScale;

            float time = IsCountingDown ? Duration - Elapsed : Elapsed;

            OnTick?.Invoke(time);

            if ((IsCountingDown && time <= 0f) || (!IsCountingDown && Elapsed >= Duration))
            {
                LoopCount++;
                if (IsLoop && (MaxLoop == 0 || LoopCount < MaxLoop))
                {
                    Elapsed = 0f;
                    OnLoopCompleted?.Invoke(LoopCount);
                }
                else
                {
                    State = TimerState.Finished;
                    OnCompleted?.Invoke();
                }
            }
        }

        /// <summary>
        /// 获取当前进度（0-1），0表示开始，1表示结束
        /// </summary>
        /// <returns></returns>
        public float GetProgress()
        {
            return Math.Clamp(Elapsed / Duration, 0f, 1f);
        }

        /// <summary>
        /// 获取当前剩余时间（秒）
        /// </summary>
        public float GetRemainingTime()
        {
            return IsCountingDown ? Duration - Elapsed : Elapsed;
        }
    }
}
