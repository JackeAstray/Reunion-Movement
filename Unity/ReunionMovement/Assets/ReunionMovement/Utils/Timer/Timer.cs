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
        // 时间缩放（钳制负值：负值会使计时倒退永不完成）
        private float _timeScale = 1f;
        public float timeScale
        {
            get => _timeScale;
            set => _timeScale = value < 0f ? 0f : value;
        }
        // 是否循环
        public bool isLoop { get; private set; }
        // 已循环次数
        public int loopCount { get; private set; } = 0;
        // 0为无限循环
        public int maxLoop { get; private set; } = 0;
        // 当前状态
        public TimerState state { get; private set; } = TimerState.Idle;

        // 完成事件，当计时器到达结束时触发
        public event Action OnCompleted;
        // 循环完成事件，当计时器每次循环结束时触发
        public event Action<int> OnLoopCompleted;
        // 取消事件，只有在计时器被取消时触发，不会触发OnCompleted事件
        public event Action OnCancelled;
        // 参数为当前已用时间或剩余时间
        public event Action<float> OnTick;

        /// <summary>生命周期绑定目标（Attach 后目标销毁时计时器自动取消，防回调打到已销毁对象）</summary>
        private UnityEngine.Object attachedTarget;

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
        /// 开始计时器。如果当前为 Idle 状态则从 0 开始计时；
        /// 如果当前为 Paused 状态则从暂停处继续（保留 elapsed）。
        /// 如果已在 Running 状态则忽略。
        /// </summary>
        public void Start()
        {
            if (state == TimerState.Running) return;

            // 仅当从 Idle/Finished/Cancelled 启动时才清零
            if (state != TimerState.Paused)
            {
                elapsed = 0f;
                loopCount = 0;
            }
            state = TimerState.Running;
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
            OnCancelled?.Invoke();
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
        /// 绑定生命周期目标：目标（GameObject/Component）被销毁后计时器自动取消，
        /// 避免回调继续访问已销毁对象。
        /// </summary>
        public void Attach(GameObject target) => attachedTarget = target;

        /// <summary>绑定生命周期目标（Component 重载）</summary>
        public void Attach(Component target) => attachedTarget = target;

        /// <summary>解除生命周期绑定</summary>
        public void Detach() => attachedTarget = null;

        /// <summary>
        /// 每帧调用，deltaTime为Time.deltaTime
        /// </summary>
        public void Update(float deltaTime)
        {
            // 生命周期绑定检查：(object) 判真 null，Unity 重载 == 判已销毁（fake null）
            if ((object)attachedTarget != null && attachedTarget == null)
            {
                Cancel();
                return;
            }

            if (state != TimerState.Running)
            {
                return;
            }

            elapsed += deltaTime * timeScale;

            float time = isCountingDown ? duration - elapsed : elapsed;

            // 长帧超调时倒计时可能为负：对外钳制到 0，避免 UI 显示 "-0.0s"
            OnTick?.Invoke(Mathf.Max(0f, time));

            // OnTick 回调内可能调用 Cancel()：状态已变为 Cancelled 时不得继续触发完成分支
            if (state != TimerState.Running) return;

            if ((isCountingDown && time <= 0f) || (!isCountingDown && elapsed >= duration))
            {
                loopCount++;
                if (isLoop && (maxLoop == 0 || loopCount < maxLoop))
                {
                    // 保留溢出量避免长帧累积漂移（原实现归零会丢弃 elapsed-duration）
                    elapsed = duration > 0f ? Mathf.Repeat(elapsed, duration) : 0f;
                    OnLoopCompleted?.Invoke(loopCount);
                }
                else
                {
                    state = TimerState.Finished;
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
            // duration 可能为 0（构造时 Math.Max(0, duration)），直接除零得到 NaN。
            // 0 时长的计时器在首次 Update 即完成，视为进度 1。
            if (duration <= 0f) return 1f;
            return Math.Clamp(elapsed / duration, 0f, 1f);
        }

        /// <summary>
        /// 获取当前剩余时间（秒）
        /// </summary>
        public float GetRemainingTime()
        {
            return Mathf.Max(0f, duration - elapsed);
        }
    }
}
