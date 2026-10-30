using NUnit.Framework;
using Gear.Framework.Modules.Timer;

namespace Gear.Tests
{
    /// <summary>
    /// 计时器对象池（<see cref="TimerMgr.ReuseInstances"/>，默认关闭）的 EditMode 测试。
    /// <para>
    /// 池化最大的风险是"旧引用被静默复用"：上一次的 OnCompleted/OnTick 订阅若没清空，
    /// 会在下一次租用时打到新计时器上。这里把这两条约束钉住。
    /// </para>
    /// </summary>
    public class TimerPoolTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetPoolState();
        }

        [TearDown]
        public void TearDown()
        {
            ResetPoolState();
        }

        private static void ResetPoolState()
        {
            TimerMgr.ReuseInstances = false;
            TimerMgr.Instance.CancelAllTimers();
        }

        [Test]
        public void RecycledTimer_ReusesInstance_AndClearsHandlers()
        {
            TimerMgr.ReuseInstances = true;
            var mgr = TimerMgr.Instance;

            var first = mgr.CreateTimer(0.1f);
            int completed = 0;
            int ticks = 0;
            first.OnCompleted += () => completed++;
            first.OnTick += _ => ticks++;
            first.Start();
            mgr.Tick(0.2f);

            Assert.AreEqual(1, completed, "计时器应已到点完成");
            Assert.AreEqual(Timer.TimerState.Idle, first.state, "回收时应复位为 Idle，供下一次租用");

            int ticksBefore = ticks;

            var second = mgr.CreateTimer(1f);
            Assert.AreSame(first, second, "池化开启时应复用已结束的实例");
            Assert.AreEqual(0, second.loopCount, "复用的实例必须从干净状态开始");

            second.Start();
            mgr.Tick(0.1f);

            Assert.AreEqual(ticksBefore, ticks, "旧的 OnTick 订阅必须在归还时被清空，不能作用到新计时器上");
            Assert.AreEqual(1, completed, "旧的 OnCompleted 不应因复用再次触发");
        }

        [Test]
        public void PoolingDisabled_AlwaysReturnsNewInstance()
        {
            var mgr = TimerMgr.Instance;

            var first = mgr.CreateTimer(0.1f);
            first.Start();
            mgr.Tick(0.2f);

            var second = mgr.CreateTimer(1f);
            Assert.AreNotSame(first, second, "默认（关闭池化）时不得复用实例，否则旧引用会串味");
        }
    }
}
