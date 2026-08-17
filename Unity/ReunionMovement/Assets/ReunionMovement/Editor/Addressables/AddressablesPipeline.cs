using System;
using System.IO;
using System.Text;
using ReunionMovement.Common;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 一键流水线：配置 → 迁移（UI/音频/图片）→ 构建 Content（version.json 含 catalogHash/remoteCatalogFolder）
    /// → 上传 OSS（含远程 catalog_*.bin/.hash，增量）。
    ///
    /// 两种触发方式：
    /// 1. 编辑器菜单：ReunionMovement → Addressables → 一键流水线（迁移+构建+上传 OSS）
    /// 2. 批处理自动化（CI / 命令行）：
    ///    Unity.exe -batchmode -projectPath &lt;项目路径&gt; -executeMethod ReunionMovement.EditorTools.Addressables.AddressablesPipeline.RunPipeline -quit -logFile Logs/addressables_pipeline.log
    ///
    /// 目标平台固定 WebGL（CDN 部署平台）。批处理下全程无弹窗；结果写入
    /// Build/Addressables/pipeline_result.txt，便于退出后核对。
    /// 注意：OSS 上传依赖本机 EditorPrefs 中的凭据（RM.OSS.*）；未配置时跳过上传、不阻断构建。
    /// </summary>
    public static class AddressablesPipeline
    {
        /// <summary>流水线结果输出文件（相对项目根）</summary>
        private const string ResultFile = "Build/Addressables/pipeline_result.txt";

        /// <summary>编辑器菜单入口（目标平台 WebGL）</summary>
        [MenuItem("ReunionMovement/Addressables/一键流水线（迁移+构建+上传 OSS）", priority = 0)]
        public static void RunPipelineMenu()
        {
            RunPipeline(BuildTarget.WebGL);
        }

        /// <summary>批处理入口（-executeMethod）：默认 WebGL 平台（CDN 部署平台）</summary>
        public static void RunPipeline()
        {
            RunPipeline(BuildTarget.WebGL);
        }

        /// <summary>执行完整流水线（幂等：迁移跳过已存在，上传增量对比）</summary>
        private static void RunPipeline(BuildTarget target)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== Addressables 一键流水线开始 =====");
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 目标平台: {target}");

            try
            {
                // 0. 确保配置（分组 / Label / Profile / 激活 DevLocal）
                AddressablesSetup.EnsureSetup();
                sb.AppendLine("[1/4] 配置就绪（分组 / Label / Profile）");

                // 1. 资源迁移（统一入口，幂等：目标已存在则跳过复制，只补齐标记）
                AddressablesMigrator.MigrateAll();
                sb.AppendLine("[2/4] 资源迁移完成（UI + 音频 + 图片）");

                // 2. 构建 Content（远程 Catalog 自动开启；version.json 含 catalogHash / remoteCatalogFolder）
                if (!AddressablesBuildWindow.BuildContentForTargetBatch(target, out string buildError))
                {
                    throw new Exception("构建 Content 失败: " + buildError);
                }
                sb.AppendLine($"[3/4] 构建完成（{target}），version.json 已更新");

                // 3. 上传 OSS（增量；含 catalog_*.bin/.hash；未配置凭据时跳过、不阻断）
                if (AddressablesCdnUploader.UploadToOSSBatch(out string uploadMessage))
                {
                    sb.AppendLine("[4/4] OSS 上传成功：" + uploadMessage);
                }
                else
                {
                    sb.AppendLine("[4/4] OSS 上传未执行/失败：" + uploadMessage);
                }

                sb.AppendLine("===== 流水线结束（成功） =====");
                Log.Debug(sb.ToString(), channel: LogChannel.Resource);
            }
            catch (Exception ex)
            {
                sb.AppendLine("===== 流水线失败 =====");
                sb.AppendLine(ex.ToString());
                Log.Error(sb.ToString(), channel: LogChannel.Resource);
            }

            // 结果落盘，便于批处理退出后核对
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Build", "Addressables");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "pipeline_result.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Warning("[AddressablesPipeline] 写入结果文件失败: " + ex.Message, channel: LogChannel.Resource);
            }
        }
    }
}
