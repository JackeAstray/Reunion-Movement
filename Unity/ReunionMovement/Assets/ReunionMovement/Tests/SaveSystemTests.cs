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

            // 加密后磁盘内容不是明文 JSON（新版格式带 HMAC：ENC2:）
            string raw = File.ReadAllText(SaveSystem.GetSavePath(TestName));
            Assert.IsTrue(raw.StartsWith("ENC2:"));
            Assert.IsFalse(raw.Contains("\"value\":42"), "密文不应包含明文字段");

            Assert.IsTrue(SaveSystem.TryLoad(TestName, out TestData loaded));
            Assert.AreEqual(42, loaded.value);
            Assert.AreEqual("你好", loaded.text);
        }

        [Test]
        public void Tampered_Cipher_Rejected()
        {
            SaveSystem.EnableEncryption = true;
            SaveSystem.Save(TestName, new TestData { value = 1 });

            // 篡改密文任意一字节：HMAC 校验必须拒绝加载（防止位翻转篡改存档）
            string path = SaveSystem.GetSavePath(TestName);
            byte[] bytes = File.ReadAllBytes(path);
            int flipIndex = bytes.Length / 2;
            bytes[flipIndex] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            Assert.IsFalse(SaveSystem.TryLoad(TestName, out TestData _), "被篡改的密文不应被解析");
        }

        [Test]
        public void Load_LegacyEnc1_BackwardCompatible()
        {
            // 旧版 ENC1:（无 HMAC）存档升级后仍可读取（TearDown 会清理）
            SaveSystem.EnableEncryption = true;
            string path = SaveSystem.GetSavePath(TestName);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // 手工构造 ENC1 存档（与升级前实现一致），验证兼容读取路径
            File.WriteAllText(path, EncryptLegacyForTest("{\"value\":9,\"text\":\"legacy-enc1\"}"));

            Assert.IsTrue(SaveSystem.TryLoad(TestName, out TestData loaded));
            Assert.AreEqual(9, loaded.value);
            Assert.AreEqual("legacy-enc1", loaded.text);
        }

        /// <summary>构造旧版 ENC1: 密文（与升级前实现一致，仅测试用）</summary>
        private static string EncryptLegacyForTest(string plain)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = SaveSystemInternalKey();
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(plain);
            byte[] cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            byte[] payload = new byte[16 + cipher.Length];
            System.Buffer.BlockCopy(aes.IV, 0, payload, 0, 16);
            System.Buffer.BlockCopy(cipher, 0, payload, 16, cipher.Length);
            return "ENC1:" + System.Convert.ToBase64String(payload);
        }

        /// <summary>派生 SaveSystem 内部密钥（测试用；生产路径走 SaveSystem 自身实现）</summary>
        private static byte[] SaveSystemInternalKey()
        {
            var salt = new byte[] { 0x52, 0x4D, 0x53, 0x76, 0x31, 0xA5, 0x3C, 0x9F };
            using var pdb = new System.Security.Cryptography.Rfc2898DeriveBytes("ReunionMovement.SaveSystem.v1", salt, 1000);
            return pdb.GetBytes(32);
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
