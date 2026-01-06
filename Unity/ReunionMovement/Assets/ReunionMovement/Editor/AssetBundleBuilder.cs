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
using System.Net.Http;

namespace ReunionMovement.EditorTools
{
    public static class AssetBundleBuilder
    {
        private const string PrefKeySourceRoot = "ReunionMovement_AssetBundleSourceRoot";
        private const string PrefKeyUploadEndpoint = "ReunionMovement_AssetBundleUploadEndpoint";
        private const string PrefKeyManifestBaseUrl = "ReunionMovement_AssetBundleManifestBaseUrl";
        private const string PrefKeyUploadAuthToken = "ReunionMovement_AssetBundleUploadAuthToken";

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

            // 可选：上传到远端并在 manifest 中写入 URL
            string uploadEndpoint = EditorPrefs.GetString(PrefKeyUploadEndpoint, string.Empty);
            string manifestBaseUrl = EditorPrefs.GetString(PrefKeyManifestBaseUrl, string.Empty);
            string authToken = EditorPrefs.GetString(PrefKeyUploadAuthToken, string.Empty);

            if (!string.IsNullOrEmpty(uploadEndpoint) && !string.IsNullOrEmpty(manifestBaseUrl))
            {
                try
                {
                    UploadBundlesWithProgress(outputPath, bundleInfos, manifestPath, uploadEndpoint, manifestBaseUrl, authToken);
                    // Refresh manifestJson after upload: read updated manifest
                    manifestJson = File.ReadAllText(manifestPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError("上传 bundle 失败: " + ex.Message);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

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

        [MenuItem("工具箱/资源/上传 配置", false, 202)]
        public static void OpenUploadConfig()
        {
            UploadConfigWindow.ShowWindow();
        }

        private static void UploadBundlesWithProgress(string outputPath, List<BundleInfo> bundleInfos, string manifestPath, string uploadEndpoint, string manifestBaseUrl, string authToken)
        {
            // Ensure manifestBaseUrl has no trailing slash
            manifestBaseUrl = manifestBaseUrl.TrimEnd('/');

            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(authToken))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                }

                int total = bundleInfos.Count;
                for (int i = 0; i < total; i++)
                {
                    var info = bundleInfos[i];
                    string filePath = Path.Combine(outputPath, info.fileName);

                    float progress = (float)i / Math.Max(1, total);
                    if (EditorUtility.DisplayCancelableProgressBar("上传 AssetBundles", $"上传 {info.fileName} ({i + 1}/{total})", progress))
                    {
                        Debug.LogWarning("用户取消了上传");
                        break;
                    }

                    if (!File.Exists(filePath))
                    {
                        Debug.LogWarning($"上传跳过，未找到文件: {filePath}");
                        continue;
                    }

                    try
                    {
                        using (var content = new MultipartFormDataContent())
                        {
                            var fileBytes = File.ReadAllBytes(filePath);
                            var fileContent = new ByteArrayContent(fileBytes);
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                            content.Add(fileContent, "file", info.fileName);

                            var resp = client.PostAsync(uploadEndpoint, content).GetAwaiter().GetResult();
                            if (!resp.IsSuccessStatusCode)
                            {
                                Debug.LogError($"上传文件失败: {info.fileName}, 状态码: {resp.StatusCode}");
                                continue;
                            }

                            // 解析服务器返回的 body，优先取 JSON 中的 url 字段
                            try
                            {
                                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                                if (!string.IsNullOrEmpty(body))
                                {
                                    // 尝试解析 JSON { "url": "https://..." }
                                    try
                                    {
                                        var uploadResp = JsonUtility.FromJson<UploadResponse>(body);
                                        if (uploadResp != null && !string.IsNullOrEmpty(uploadResp.url))
                                        {
                                            info.url = uploadResp.url;
                                        }
                                        else
                                        {
                                            // fallback to manifestBaseUrl
                                            info.url = manifestBaseUrl + "/" + info.fileName;
                                        }
                                    }
                                    catch
                                    {
                                        info.url = manifestBaseUrl + "/" + info.fileName;
                                    }
                                }
                                else
                                {
                                    info.url = manifestBaseUrl + "/" + info.fileName;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"解析上传响应失败: {ex.Message}");
                                info.url = manifestBaseUrl + "/" + info.fileName;
                            }

                            Debug.Log($"上传成功: {info.fileName} -> {info.url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"上传 {info.fileName} 出错: {ex.Message}");
                    }
                }

                // 更新 manifest 文件 with URLs
                try
                {
                    var updatedManifest = new BundleManifest { bundles = bundleInfos };
                    var json = updatedManifest.ToJson();
                    File.WriteAllText(manifestPath, json, Encoding.UTF8);

                    // 上传 manifest itself
                    try
                    {
                        using (var content = new MultipartFormDataContent())
                        {
                            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            content.Add(fileContent, "file", "bundle_manifest.json");

                            var resp = client.PostAsync(uploadEndpoint, content).GetAwaiter().GetResult();
                            if (!resp.IsSuccessStatusCode)
                            {
                                Debug.LogError($"上传 manifest 失败, 状态码: {resp.StatusCode}");
                            }
                            else
                            {
                                Debug.Log("manifest 上传成功");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("上传 manifest 过程中出错: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("写入/上传 manifest 失败: " + ex.Message);
                }
            }
        }

        [Serializable]
        private class UploadResponse
        {
            public string url;
        }

        // 简单的 EditorWindow 用于设置上传配置
        public class UploadConfigWindow : EditorWindow
        {
            private string uploadEndpoint;
            private string manifestBaseUrl;
            private string authToken;

            public static void ShowWindow()
            {
                var win = GetWindow<UploadConfigWindow>(true, "AssetBundle 上传配置");
                win.minSize = new Vector2(600, 150);
                win.Load();
                win.Show();
            }

            void Load()
            {
                uploadEndpoint = EditorPrefs.GetString(PrefKeyUploadEndpoint, string.Empty);
                manifestBaseUrl = EditorPrefs.GetString(PrefKeyManifestBaseUrl, string.Empty);
                authToken = EditorPrefs.GetString(PrefKeyUploadAuthToken, string.Empty);
            }

            void OnGUI()
            {
                GUILayout.Label("上传配置", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                uploadEndpoint = EditorGUILayout.TextField(new GUIContent("上传接口(POST):", "接收文件的 HTTP POST 接口，字段名为 'file'"), uploadEndpoint);
                manifestBaseUrl = EditorGUILayout.TextField(new GUIContent("Manifest 基础 URL:", "客户端访问 bundle 的基础 URL，例如 https://cdn.example.com/assets"), manifestBaseUrl);
                authToken = EditorGUILayout.TextField(new GUIContent("Bearer Token (可选):", "如果服务器需要认证，请填入 Bearer token"), authToken);

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存"))
                {
                    EditorPrefs.SetString(PrefKeyUploadEndpoint, uploadEndpoint ?? string.Empty);
                    EditorPrefs.SetString(PrefKeyManifestBaseUrl, manifestBaseUrl ?? string.Empty);
                    EditorPrefs.SetString(PrefKeyUploadAuthToken, authToken ?? string.Empty);
                    Close();
                }
                if (GUILayout.Button("取消"))
                {
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif