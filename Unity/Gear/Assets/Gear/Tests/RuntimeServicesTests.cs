using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Gear.Framework.Runtime.Base;
using System;
using System.Collections.Generic;

namespace Gear.Tests
{
    /// <summary>
    /// Runtime ↔ Modules 解耦层的测试：模块来源注册表、分辨率实现桥。
    /// 都是纯托管逻辑，不依赖 Unity 运行时。
    /// </summary>
    public class RuntimeServicesTests
    {
        [SetUp]
        public void SetUp()
        {
            ModuleSourceRegistry.Reset();
            ResolutionBridge.Reset();
            PerformanceStatsBridge.Reset();
            ErrorReportingBridge.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ModuleSourceRegistry.Reset();
            ResolutionBridge.Reset();
            PerformanceStatsBridge.Reset();
            ErrorReportingBridge.Reset();
        }

        [Test]
        public void ModuleSourceRegistry_Replaces_Source_Of_Same_Type()
        {
            ModuleSourceRegistry.Register(new FakeModuleSource(1));
            ModuleSourceRegistry.Register(new FakeModuleSource(2));

            Assert.AreEqual(1, ModuleSourceRegistry.Count, "同类型来源应替换，不能在关闭 Domain Reload 时越积越多");

            var target = new List<ICustomSystem>();
            Assert.AreEqual(2, ModuleSourceRegistry.Collect(target), "应使用后注册的来源");
            Assert.AreEqual(2, target.Count);
        }

        [Test]
        public void ModuleSourceRegistry_Skips_Null_And_Throwing_Sources()
        {
            ModuleSourceRegistry.Register(new ThrowingSource());
            ModuleSourceRegistry.Register(new NullSource());

            var target = new List<ICustomSystem>();
            Assert.AreEqual(0, ModuleSourceRegistry.Collect(target), "可选来源异常不应阻断启动");
        }

        [Test]
        public void ModuleSourceRegistry_Reset_Clears_All_Sources()
        {
            ModuleSourceRegistry.Register(new FakeModuleSource(1));
            ModuleSourceRegistry.Reset();

            Assert.AreEqual(0, ModuleSourceRegistry.Count);
        }

        [Test]
        public void ModuleSourceRegistration_Contributes_Modules_Source()
        {
            ModuleSourceRegistry.Reset();

            Gear.Framework.Modules.ModuleSourceRegistration.EnsureRegistered();

            Assert.AreEqual(1, ModuleSourceRegistry.Count, "Modules 程序集应注册一个来源");
        }

        [Test]
        public void ResolutionBridge_Only_Current_Instance_Can_Unregister()
        {
            var first = new FakeResolutionApplier();
            var second = new FakeResolutionApplier();

            ResolutionBridge.Register(first);
            Assert.AreSame(first, ResolutionBridge.Current);

            ResolutionBridge.Register(second);
            ResolutionBridge.Unregister(first); // 旧实例注销不应清掉新实例
            Assert.AreSame(second, ResolutionBridge.Current);

            ResolutionBridge.Unregister(second);
            Assert.IsNull(ResolutionBridge.Current);

            ResolutionBridge.Register(first);
            ResolutionBridge.Reset();
            Assert.IsNull(ResolutionBridge.Current);
        }

        [Test]
        public void PerformanceStatsBridge_Only_Current_Instance_Can_Unregister()
        {
            var first = new FakePerformanceStats();
            var second = new FakePerformanceStats();

            PerformanceStatsBridge.Register(first);
            PerformanceStatsBridge.Register(second);
            PerformanceStatsBridge.Unregister(first);
            Assert.AreSame(second, PerformanceStatsBridge.Current);

            PerformanceStatsBridge.Unregister(second);
            Assert.IsNull(PerformanceStatsBridge.Current);
        }

        [Test]
        public void PerformanceStatsBridge_GetOrCreate_Uses_Registered_Factory()
        {
            var stats = new FakePerformanceStats();
            PerformanceStatsBridge.Resolve = () => stats;

            Assert.AreSame(stats, PerformanceStatsBridge.GetOrCreate());
        }

        [Test]
        public void ErrorReportingBridge_Upload_Reports_Whether_Handler_Is_Registered()
        {
            bool called = false;
            Assert.IsFalse(ErrorReportingBridge.TryUploadLog(_ => { called = true; }), "未注册时应返回 false");

            ErrorReportingBridge.UploadLog = onComplete => { onComplete(true); };
            Assert.IsTrue(ErrorReportingBridge.TryUploadLog(ok => { called = ok; }));
            Assert.IsTrue(called);
        }

        // ---- 测试替身 ----

        private sealed class FakeModule : ICustomSystem
        {
            public double InitProgress => 100;
            public UniTask Init() => UniTask.CompletedTask;
        }

        private sealed class FakeModuleSource : IModuleSource
        {
            private readonly int count;

            public FakeModuleSource(int count)
            {
                this.count = count;
            }

            public IEnumerable<ICustomSystem> GetModules()
            {
                for (int i = 0; i < count; i++) yield return new FakeModule();
            }
        }

        private sealed class ThrowingSource : IModuleSource
        {
            public IEnumerable<ICustomSystem> GetModules()
            {
                throw new InvalidOperationException("source failed");
            }
        }

        private sealed class NullSource : IModuleSource
        {
            public IEnumerable<ICustomSystem> GetModules() => null;
        }

        private sealed class FakeResolutionApplier : IResolutionApplier
        {
            public void ApplyResolutionFromOptions(int width, int height, bool fullscreen) { }
        }

        private sealed class FakePerformanceStats : IPerformanceStats
        {
            public float CurrentFps => 60f;
            public float MinFpsRecord => 55f;
            public long CurrentMemoryMB => 128L;
        }
    }
}
