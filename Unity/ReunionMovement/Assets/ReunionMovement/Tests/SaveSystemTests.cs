using System.IO;
using NUnit.Framework;
using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// SaveSystem 加密/槽位/兼容性 EditMode 测试。
    /// 注意：写入真实 persistentDataPath，SetUp/TearDown 清理测试存档。
    /// </summary>
    public class SaveSystemTests
    {
        [System.Serializable]
        public class TestData
        {
            public int value;
            public string text;
        }

        private const string TestName = "unit_test_save";

        [SetUp]
        public void SetUp()
        {
            SaveSystem.Delete(TestName);
            SaveSystem.Delete(TestName, 1);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Delete(TestName);
            SaveSystem.Delete(TestName, 1);
            SaveSystem.EnableEncryption = true;
        }

        [Test]
        public void Save_Load_Roundtrip_Encrypted()
        {
            SaveSystem.EnableEncryption = true;
            var data = new TestData { value = 42, text = "你好" };
            SaveSystem.Save(TestName, data);

            // 加密后磁盘内容不是明文 JSON
            string raw = File.ReadAllText(SaveSystem.GetSavePath(TestName));
            Assert.IsTrue(raw.StartsWith("ENC1:"));
            Assert.IsFalse(raw.Contains("\"value\":42"), "密文不应包含明文字段");

            Assert.IsTrue(SaveSystem.TryLoad(TestName, out TestData loaded));
            Assert.AreEqual(42, loaded.value);
            Assert.AreEqual("你好", loaded.text);
        }

        [Test]
        public void Load_PlainText_BackwardCompatible()
        {
            SaveSystem.EnableEncryption = true; // 读取时自动兼容明文旧档
            string path = SaveSystem.GetSavePath(TestName);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"value\":7,\"text\":\"legacy\"}");

            Assert.IsTrue(SaveSystem.TryLoad(TestName, out TestData loaded));
            Assert.AreEqual(7, loaded.value);
            Assert.AreEqual("legacy", loaded.text);
        }

        [Test]
        public void Slot_Isolation()
        {
            SaveSystem.Save(TestName, new TestData { value = 1 }, prettyPrint: false, slot: 0);
            SaveSystem.Save(TestName, new TestData { value = 2 }, prettyPrint: false, slot: 1);

            Assert.AreEqual(1, SaveSystem.Load<TestData>(TestName).value);
            Assert.AreEqual(2, SaveSystem.Load<TestData>(TestName, 1).value);
            Assert.IsTrue(SaveSystem.Exists(TestName, 0));
            Assert.IsTrue(SaveSystem.Exists(TestName, 1));

            SaveSystem.Delete(TestName, 1);
            Assert.IsFalse(SaveSystem.Exists(TestName, 1));
            Assert.IsTrue(SaveSystem.Exists(TestName, 0), "删除槽位 1 不应影响槽位 0");
        }

        [Test]
        public void Load_Missing_ReturnsFalse()
        {
            Assert.IsFalse(SaveSystem.TryLoad(TestName, out TestData _));
        }
    }
}
