using System.Text.RegularExpressions;
using NUnit.Framework;
using ReunionMovement.Core.Pause;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// PauseSystem 多原因计票的 EditMode 测试。
    /// 重点验证"一方误释放不会打断其他系统暂停"这一契约，
    /// 以及 timeScale 在暂停/恢复/清理时的还原行为。
    /// 说明：测试里关掉 pauseAudioOnPause，避免触碰 AudioListener 与 GameOption 的全局状态。
    /// </summary>
    public class PauseSystemTests
    {
        private static readonly object ReasonA = new object();
        private static readonly object ReasonB = new object();

        private float originalTimeScale;

        private PauseSystem Pause => PauseSystem.Instance;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            Pause.Clear();
            Pause.Init();
            Pause.pauseAudioOnPause = false;
        }

        [TearDown]
        public void TearDown()
        {
            Pause.Clear();
            Time.timeScale = originalTimeScale;
        }

        [Test]
        public void RequestPause_BeforeInit_IsIgnored()
        {
            Pause.Clear();
            LogAssert.Expect(LogType.Warning, new Regex("RequestPause.*被忽略"));

            Pause.RequestPause(ReasonA);

            Assert.IsNull(Pause.IsPaused, "未初始化时 IsPaused 应为 null（已 Dispose）");
            Assert.AreEqual(1f, Time.timeScale, 1e-6f, "未初始化时不应改动 timeScale");
        }

        [Test]
        public void SingleReason_PausesThenResumes()
        {
            Pause.RequestPause(ReasonA);

            Assert.IsTrue(Pause.IsPaused.Value);
            Assert.AreEqual(0f, Time.timeScale, 1e-6f);

            Pause.ReleasePause(ReasonA);

            Assert.IsFalse(Pause.IsPaused.Value);
            Assert.AreEqual(1f, Time.timeScale, 1e-6f);
        }

        [Test]
        public void TwoReasons_StayPausedUntilBothReleased()
        {
            Pause.RequestPause(ReasonA);
            Pause.RequestPause(ReasonB);

            Pause.ReleasePause(ReasonA);
            Assert.IsTrue(Pause.IsPaused.Value, "还有一个原因未释放，不应恢复");
            Assert.AreEqual(0f, Time.timeScale, 1e-6f);

            Pause.ReleasePause(ReasonB);
            Assert.IsFalse(Pause.IsPaused.Value);
        }

        [Test]
        public void DuplicateReason_IsIdempotent()
        {
            Pause.RequestPause(ReasonA);
            Pause.RequestPause(ReasonA);

            Pause.ReleasePause(ReasonA);

            Assert.IsFalse(Pause.IsPaused.Value, "同一 reason 重复注册只算一票，释放一次即恢复");
        }

        [Test]
        public void SetPaused_IsIdempotent()
        {
            Pause.SetPaused(true);
            Pause.SetPaused(true);

            Pause.SetPaused(false);

            Assert.IsFalse(Pause.IsPaused.Value, "SetPaused 复用全局键，重复调用不应叠加票数");
        }

        [Test]
        public void Resume_RestoresPreviousTimeScale()
        {
            Time.timeScale = 0.5f;

            Pause.RequestPause(ReasonA);
            Assert.AreEqual(0f, Time.timeScale, 1e-6f, "暂停期间 timeScale 归零");

            Pause.ReleasePause(ReasonA);
            Assert.AreEqual(0.5f, Time.timeScale, 1e-6f, "恢复时应还原进入暂停前的时间缩放，而不是无条件置 1");
        }

        [Test]
        public void TogglePause_FlipsState()
        {
            Pause.TogglePause();
            Assert.IsTrue(Pause.IsPaused.Value);

            Pause.TogglePause();
            Assert.IsFalse(Pause.IsPaused.Value);
        }

        [Test]
        public void Clear_ResetsStateAndTimeScale()
        {
            Pause.RequestPause(ReasonA);

            Pause.Clear();

            Assert.IsNull(Pause.IsPaused, "Clear 应释放并置空 IsPaused，避免订阅者泄漏");
            Assert.AreEqual(1f, Time.timeScale, 1e-6f, "Clear 必须还原 timeScale，否则引擎重建后整局卡死");
            Assert.IsFalse(Pause.isInited);
        }
    }
}
