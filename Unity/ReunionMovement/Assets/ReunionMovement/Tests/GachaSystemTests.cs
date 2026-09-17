using System.Collections.Generic;
using NUnit.Framework;
using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// GachaSystem 确定性逻辑测试（EditMode，不依赖随机数）：
    /// 覆盖 ApplyServerResult 星级校验/保底推进、保底存档完整性校验与旧版兼容。
    /// 注意：保底持久化走 PlayerPrefs，SetUp/TearDown 清理测试键。
    /// </summary>
    public class GachaSystemTests
    {
        private const string PityKey = "gacha_pity_save_v1";
        private GachaSystem system;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PityKey);
            // 仅测试纯逻辑（卡池为空时 ApplyServerResult 不依赖卡池）
            system = new GachaSystem();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PityKey);
        }

        private static GachaSystem.GachaItem Item(int starRating) =>
            new GachaSystem.GachaItem { itemName = "item_" + starRating, starRating = starRating };

        [Test]
        public void ApplyServerResult_InvalidStar_ClampedTo3()
        {
            var item = Item(6);
            system.ApplyServerResult(item, isUp: false);

            // 非法星级按三星处理：字段被修正，保底只推进不重置
            Assert.AreEqual(3, item.starRating);
            Assert.AreEqual(1, system.Pity5Star);
            Assert.AreEqual(1, system.Pity4Star);
            Assert.IsFalse(system.IsLastPullUp);
        }

        [Test]
        public void ApplyServerResult_5Star_ResetsPity5Only()
        {
            system.ApplyServerResult(Item(5), isUp: true);

            Assert.AreEqual(0, system.Pity5Star, "五星应重置五星保底");
            Assert.AreEqual(1, system.Pity4Star, "四星保底独立，不应被五星重置");
            Assert.IsFalse(system.IsGuaranteedUp5Star, "isUp=true 应解除大保底");
            Assert.IsTrue(system.IsLastPullUp);
            Assert.AreEqual(1, system.Last5StarPullCount);
        }

        [Test]
        public void ApplyServerResult_5Star_NotUp_SetsGuarantee()
        {
            system.ApplyServerResult(Item(5), isUp: false);

            Assert.IsTrue(system.IsGuaranteedUp5Star, "非 UP 五星应触发大保底");
            Assert.IsFalse(system.IsLastPullUp);
        }

        [Test]
        public void ApplyServerResult_4Star_ResetsPity4Only()
        {
            system.ApplyServerResult(Item(4), isUp: true);

            Assert.AreEqual(0, system.Pity4Star);
            Assert.AreEqual(1, system.Pity5Star, "四星不应重置五星保底");
            Assert.IsFalse(system.IsGuaranteedUp4Star);
            Assert.IsTrue(system.IsLastPullUp);
        }

        [Test]
        public void ApplyServerResult_3Star_OnlyAdvancesPity()
        {
            system.ApplyServerResult(Item(3));

            Assert.AreEqual(1, system.Pity5Star);
            Assert.AreEqual(1, system.Pity4Star);
            Assert.IsFalse(system.IsLastPullUp);
        }

        [Test]
        public void LoadPityState_TamperedHash_Rejected()
        {
            // 手改 PlayerPrefs（错误 integrityHash）→ 校验失败应保持默认状态
            PlayerPrefs.SetString(PityKey,
                "{\"pity5Star\":50,\"pity4Star\":5,\"isGuaranteedUp5Star\":false,\"isGuaranteedUp4Star\":false,\"last5StarPullCount\":0,\"isLastPullUp\":false,\"integrityHash\":\"deadbeef\"}");
            system.LoadPityState();

            Assert.AreEqual(0, system.Pity5Star, "篡改数据应被拒绝");
            Assert.AreEqual(0, system.Pity4Star);
        }

        [Test]
        public void LoadPityState_LegacyNoHash_Compatible()
        {
            // 旧版存档无 integrityHash 字段：兼容读取
            PlayerPrefs.SetString(PityKey,
                "{\"pity5Star\":7,\"pity4Star\":3,\"isGuaranteedUp5Star\":false,\"isGuaranteedUp4Star\":true,\"last5StarPullCount\":7,\"isLastPullUp\":false}");
            system.LoadPityState();

            Assert.AreEqual(7, system.Pity5Star);
            Assert.AreEqual(3, system.Pity4Star);
            Assert.IsTrue(system.IsGuaranteedUp4Star);
            Assert.AreEqual(7, system.Last5StarPullCount);
        }

        [Test]
        public void SaveAndLoad_ValidState_Roundtrip()
        {
            // 通过服务端结果推进保底（确定性），再持久化/恢复
            system.ApplyServerResult(Item(5), isUp: false);   // pity5 重置 → 0
            system.ApplyServerResult(Item(3));                 // pity5 → 1, pity4 → 1
            system.SavePityState();

            var restored = new GachaSystem();
            restored.LoadPityState();

            Assert.AreEqual(1, restored.Pity5Star);
            Assert.AreEqual(1, restored.Pity4Star);
            Assert.IsTrue(restored.IsGuaranteedUp5Star);
        }

        // ===== 卡池为空时保底状态机回归（修复：空池出货不得消耗保底计数）=====

        [Test]
        public void PerformPull_AllPoolsEmpty_NoPityAdvance()
        {
            // 所有卡池为空（默认构造）：直接返回 null，不得推进保底
            var item = system.PerformPull();

            Assert.IsNull(item);
            Assert.AreEqual(0, system.Pity5Star);
            Assert.AreEqual(0, system.Pity4Star);
        }

        [Test]
        public void Perform10Pull_AllPoolsEmpty_NoPityAdvance()
        {
            var results = system.Perform10Pull();

            Assert.IsNotNull(results);
            Assert.AreEqual(10, results.Count);
            Assert.AreEqual(0, system.Pity5Star, "全空池十连不得推进/消耗保底");
            Assert.AreEqual(0, system.Pity4Star);
        }

        [Test]
        public void PerformPull_5StarPoolEmpty_HardPityNotConsumed()
        {
            // 构造：五星双池皆空 + 四星/三星池有货；将五星保底推到硬保底（89→90）必出五星
            system.up5StarPool = new List<GachaSystem.GachaItem>();
            system.standard5StarPool = new List<GachaSystem.GachaItem>();
            system.up4StarPool = new List<GachaSystem.GachaItem> { Item(4) };
            system.standard4StarPool = new List<GachaSystem.GachaItem> { Item(4) };
            system.standard3StarPool = new List<GachaSystem.GachaItem> { Item(3) };

            var pityField = typeof(GachaSystem).GetField("pity5Star",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            pityField.SetValue(system, 89);

            var item = system.PerformPull();

            Assert.IsNull(item, "双五星池皆空应返回 null");
            Assert.AreEqual(90, system.Pity5Star, "空池出货不得消耗五星硬保底（保底保留推进后的值）");
            Assert.AreEqual(1, system.Pity4Star, "四星保底正常推进");
            Assert.AreEqual(0, system.Last5StarPullCount, "未实际出货不应改写最近五星抽数");
        }
    }
}
