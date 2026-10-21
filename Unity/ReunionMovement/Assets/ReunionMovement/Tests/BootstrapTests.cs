using NUnit.Framework;
using ReunionMovement.Core;
using ReunionMovement.Core.Base;
using System;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// 启动入口与启动配置的 EditMode 测试。
    ///
    /// 回归保护：
    /// 1. 测试场景判定改成可配置后，默认值必须与旧版硬编码规则完全一致，
    ///    否则示例场景会开始自动跳转（或业务场景被误判为测试场景）。
    /// 2. 入口解析必须能在没有用户声明时回退到框架内置的 StartGame。
    /// </summary>
    public class BootstrapTests
    {
        [TestCase("Test_Physics", true)]
        [TestCase("_Sandbox", true)]
        [TestCase("Arrow Example", true)]
        [TestCase("UI Example", true)]
        [TestCase("Music Example", true)]
        [TestCase("Audio Debug", true)]
        [TestCase("UIPlaneScene", true)]
        [TestCase("StartGame", false)]
        [TestCase("Temp", false)]
        [TestCase("LoadingScene", false)]
        [TestCase("Demo", false)]
        public void DefaultOptions_Match_Legacy_Scene_Rules(string sceneName, bool expected)
        {
            var options = new BootstrapOptions();
            Assert.AreEqual(expected, options.IsTestScene(sceneName), $"场景 '{sceneName}' 的判定与旧版规则不一致");
        }

        [Test]
        public void Default_TestScene_Rules_Are_Configurable()
        {
            var options = new BootstrapOptions
            {
                TestScenePrefixes = new[] { "Sandbox" },
                TestSceneContains = Array.Empty<string>(),
            };

            Assert.IsTrue(options.IsTestScene("Sandbox_Arena"));
            Assert.IsFalse(options.IsTestScene("Test_Physics"), "覆盖后不再命中旧的 Test 前缀");
            Assert.IsFalse(options.IsTestScene("Arrow Example"), "覆盖后不再命中旧的 Example 关键字");
        }

        [Test]
        public void Null_Or_Empty_SceneName_Is_Not_TestScene()
        {
            var options = new BootstrapOptions();
            Assert.IsFalse(options.IsTestScene(null));
            Assert.IsFalse(options.IsTestScene(string.Empty));
        }

        [Test]
        public void Resolver_Finds_Framework_Declared_Entry()
        {
            // Core 程序集声明了 [assembly: ReunionGameEntry(typeof(StartGame))]
            Assert.AreEqual(typeof(StartGame), GameEntryResolver.FindEntryType(),
                "未找到框架声明的入口，检查 Core/Bootstrap.cs 顶部的 assembly 特性");
        }

        [Test]
        public void Resolver_Creates_Entry_Instance()
        {
            var entry = GameEntryResolver.CreateEntry();
            Assert.IsNotNull(entry);
            Assert.IsInstanceOf<StartGame>(entry);
        }

        [Test]
        public void Attribute_Rejects_Null_Type()
        {
            Assert.Throws<ArgumentNullException>(() => new ReunionGameEntryAttribute(null));
        }
    }
}
