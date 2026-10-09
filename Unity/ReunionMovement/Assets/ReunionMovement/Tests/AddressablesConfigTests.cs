using NUnit.Framework;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// Addressables 开关语义 EditMode 测试。
    ///
    /// 回归保护：Addressables 只在需要热更新时启用 —— `enableHotUpdate = false` 必须派生出 `Off`
    /// （纯 Resources：无 catalog 初始化、无 location 查找、无 bundle 装载），
    /// 且不再存在 `LocalOnly` 中间档（该模式在玩家包内不成立，见 AddressablesMode 注释）。
    /// </summary>
    public class AddressablesConfigTests
    {
        private const string ConfigResourcePath = "ScriptableObjects/GameConfig";

        private bool originalValue;
        private bool captured;

        [SetUp]
        public void SetUp()
        {
            Config.EnsureLoaded();
            var cfg = Resources.Load<GameConfig>(ConfigResourcePath);
            Assert.IsNotNull(cfg, $"未找到 GameConfig 资产: Resources/{ConfigResourcePath}");
            originalValue = cfg.enableHotUpdate;
            captured = true;
        }

        [TearDown]
        public void TearDown()
        {
            // 还原资产字段（仅内存实例，不写盘），避免测试污染后续用例
            if (!captured) return;
            var cfg = Resources.Load<GameConfig>(ConfigResourcePath);
            if (cfg != null) cfg.enableHotUpdate = originalValue;
        }

        [Test]
        public void Disabled_HotUpdate_Maps_To_Off()
        {
            Config.EnableHotUpdate = false;

            Assert.IsFalse(Config.EnableHotUpdate, "关闭热更新后 EnableHotUpdate 应为 false");
            Assert.AreEqual(AddressablesMode.Off, Config.AddressablesMode,
                "关闭热更新必须派生 Off（全部走 Resources）");
        }

        [Test]
        public void Enabled_HotUpdate_Maps_To_Remote()
        {
            Config.EnableHotUpdate = true;

            Assert.IsTrue(Config.EnableHotUpdate, "开启热更新后 EnableHotUpdate 应为 true");
            Assert.AreEqual(AddressablesMode.Remote, Config.AddressablesMode,
                "开启热更新必须派生 Remote（唯一启用 Addressables 的形态）");
        }

        [Test]
        public void AddressablesMode_Has_Only_Off_And_Remote()
        {
            CollectionAssert.AreEquivalent(
                new[] { AddressablesMode.Off, AddressablesMode.Remote },
                System.Enum.GetValues(typeof(AddressablesMode)),
                "AddressablesMode 只应有 Off 与 Remote 两个值");

            Assert.IsFalse(System.Enum.IsDefined(typeof(AddressablesMode), 1),
                "值 1 曾为 LocalOnly，应保持未定义（保留空位避免误读历史数据）");
        }
    }
}
