using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
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
        [MenuItem("ReunionMovement/Addressables/构建 Content（当前平台）", priority = 1)]
        public static void BuildContent()
        {
            BuildContentForTarget(EditorUserBuildSettings.activeBuildTarget);
        }

        // ==================== 选择平台并构建（原生子菜单，悬停/点击即在菜单项右侧展开） ====================
        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/Windows (x86_64)", priority = 2)]
        public static void BuildWindows() => SwitchAndBuild(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/macOS (Universal)", priority = 2)]
        public static void BuildMacOS() => SwitchAndBuild(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/Linux (x86_64)", priority = 2)]
        public static void BuildLinux() => SwitchAndBuild(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/Android", priority = 2)]
        public static void BuildAndroid() => SwitchAndBuild(BuildTargetGroup.Android, BuildTarget.Android);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/iOS", priority = 2)]
        public static void BuildIOS() => SwitchAndBuild(BuildTargetGroup.iOS, BuildTarget.iOS);

        [MenuItem("ReunionMovement/Addressables/构建/选择平台并构建/WebGL", priority = 2)]
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
                    Debug.LogError($"[AddressablesBuild] 切换平台失败: {target}");
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
                    Debug.Log("[AddressablesBuild] 已自动启用远程 Catalog");
                }
            }

            if (!EditorUtility.DisplayDialog("Addressables 构建",
                    $"将按平台 [{target}] 构建全部 Addressables Content（含本地与远程 Bundle），是否继续？", "构建", "取消"))
                return;

            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result.Error != null)
            {
                Debug.LogError($"[AddressablesBuild] 构建失败: {result.Error}");
                EditorUtility.DisplayDialog("Addressables 构建", $"构建失败:\n{result.Error}", "确定");
                return;
            }

            WriteVersionManifest(result.OutputPath, target);
            Debug.Log($"[AddressablesBuild] 构建成功 [{target}]，版本清单: {OutputFolder}/{VersionFileName}");
            EditorUtility.DisplayDialog("Addressables 构建",
                $"构建成功 [{target}]\n已生成 version.json（含 catalog hash 与 Bundle 清单）", "确定");
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
        private static void WriteVersionManifest(string outputPath, BuildTarget target)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
            Directory.CreateDirectory(dir);

            // 注意：Addressables 2.9.1 的 result.OutputPath 是 settings.json 的【文件】路径
            // （如 Library/com.unity.addressables/aa/WebGL/settings.json），需取其所在目录作为产物目录。
            string buildFolder = ResolveBuildFolder(outputPath);

            string catalogFile = FindCatalogFile(buildFolder);
            string catalogHash = catalogFile != null ? Md5File(catalogFile) : "";

            var bundleFiles = new List<FileInfo>();
            if (!string.IsNullOrEmpty(buildFolder) && Directory.Exists(buildFolder))
            {
                foreach (var f in Directory.GetFiles(buildFolder, "*.bundle", SearchOption.AllDirectories))
                    bundleFiles.Add(new FileInfo(f));
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"version\": \"{PlayerSettings.bundleVersion}\",");
            sb.AppendLine($"  \"platform\": \"{target}\",");
            sb.AppendLine($"  \"buildTime\": \"{System.DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",");
            sb.AppendLine($"  \"catalogHash\": \"{catalogHash}\",");
            sb.AppendLine($"  \"catalogFile\": \"{Escape(catalogFile)}\",");
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
            foreach (var f in Directory.GetFiles(outputPath, "catalog_*.json", SearchOption.AllDirectories))
                return f;
            return null;
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
