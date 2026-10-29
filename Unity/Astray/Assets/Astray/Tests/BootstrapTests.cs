using NUnit.Framework;
using Astray.Framework.Runtime;
using Astray.Framework.Runtime.Base;
using System;
using System.Linq;

namespace Astray.Tests
{
    /// <summary>
    /// 启动入口与启动配置的 EditMode 测试。
    ///
    /// 回归保护：
    /// 1. 测试场景判定改成可配置后，默认值必须与旧版硬编码规则完全一致，
    ///    否则示例场景会开始自动跳转（或业务场景被误判为测试场景）。
    /// 2. 入口选择：配置优先于程序集声明；没有入口时默认<b>不</b>回退内置 StartGame，
    ///    避免框架被导入已有工程后自动接管宿主启动流程。
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
        public void Resolver_Finds_Demo_Entry()
        {
            if (!IsDemoAssemblyLoaded())
            {
                Assert.Ignore("示例入口在 Assembly-CSharp，HeadlessCheck 不加载该程序集");
            }

            // Game/StartGameEntry.cs（Assembly-CSharp）声明了示例入口；
            // 框架程序集本身不声明入口，避免包安装时接管宿主启动。
            Assert.AreEqual(typeof(StartGame), GameEntryResolver.FindEntryType(),
                "未找到示例入口，检查 Assets/Astray/Game/StartGameEntry.cs");
        }

        [Test]
        public void Resolver_Creates_Entry_Instance()
        {
            if (!IsDemoAssemblyLoaded())
            {
                Assert.Ignore("示例入口在 Assembly-CSharp，HeadlessCheck 不加载该程序集");
            }

            var entry = GameEntryResolver.CreateEntry();
            Assert.IsNotNull(entry);
            Assert.IsInstanceOf<StartGame>(entry);
        }

        private static bool IsDemoAssemblyLoaded()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "Assembly-CSharp");
        }

        [Test]
        public void Attribute_Rejects_Null_Type()
        {
            Assert.Throws<ArgumentNullException>(() => new AstrayGameEntryAttribute(null));
        }

        // ---------------------------------------------------------------- 入口选择

        [Test]
        public void Options_Default_Does_Not_Fall_Back_To_BuiltIn_Entry()
        {
            var options = new BootstrapOptions();

            Assert.IsNull(options.Entry);
            Assert.IsFalse(options.AllowBuiltInEntryFallback,
                "默认不允许回退，否则把框架导入已有工程会自动接管启动流程");
        }

        [Test]
        public void ResolveEntry_Prefers_Configured_Entry()
        {
            var configured = new StartGame();
            var declared = new StartGame();

            Assert.AreSame(configured, Bootstrap.ResolveEntry(configured, () => declared, true),
                "Options.Entry 应优先于程序集声明");
        }

        [Test]
        public void ResolveEntry_Prefers_Declared_Entry_When_No_Configured()
        {
            var declared = new StartGame();

            Assert.AreSame(declared, Bootstrap.ResolveEntry(null, () => declared, true));
        }

        [Test]
        public void ResolveEntry_Returns_Null_When_Nothing_Declared_And_Fallback_Disabled()
        {
            Assert.IsNull(Bootstrap.ResolveEntry(null, () => null, false),
                "没有入口且未显式打开回退时，必须返回 null，不能替用户选 StartGame");
        }

        [Test]
        public void ResolveEntry_Falls_Back_Only_When_Explicitly_Enabled()
        {
            var entry = Bootstrap.ResolveEntry(null, () => null, true);

            Assert.IsInstanceOf<StartGame>(entry);
        }
    }
}
