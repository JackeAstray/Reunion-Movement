using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Gear.Framework.Modules.Timer;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;
using NUnit.Framework;

namespace Gear.Tests
{
    /// <summary>
    /// 热路径零分配棘轮。
    ///
    /// <para>
    /// 思路和 <c>instance-usage-baseline.txt</c> 一致：把"每次访问 / 每帧驱动不应该分配内存"
    /// 变成会红的断言。以后谁在 <see cref="Modules.Get{T}"/>、引擎驱动循环或计时器 tick 里
    /// 顺手加了装箱、lambda 捕获或 <c>new</c>，这里先炸，而不是等到真机上掉帧才发现。
    /// </para>
    ///
    /// <para>
    /// 用 <see cref="GC.GetAllocatedBytesForCurrentThread"/> 只统计当前线程的托管分配，
    /// 且测量前先跑一轮预热，把 JIT、类型索引构建、字典扩容这些一次性开销排除在稳态之外。
    /// </para>
    /// </summary>
    public class AllocationRatchetTests
    {
        private sealed class PlainModule : ICustomSystem
        {
            public double InitProgress => 100;
            public UniTask Init() => UniTask.CompletedTask;
        }

        private sealed class UpdatableModule : ICustomSystem, ISystemUpdatable
        {
            public int UpdateCount;

            public double InitProgress => 100;
            public UniTask Init() => UniTask.CompletedTask;
            public void Update(float logicTime, float realTime) => UpdateCount++;
        }

        private sealed class StubEntry : IGameEntry
        {
            public UniTask OnBeforeInitAsync() => UniTask.CompletedTask;
            public UniTask OnGameStartAsync() => UniTask.CompletedTask;
        }

        private GameEngine engine;

        [TearDown]
        public void TearDown()
        {
            engine?.Dispose();
            engine = null;
            TimerMgr.Instance.CancelAllTimers();
        }

        private void Launch(params ICustomSystem[] modules)
        {
            engine = GameEngine.Create();
            InitResult result = engine
                .LaunchAsync(new StubEntry(), new List<ICustomSystem>(modules))
                .GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess, "引擎启动失败：" + result.ErrorMessage);
        }

        private static long Measure(Action action, int iterations)
        {
            action(); // 预热：一次性开销（JIT / 索引构建 / 字典扩容）不计入稳态
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++) action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void ModulesGet_ExactType_DoesNotAllocate()
        {
            Launch(new PlainModule());

            long bytes = Measure(() => Modules.Get<PlainModule>(), 1000);

            Assert.Zero(bytes, "Modules.Get<T> 命中类型索引后应零分配");
        }

        [Test]
        public void ModulesTryGet_Miss_DoesNotAllocate()
        {
            Launch(new PlainModule());

            // 未注册类型走线性扫描兜底，同样不应产生分配
            long bytes = Measure(() => Modules.TryGet<UpdatableModule>(out _), 1000);

            Assert.Zero(bytes, "Modules.TryGet 未命中时只做类型判定，应零分配");
        }

        [Test]
        public void EngineUpdate_DoesNotAllocatePerFrame()
        {
            var updatable = new UpdatableModule();
            Launch(new PlainModule(), updatable);

            // 注意：LaunchAsync 本身不驱动一帧，Update 由 Driver 每帧推进，
            // 所以这里只能在驱动之后再断言"确实被调到了"。
            int before = updatable.UpdateCount;

            long bytes = Measure(() => engine.OnUpdate(0.016f, 0.016f), 1000);

            Assert.Greater(updatable.UpdateCount, before, "前置条件：可更新模块应已被驱动");
            Assert.Zero(bytes,
                "每帧驱动（R3 广播 + 快照遍历 + 逐模块分派 + 周期事件）应零分配；"
                + "引入装箱、lambda 或 new 会让这里变红");
        }

        [Test]
        public void TimerTick_DoesNotAllocateWhileRunning()
        {
            var mgr = TimerMgr.Instance;
            mgr.CancelAllTimers();

            var a = mgr.CreateTimer(1000f);
            var b = mgr.CreateTimer(1000f);
            a.Start();
            b.Start();

            long bytes = Measure(() => mgr.Tick(0.016f), 1000);

            Assert.Zero(bytes, "计时器 tick 复用快照与待移除表，应零分配");
        }
    }
}
