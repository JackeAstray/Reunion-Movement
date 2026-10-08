using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// FileOperationUtil 文件名净化测试（EditMode）：
    /// 验证 SaveJson/LoadJson 对路径穿越与非法字符的净化，不会写出 Json 目录。
    /// 注意：写入真实 persistentDataPath，TearDown 清理测试文件。
    /// </summary>
    public class FileOperationUtilTests
    {
        [System.Serializable]
        private class SimpleJson
        {
            public int value;
        }

        private static string JsonDir => Path.Combine(Application.persistentDataPath, "Json");

        [TearDown]
        public void TearDown()
        {
            // 清理测试可能产生的净化文件
            TryDelete(Path.Combine(JsonDir, "....evil.json"));
            TryDelete(Path.Combine(JsonDir, "...nope.json"));
            TryDelete(Path.Combine(JsonDir, "abc.json"));
            TryDelete(Path.Combine(JsonDir, "unit_test_json.json"));
            TryDelete(Path.Combine(Application.persistentDataPath, "evil.json"));
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        [Test]
        public async Task SaveJson_TraversalName_Sanitized()
        {
            // "../../evil"：斜杠被剥离，只允许作为 Json 目录内的单文件名写出
            bool ok = await FileOperationUtil.SaveJson("{\"value\":1}", "../../evil");
            Assert.IsTrue(ok);
            Assert.IsTrue(File.Exists(Path.Combine(JsonDir, "....evil.json")), "净化后应落在 Json 目录内");
            Assert.IsFalse(File.Exists(Path.Combine(Application.persistentDataPath, "evil.json")), "不得写出 Json 目录");
        }

        [Test]
        public async Task SaveJson_AllIllegalChars_Rejected()
        {
            // 纯分隔符/非法字符：净化后为空，拒绝写入
            bool ok = await FileOperationUtil.SaveJson("{}", "/\\/");
            Assert.IsFalse(ok);
        }

        [Test]
        public async Task SaveJson_IllegalChars_Stripped()
        {
            bool ok = await FileOperationUtil.SaveJson("{\"value\":2}", "a<b>c");
            Assert.IsTrue(ok);
            // 只剥离非法字符本身，"a<b>c" 净化后是 "abc"（不是 "ac"）
            Assert.IsTrue(File.Exists(Path.Combine(JsonDir, "abc.json")), "非法字符 < > 应被剥离（a<b>c → abc）");
        }

        [Test]
        public void LoadJson_TraversalName_NotFound()
        {
            // "../nope" 净化后为单文件名，不存在则返回 default（null），不读目录外文件
            var result = FileOperationUtil.LoadJson<SimpleJson>("../nope");
            Assert.IsNull(result);
        }

        [Test]
        public async Task LoadJson_SaveLoad_Roundtrip()
        {
            // 必须 await：UniTask 不允许对未完成的操作用 GetAwaiter().GetResult() 阻塞等待
            // （会抛 InvalidOperationException，且遗留续体会在后续测试里跑成未处理异常）
            bool ok = await FileOperationUtil.SaveJson("{\"value\":42}", "unit_test_json");
            Assert.IsTrue(ok);
            var loaded = FileOperationUtil.LoadJson<SimpleJson>("unit_test_json");
            Assert.IsNotNull(loaded);
            Assert.AreEqual(42, loaded.value);
        }
    }
}
