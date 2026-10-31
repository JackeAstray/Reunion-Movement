using System;
using System.IO;
using Gear.Common;

namespace Gear.Framework.Modules.Download
{
    /// <summary>
    /// <see cref="UWRExecutor"/> 的"下载完成标记"部分：
    /// 只有成功落盘且大小校验通过的文件才写 <c>&lt;file&gt;.done</c>，
    /// 供 <see cref="DownloadMgr"/> 的 skipIfExists 判断"真的下完了"，
    /// 避免把中断留下的截断文件当成已完成。
    /// </summary>
    public partial class UWRExecutor
    {
        /// <summary>完成标记后缀（与最终文件同目录）。</summary>
        public const string CompletionMarkerSuffix = ".done";

        /// <summary>完成标记路径（文件为空时返回 null）。</summary>
        private string CompletionMarkerPath
        {
            get
            {
                string resultPath = DownloadResultPath;
                return string.IsNullOrEmpty(resultPath) ? null : resultPath + CompletionMarkerSuffix;
            }
        }

        /// <summary>
        /// 判断某个已落盘文件是否是"完成且大小一致"的下载结果。
        /// 没有标记（旧版本下载/被中断）一律返回 false，调用方应重新下载或续传。
        /// </summary>
        public static bool HasCompletedDownload(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            string marker = filePath + CompletionMarkerSuffix;
            if (!File.Exists(marker)) return false;

            try
            {
                long actual = new FileInfo(filePath).Length;
                string text = File.ReadAllText(marker)?.Trim();
                return long.TryParse(text, System.Globalization.NumberStyles.Integer,
                           System.Globalization.CultureInfo.InvariantCulture, out long recorded)
                       && recorded == actual;
            }
            catch (Exception ex)
            {
                Log.Warning("[UWRExecutor] 读取完成标记失败（按未完成处理）: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>写入完成标记（只在确认落盘大小正确后调用）。</summary>
        private void WriteCompletionMarker()
        {
            string marker = CompletionMarkerPath;
            if (string.IsNullOrEmpty(marker)) return;
            try
            {
                long length = new FileInfo(DownloadResultPath).Length;
                File.WriteAllText(marker, length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Log.Warning("[UWRExecutor] 写入完成标记失败: {0}", ex.Message);
            }
        }

        /// <summary>删除完成标记（失败/取消/重新下载时调用）。</summary>
        private void DeleteCompletionMarker()
        {
            string marker = CompletionMarkerPath;
            if (string.IsNullOrEmpty(marker)) return;
            try { if (File.Exists(marker)) File.Delete(marker); }
            catch (Exception ex) { Log.Warning("[UWRExecutor] 删除完成标记失败: {0}", ex.Message); }
        }
    }
}
