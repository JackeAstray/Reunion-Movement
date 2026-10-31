using System;
using System.IO;
using NUnit.Framework;
using Gear.Framework.Modules.Download;

namespace Gear.Tests
{
    /// <summary>
    /// 下载完整性标记测试：验证"文件存在"不再等同于"下载完成"。
    /// 纯文件 IO，不依赖 Unity 运行时。
    /// </summary>
    public class DownloadIntegrityTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "gear-download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch { /* 测试清理失败不影响结论 */ }
        }

        [Test]
        public void HasCompletedDownload_MissingFile_ReturnsFalse()
        {
            Assert.IsFalse(UWRExecutor.HasCompletedDownload(Path.Combine(tempDir, "missing.bin")));
        }

        [Test]
        public void HasCompletedDownload_NoMarker_ReturnsFalse()
        {
            string file = Path.Combine(tempDir, "no-marker.bin");
            File.WriteAllBytes(file, new byte[10]);

            Assert.IsFalse(UWRExecutor.HasCompletedDownload(file),
                "没有完成标记的旧文件应重新下载/续传，不能直接跳过");
        }

        [Test]
        public void HasCompletedDownload_MatchingMarker_ReturnsTrue()
        {
            string file = Path.Combine(tempDir, "complete.bin");
            File.WriteAllBytes(file, new byte[10]);
            File.WriteAllText(file + UWRExecutor.CompletionMarkerSuffix, "10");

            Assert.IsTrue(UWRExecutor.HasCompletedDownload(file));
        }

        [Test]
        public void HasCompletedDownload_TruncatedFile_ReturnsFalse()
        {
            string file = Path.Combine(tempDir, "truncated.bin");
            File.WriteAllBytes(file, new byte[10]);
            File.WriteAllText(file + UWRExecutor.CompletionMarkerSuffix, "10");

            // 模拟下载中断：文件被截断，但标记还留着
            File.WriteAllBytes(file, new byte[4]);

            Assert.IsFalse(UWRExecutor.HasCompletedDownload(file),
                "大小与标记不一致必须判定为未完成，否则截断文件会被永久跳过");
        }

        [Test]
        public void HasCompletedDownload_CorruptMarker_ReturnsFalse()
        {
            string file = Path.Combine(tempDir, "bad-marker.bin");
            File.WriteAllBytes(file, new byte[3]);
            File.WriteAllText(file + UWRExecutor.CompletionMarkerSuffix, "not-a-number");

            Assert.IsFalse(UWRExecutor.HasCompletedDownload(file));
        }
    }
}
