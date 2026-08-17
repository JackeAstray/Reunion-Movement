using NUnit.Framework;
using ReunionMovement.Common.Util.Timer;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// Timer 纯逻辑 EditMode 测试（无场景依赖）。
    /// </summary>
    public class TimerTests
    {
        [Test]
        public void Countdown_Completes_AtDuration()
        {
            var timer = new Timer(2f, isCountingDown: true);
            bool completed = false;
            timer.OnCompleted += () => completed = true;
            timer.Start();
            timer.Update(1f);
            Assert.IsFalse(completed);
            timer.Update(1f);
            Assert.IsTrue(completed);
            Assert.AreEqual(Timer.TimerState.Finished, timer.state);
        }

        [Test]
        public void Cancel_InOnTick_DoesNotFireCompleted()
        {
            var timer = new Timer(1f, isCountingDown: true);
            bool completed = false;
            bool cancelled = false;
            timer.OnTick += t =>
            {
                if (t <= 0.5f) timer.Cancel();
            };
            timer.OnCompleted += () => completed = true;
            timer.OnCancelled += () => cancelled = true;

            timer.Start();
            timer.Update(0.5f); // 首次 tick：t=0.5 → 回调内 Cancel
            timer.Update(0.5f); // 状态已非 Running，不再推进

            Assert.IsFalse(completed, "OnTick 内取消后不应触发 OnCompleted");
            Assert.IsTrue(cancelled);
            Assert.AreEqual(Timer.TimerState.Cancelled, timer.state);
        }

        [Test]
        public void Loop_CarriesOverflow()
        {
            var timer = new Timer(1f, isCountingDown: true, isLoop: true, maxLoop: 3);
            int loopCompleted = 0;
            timer.OnLoopCompleted += _ => loopCompleted++;
            timer.Start();

            timer.Update(2.5f); // 单帧跨越 2 次循环 + 0.5 溢出

            Assert.AreEqual(2, loopCompleted, "长帧跨越的周期应逐次补发 OnLoopCompleted（2026-08-15 已修复多循环限制）");
            Assert.AreEqual(0.5f, timer.elapsed, 0.001f, "溢出量应保留而非归零");
        }

        [Test]
        public void PauseResume_KeepsElapsed()
        {
            var timer = new Timer(4f, isCountingDown: false);
            timer.Start();
            timer.Update(1f);
            timer.Pause();
            timer.Update(1f); // 暂停期间不推进
            Assert.AreEqual(1f, timer.elapsed, 0.001f);
            timer.Resume();
            timer.Update(1f);
            Assert.AreEqual(2f, timer.elapsed, 0.001f);
        }

        [Test]
        public void Start_FromFinished_ResetsState()
        {
            var timer = new Timer(1f, isCountingDown: true);
            bool completed = false;
            timer.OnCompleted += () => completed = true;
            timer.Start();
            timer.Update(1f);
            Assert.IsTrue(completed);

            timer.Start(); // 从 Finished 重新开始
            Assert.AreEqual(Timer.TimerState.Running, timer.state);
            Assert.AreEqual(0f, timer.elapsed, 0.001f);
        }
    }
}
