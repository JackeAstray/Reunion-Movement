using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Gear.Framework.Modules;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;

namespace Gear.Tests.PlayMode
{
    /// <summary>
    /// P2 解耦的运行时验收：真实 Play 模式下，ModulePresets.Full() 应拿到
    /// 11 个 Runtime 模块 + Modules 程序集注册的 3 个模块（Timer / Network / Http）。
    /// </summary>
    public class ModuleCompositionPlayModeTests
    {
        /// <summary>
        /// PlayMode 测试不需要启动完整游戏：在 BeforeSceneLoad 之前关掉自动 Bootstrap，
        /// 避免测试进程里同时创建 GameEngine/Driver 干扰断言。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DisableAutoBootstrapEarly()
        {
            DisableAutoBootstrapIfBatchMode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void DisableAutoBootstrapForTests()
        {
            DisableAutoBootstrapIfBatchMode();
        }

        /// <summary>
        /// 只在批处理测试（CI / -batchmode）里关闭 Bootstrap；正常编辑器 Play 不受影响。
        /// 注意：测试程序集在编辑器里也会被加载，不能无条件 Disable。
        /// </summary>
        private static void DisableAutoBootstrapIfBatchMode()
        {
            if (Application.isBatchMode) Bootstrap.Disable();
        }

        [SetUp]
        public void SetUp()
        {
            ModuleSourceRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ModuleSourceRegistry.Reset();
        }

        [UnityTest]
        public IEnumerator Full_Contains_Runtime_And_Modules_Contributions()
        {
            // SubsystemRegistration 已经注册过一次；SetUp 清空后显式再注册，模拟真实启动时序
            ModuleSourceRegistration.EnsureRegistered();

            var modules = ModulePresets.Full();

            Assert.AreEqual(14, modules.Count, "Full() 应为 11 个 Runtime 模块 + 3 个 Modules 模块");

            var types = new HashSet<System.Type>();
            int runtimeModules = 0;
            int modulesAssemblyModules = 0;

            foreach (var module in modules)
            {
                Assert.IsNotNull(module);
                Assert.IsTrue(types.Add(module.GetType()), "模块重复：" + module.GetType().Name);

                string assemblyName = module.GetType().Assembly.GetName().Name;
                if (assemblyName == "Gear.Framework.Runtime") runtimeModules++;
                else if (assemblyName == "Gear.Framework.Modules") modulesAssemblyModules++;
            }

            Assert.AreEqual(11, runtimeModules, "Runtime 程序集应贡献 11 个模块");
            Assert.AreEqual(3, modulesAssemblyModules, "Modules 程序集应贡献 Timer / Network / Http 3 个模块");

            yield return null;
        }
    }
}
