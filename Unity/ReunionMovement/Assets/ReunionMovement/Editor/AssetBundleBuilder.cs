#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ReunionMovement.Core.Resources;
using ReunionMovement.Common.Util;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools
{
    public static class AssetBundleBuilder
    {
        private const string PrefKeySourceRoot = "ReunionMovement_AssetBundleSourceRoot";

        [MenuItem("工具箱/资源/构建 AssetBundles 并生成 manifest", false, 200)]
        public static void BuildAndCreateManifest()
        {
            // 允许通过 EditorPrefs 配置源文件夹，默认值为原来的路径
            string defaultSource = "Assets/ReunionMovement/AssetBundlesToBuild";
            string sourceRoot = EditorPrefs.GetString(PrefKeySourceRoot, defaultSource);

            // 源文件夹，按子目录打包
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError($"源目录不存在: {sourceRoot}, 请通过 工具箱/资源/设置 AssetBundle 源目录 设置正确的目录，或创建并放入要打包的资源（每个子目录打包为一个bundle）");
                return;
            }

            string outputPath = PathUtil.GetLocalPath(DownloadType.StreamingAssets) + "/AssetBundles";
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            var subdirs = Directory.GetDirectories(sourceRoot);
            if (subdirs.Length == 0)
            {
                Debug.LogError($"未在 {sourceRoot} 下找到子目录，无法生成 bundle");
                return;
            }

            var builds = new List<AssetBundleBuild>();
            var bundleInfos = new List<BundleInfo>();

            foreach (var dir in subdirs)
            {
                var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.Replace("\\", "/"))
                    .ToArray();

                var assetPaths = files
                    .Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (assetPaths.Length == 0) continue;

                var bundleName = Path.GetFileName(dir).ToLowerInvariant() + ".bundle";
                var build = new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = assetPaths
                };
                builds.Add(build);

                var info = new BundleInfo
                {
                    name = Path.GetFileName(dir),
                    fileName = bundleName,
                    dependencies = Array.Empty<string>(),
                    version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                    url = string.Empty
                };
                bundleInfos.Add(info);
            }

            if (builds.Count == 0)
            {
                Debug.LogError("没有可打包的资源");
                return;
            }

            // 构建
            Debug.Log($"开始构建 {builds.Count} 个 bundle 到 {outputPath}");
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
            if (manifest == null)
            {
                Debug.LogError("构建失败: manifest 为 null");
                return;
            }

            // 填充依赖信息
            foreach (var info in bundleInfos)
            {
                var deps = manifest.GetAllDependencies(info.fileName);
                info.dependencies = deps ?? Array.Empty<string>();

                // 计算版本为文件 MD5
                string filePath = Path.Combine(outputPath, info.fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        using (var fs = File.OpenRead(filePath))
                        using (var md5 = MD5.Create())
                        {
                            var hash = md5.ComputeHash(fs);
                            info.version = BitConverter.ToString(hash).Replace("-", "");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("计算 MD5 失败: " + ex.Message);
                        info.version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    }
                }
            }

            var bundleManifest = new BundleManifest { bundles = bundleInfos };
            var manifestJson = bundleManifest.ToJson();

            string manifestPath = Path.Combine(outputPath, "bundle_manifest.json");
            File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);

            // 复制到 PersistentAssets 便于运行时访问
            string persistentPath = PathUtil.GetLocalPath(DownloadType.PersistentAssets);
            if (!Directory.Exists(persistentPath)) Directory.CreateDirectory(persistentPath);
            File.Copy(manifestPath, Path.Combine(persistentPath, "bundle_manifest.json"), true);

            AssetDatabase.Refresh();
            Debug.Log($"构建完成，manifest 生成于 {manifestPath} 并复制到 {persistentPath}");
        }

        [MenuItem("工具箱/资源/设置 AssetBundle 源目录", false, 201)]
        public static void SetSourceRoot()
        {
            string defaultSource = "Assets/ReunionMovement/AssetBundlesToBuild";
            string current = EditorPrefs.GetString(PrefKeySourceRoot, defaultSource);

            // 使用 OpenFolderPanel 让用户选择目录
            string startFolder = Application.dataPath;
            if (!string.IsNullOrEmpty(current) && current.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var candidate = Path.Combine(Application.dataPath, current.Substring("Assets/".Length));
                    if (Directory.Exists(candidate)) startFolder = candidate;
                }
                catch { }
            }

            string folder = EditorUtility.OpenFolderPanel("选择 AssetBundle 源目录", startFolder, "");
            if (string.IsNullOrEmpty(folder)) return;

            // 如果选择的是项目内路径，将其转换为以 Assets/ 开头的相对路径
            string projectPath = Application.dataPath.TrimEnd('/', '\\');
            string selected = folder.TrimEnd('/', '\\');
            if (selected.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                string rel = "Assets" + selected.Substring(projectPath.Length).Replace("\\", "/");
                EditorPrefs.SetString(PrefKeySourceRoot, rel);
                Debug.Log($"已设置 AssetBundle 源目录: {rel}");
            }
            else
            {
                // 如果用户选择项目外目录，也允许，但要保存绝对路径
                EditorPrefs.SetString(PrefKeySourceRoot, selected);
                Debug.Log($"已设置 AssetBundle 源目录 (绝对路径): {selected}");
            }
        }
    }
}
#endif