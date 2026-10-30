using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;
using NUnit.Framework;

namespace Gear.Tests
{
    /// <summary>
    /// <see cref="Modules"/> 类型索引（快路径）的 EditMode 测试。
    ///
    /// <para>
    /// 与 <c>ModulesAccessorTests</c> 的分工：那个只覆盖"引擎未启动 / 未注册"的失败路径，
    /// 永远走不到类型索引；索引只在引擎持有模块列表时才存在，所以这里真的把引擎跑起来。
    /// </para>
    ///
    /// <para>
    /// 覆盖三件事：精确类型命中、基类/派生类的回退扫描、以及模块增删后索引必须失效重建。
    /// </para>
    /// </summary>
    public class EngineModuleIndexTests
    {
        private abstract class BaseModule : ICustomSystem
        {
            public double InitProgress => 100;
            public UniTask Init() => UniTask.CompletedTask;
        }

        private sealed class ModuleA : ICustomSystem
        {
            public double InitProgress => 100;
            public UniTask Init() => UniTask.CompletedTask;
        }

        private sealed class DerivedModule : BaseModule
        {
        }

        private sealed class ModuleC : ICustomSystem
        {
            public double InitProgress => 100;
            public UniTask Init() => UniTask.CompletedTask;
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
            // 必须销毁：Modules 读的是 GameEngine.Current，残留实例会让其他测试拿到脏状态
            engine?.Dispose();
            engine = null;
        }

        private void Launch(params ICustomSystem[] modules)
        {
            engine = GameEngine.Create();
            InitResult result = engine
                .LaunchAsync(new StubEntry(), new List<ICustomSystem>(modules))
                .GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess, "引擎启动失败：" + result.ErrorMessage);
        }

        [Test]
        public void Get_ExactType_ReturnsRegisteredInstance()
        {
            var a = new ModuleA();
            Launch(a, new ModuleC());

            Assert.AreSame(a, Modules.Get<ModuleA>());
            Assert.IsTrue(Modules.Contains<ModuleA>());
        }

        [Test]
        public void Get_BaseType_FallsBackToAssignableScan()
        {
            var derived = new DerivedModule();
            Launch(new ModuleA(), derived);

            Assert.AreSame(derived, Modules.Get<BaseModule>(),
                "精确类型没命中时应退回线性扫描，保持“取第一个可赋值实现”的原语义");
        }

        [Test]
        public void RemoveModule_InvalidatesIndex()
        {
            var a = new ModuleA();
            var c = new ModuleC();
            Launch(a, c);

            Assert.AreSame(a, Modules.Get<ModuleA>());

            Assert.IsTrue(engine.RemoveModule(a), "移除已注册模块应成功");

            Assert.IsFalse(Modules.Contains<ModuleA>(), "移除后索引必须失效，不能继续返回旧模块");
            Assert.Throws<InvalidOperationException>(() => Modules.Get<ModuleA>());
            Assert.AreSame(c, Modules.Get<ModuleC>(), "移除一个模块不应影响其他模块的解析");
        }

        [Test]
        public void AddModuleAsync_InvalidatesIndex()
        {
            Launch(new ModuleA());

            Assert.IsFalse(Modules.Contains<ModuleC>());

            var c = new ModuleC();
            bool added = engine.AddModuleAsync(c).GetAwaiter().GetResult();
            Assert.IsTrue(added, "引擎运行期注册模块应成功");

            Assert.AreSame(c, Modules.Get<ModuleC>(), "新注册的模块必须立刻能被解析到");
        }
    }
}
