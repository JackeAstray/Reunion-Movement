using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 构建工具 —— 一键构建 Content 并生成部署版本清单 version.json。
    /// 菜单：ReunionMovement → Addressables → 构建 Content（生成 version.json）
    /// 设计文档：仓库 Docs/Addressables/Addressables集成设计方案.md §6
    /// </summary>
    public static class AddressablesBuildWindow
    {
        /// <summary>版本清单输出目录（相对项目根）</summary>
        private const string OutputFolder = "Build/Addressables";
        private const string VersionFileName = "version.json";

        /// <summary>按当前激活平台构建 Content（Addressables 自动适配当前平台）</summary>
        [MenuItem("ReunionMovement/Addressables/构建/构建 Content（当前平台）", priority = 20)]
        public static void BuildContent()
        {
            BuildContentForTarget(EditorUserBuildSettings.activeBuildTarget);
        }

        // ==================== 选择平台并构建（原生子菜单，悬停/点击即在菜单项右侧展开） ====================
        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/Windows (x86_64)", priority = 21)]
        public static void BuildWindows() => SwitchAndBuild(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/macOS (Universal)", priority = 21)]
        public static void BuildMacOS() => SwitchAndBuild(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/Linux (x86_64)", priority = 21)]
        public static void BuildLinux() => SwitchAndBuild(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/Android", priority = 21)]
        public static void BuildAndroid() => SwitchAndBuild(BuildTargetGroup.Android, BuildTarget.Android);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/iOS", priority = 21)]
        public static void BuildIOS() => SwitchAndBuild(BuildTargetGroup.iOS, BuildTarget.iOS);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/WebGL", priority = 21)]
        public static void BuildWebGL() => SwitchAndBuild(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        /// <summary>切换到目标平台后构建（切换会触发资源重导入，延迟一帧执行）</summary>
        private static void SwitchAndBuild(BuildTargetGroup group, BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (!EditorUtility.DisplayDialog("Addressables 构建",
                        $"将切换目标平台到 {target} 并构建，是否继续？", "切换并构建", "取消"))
                    return;

                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                {
                    Log.Error($"[AddressablesBuild] 切换平台失败: {target}", channel: LogChannel.Resource);
                    return;
                }
                EditorApplication.delayCall += () => BuildContentForTarget(target);
                return;
            }

            BuildContentForTarget(target);
        }

        /// <summary>按指定平台构建 Content 并生成 version.json</summary>
        private static void BuildContentForTarget(BuildTarget target)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                if (!EditorUtility.DisplayDialog("Addressables 构建", "尚未创建 Addressables 配置，是否现在创建？", "创建", "取消"))
                    return;
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                AddressablesSetup.EnsureSetup();
            }

            // 远程 Catalog 检测：项目含 Remote_* 分组但未启用远程 Catalog 时提醒（不强制）
            if (!settings.BuildRemoteCatalog && HasRemoteGroups(settings))
            {
                int choice = EditorUtility.DisplayDialogComplex("Addressables 构建",
                    "检测到项目包含 Remote_* 分组，但「远程 Catalog」未启用（默认关闭）。\n\n" +
                    "未启用时不会生成 catalog_*.json，客户端将无法检测远程更新（热更失效）。\n\n" +
                    "是否现在启用？",
                    "启用并构建", "仅构建", "取消");
                if (choice == 2) return; // 取消
                if (choice == 0)
                {
                    settings.BuildRemoteCatalog = true;
                    settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                    settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                    AssetDatabase.SaveAssets();
                    Log.Debug("[AddressablesBuild] 已自动启用远程 Catalog", channel: LogChannel.Resource);
                }
            }

            if (!EditorUtility.DisplayDialog("Addressables 构建",
                    $"将按平台 [{target}] 构建全部 Addressables Content（含本地与远程 Bundle），是否继续？", "构建", "取消"))
                return;

            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result.Error != null)
            {
                Log.Error($"[AddressablesBuild] 构建失败: {result.Error}", channel: LogChannel.Resource);
                EditorUtility.DisplayDialog("Addressables 构建", $"构建失败:\n{result.Error}", "确定");
                return;
            }

            WriteVersionManifest(settings, result.OutputPath, target);
            Log.Debug($"[AddressablesBuild] 构建成功 [{target}]，版本清单: {OutputFolder}/{VersionFileName}", channel: LogChannel.Resource);
            EditorUtility.DisplayDialog("Addressables 构建",
                $"构建成功 [{target}]\n已生成 version.json（含 catalog hash 与 Bundle 清单）", "确定");
        }

        /// <summary>
        /// 批处理 / 流水线安全构建入口（无弹窗）：确保远程 Catalog 开启 → 切换目标平台（如需）→
        /// 构建 Content → 写 version.json（含 catalogHash / remoteCatalogFolder）。
        /// 返回是否成功；失败原因经 error 输出。供 AddressablesPipeline / CI（-executeMethod）调用。
        /// </summary>
        public static bool BuildContentForTargetBatch(BuildTarget target, out string error)
        {
            error = null;
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
                if (settings == null)
                {
                    settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                    AddressablesSetup.EnsureSetup();
                }

                // 远程 Catalog 检测：含 Remote_* 分组但未开启时自动开启（批处理无弹窗，直接启用）
                if (!settings.BuildRemoteCatalog && HasRemoteGroups(settings))
                {
                    settings.BuildRemoteCatalog = true;
                    settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                    settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                    AssetDatabase.SaveAssets();
                    Log.Debug("[AddressablesBuild] 批处理：已自动启用远程 Catalog", channel: LogChannel.Resource);
                }

                // 切换目标平台（仅当不一致；批处理下同步完成）
                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    var group = BuildPipeline.GetBuildTargetGroup(target);
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                    {
                        error = $"切换平台失败: {target}";
                        return false;
                    }
                }

                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (result.Error != null)
                {
                    error = result.Error;
                    return false;
                }

                WriteVersionManifest(settings, result.OutputPath, target);
                Log.Debug($"[AddressablesBuild] 批处理构建成功 [{target}]，version.json 已更新", channel: LogChannel.Resource);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>是否存在任何 Remote_* 前缀的分组</summary>
        private static bool HasRemoteGroups(AddressableAssetSettings settings)
        {
            foreach (var g in settings.groups)
            {
                if (g != null && g.Name.StartsWith("Remote_", System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 写入部署版本清单：version / platform / buildTime / catalogHash / bundle 文件清单（md5+size）。
        /// 供 CI 或人工上传 Remote Bundle 到 CDN 后核对（每个平台一份，按平台目录区分）。
        /// </summary>
        private static void WriteVersionManifest(AddressableAssetSettings settings, string outputPath, BuildTarget target)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
            Directory.CreateDirectory(dir);

            // 注意：Addressables 2.9.1 的 result.OutputPath 是 settings.json 的【文件】路径
            // （如 Library/com.unity.addressables/aa/WebGL/settings.json），需取其所在目录作为产物目录。
            string buildFolder = ResolveBuildFolder(outputPath);

            // 远程 Catalog 输出目录（Remote.BuildPath 求值，如 ServerData/WebGL）。
            // 远程 catalog（catalog_*.bin/.hash）不落在构建输出目录，需单独定位，否则 catalogHash 恒为空。
            string remoteCatalogFolder = ResolveRemoteCatalogFolder(settings, target);

            // 远程 catalog 优先在远程输出目录查找，其次构建输出目录（兼容旧布局）
            string catalogFile = FindCatalogFile(remoteCatalogFolder) ?? FindCatalogFile(buildFolder);
            string catalogHash = catalogFile != null ? Md5File(catalogFile) : "";

            var bundleFiles = new List<FileInfo>();
            if (!string.IsNullOrEmpty(buildFolder) && Directory.Exists(buildFolder))
            {
                foreach (var f in Directory.GetFiles(buildFolder, "*.bundle", SearchOption.AllDirectories))
                    bundleFiles.Add(new FileInfo(f));
            }
            // 远程分组 bundle 输出到 Remote.BuildPath（ServerData/{platform}），一并纳入部署清单
            if (!string.IsNullOrEmpty(remoteCatalogFolder) && Directory.Exists(remoteCatalogFolder))
            {
                foreach (var f in Directory.GetFiles(remoteCatalogFolder, "*.bundle", SearchOption.AllDirectories))
                    bundleFiles.Add(new FileInfo(f));
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"version\": \"{PlayerSettings.bundleVersion}\",");
            sb.AppendLine($"  \"platform\": \"{target}\",");
            sb.AppendLine($"  \"buildTime\": \"{System.DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",");
            sb.AppendLine($"  \"catalogHash\": \"{catalogHash}\",");
            sb.AppendLine($"  \"catalogFile\": \"{Escape(catalogFile)}\",");
            sb.AppendLine($"  \"remoteCatalogFolder\": \"{Escape(remoteCatalogFolder)}\",");
            sb.AppendLine($"  \"remoteUploadFolder\": \"{Escape(buildFolder)}\",");
            sb.AppendLine($"  \"bundleCount\": {bundleFiles.Count},");
            sb.AppendLine("  \"bundles\": [");
            for (int i = 0; i < bundleFiles.Count; i++)
            {
                var f = bundleFiles[i];
                sb.Append("    { \"name\": \"").Append(f.Name)
                  .Append("\", \"md5\": \"").Append(Md5File(f.FullName))
                  .Append("\", \"size\": ").Append(f.Length).Append(" }");
                sb.AppendLine(i < bundleFiles.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(dir, VersionFileName), sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 解析构建产物目录：OutputPath 可能是 settings.json 文件路径，也可能是目录，
        /// 统一返回其中的【目录】路径；无法解析时原样返回。
        /// </summary>
        private static string ResolveBuildFolder(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return outputPath;
            if (Directory.Exists(outputPath)) return outputPath;

            string parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                return parent;

            return outputPath;
        }

        private static string FindCatalogFile(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath)) return null;
            // Addressables 2.9.x 默认生成二进制 catalog（catalog_*.bin + .hash）；
            // 兼容旧版 JSON catalog（catalog_*.json，需 m_EnableJsonCatalog=true）。
            foreach (var f in Directory.GetFiles(outputPath, "catalog_*.bin", SearchOption.AllDirectories))
                return f;
            foreach (var f in Directory.GetFiles(outputPath, "catalog_*.json", SearchOption.AllDirectories))
                return f;
            return null;
        }

        /// <summary>
        /// 解析远程 Catalog 输出目录：对 Remote.BuildPath 变量求值（如 "ServerData/[BuildTarget]" → ServerData/WebGL）。
        /// 求值失败或目录不存在时按约定 "ServerData/{target}" 兜底。
        /// </summary>
        private static string ResolveRemoteCatalogFolder(AddressableAssetSettings settings, BuildTarget target)
        {
            try
            {
                string folder = settings.RemoteCatalogBuildPath.GetValue(settings);
                if (!string.IsNullOrEmpty(folder))
                {
                    folder = folder.Replace("[BuildTarget]", target.ToString());
                    string full = Path.GetFullPath(folder);
                    if (Directory.Exists(full))
                    {
                        return Escape(full);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AddressablesBuild] 解析远程 Catalog 目录失败（使用默认 ServerData/{target}）: {ex.Message}", channel: LogChannel.Resource);
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "ServerData", target.ToString()).Replace('\\', '/');
        }

        private static string Md5File(string path)
        {
            using var md5 = MD5.Create();
            using var fs = File.OpenRead(path);
            var hash = md5.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string Escape(string s) => s?.Replace("\\", "/") ?? "";
    }
}
