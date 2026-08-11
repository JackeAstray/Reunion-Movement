using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 构建产物自动上传 —— 阿里云 OSS（HTTP PUT + 签名，纯 C#，无第三方 SDK）。
    ///
    /// 特性：
    /// - 增量上传：基于 Build/Addressables/upload_state.json 记录上次各文件 md5，仅上传变化文件（省流量、省请求）。
    /// - 目录结构：{prefix}/{platform}/{fileName}（prefix 默认 reunion/{version}，与远程部署指南 CDN 目录一致）。
    /// - 覆盖对象：remoteUploadFolder 内所有 .bundle + catalog_*.json。
    ///
    /// 配置：菜单 ReunionMovement → Addressables → OSS 上传配置…（Endpoint/Bucket/AK/SK/Prefix，存 EditorPrefs）。
    /// 上传：菜单 ReunionMovement → Addressables → 上传到 OSS（增量）
    ///
    /// 文档：Docs/Addressables/远程部署指南.md
    /// 注意：AK/SK 存于本机 EditorPrefs，请勿提交到版本库；建议使用最小权限的 RAM 子账号。
    /// </summary>
    public static class AddressablesCdnUploader
    {
        private const string VersionJsonPath = "Build/Addressables/version.json";
        private const string UploadStatePath = "Build/Addressables/upload_state.json";

        // EditorPrefs 键（本机持久化，不入库）
        private const string PrefEndpoint = "RM.OSS.Endpoint";
        private const string PrefBucket = "RM.OSS.Bucket";
        private const string PrefAK = "RM.OSS.AccessKey";
        private const string PrefSK = "RM.OSS.SecretKey";
        private const string PrefPrefix = "RM.OSS.Prefix";
        private const string PrefVersion = "RM.OSS.Version";

        public static string Endpoint
        {
            get => EditorPrefs.GetString(PrefEndpoint, "https://oss-cn-hangzhou.aliyuncs.com");
            set => EditorPrefs.SetString(PrefEndpoint, value);
        }
        public static string Bucket
        {
            get => EditorPrefs.GetString(PrefBucket, "");
            set => EditorPrefs.SetString(PrefBucket, value);
        }
        public static string AccessKey
        {
            get => EditorPrefs.GetString(PrefAK, "");
            set => EditorPrefs.SetString(PrefAK, value);
        }
        public static string SecretKey
        {
            get => EditorPrefs.GetString(PrefSK, "");
            set => EditorPrefs.SetString(PrefSK, value);
        }
        /// <summary>对象前缀，支持 {version} 占位，如 reunion/{version}</summary>
        public static string Prefix
        {
            get => EditorPrefs.GetString(PrefPrefix, "reunion/{version}");
            set => EditorPrefs.SetString(PrefPrefix, value);
        }
        /// <summary>手动指定 version（覆盖 version.json 的 version 字段；留空则用 version.json）</summary>
        public static string VersionOverride
        {
            get => EditorPrefs.GetString(PrefVersion, "");
            set => EditorPrefs.SetString(PrefVersion, value);
        }

        // ============================================================
        //  上传入口（独立菜单触发）
        // ============================================================
        /// <summary>打开 OSS 上传配置窗口（Endpoint / Bucket / AK / SK / Prefix / Version）</summary>
        [MenuItem("ReunionMovement/Addressables/OSS 上传配置…", priority = 3)]
        public static void OpenConfigWindow()
        {
            OSSUploadConfigWindow.Open();
        }

        [MenuItem("ReunionMovement/Addressables/上传到 OSS（增量）", priority = 4)]
        public static void UploadToOSS()
        {
            // 1. 配置校验
            if (string.IsNullOrEmpty(Bucket) || string.IsNullOrEmpty(AccessKey) || string.IsNullOrEmpty(SecretKey))
            {
                EditorUtility.DisplayDialog("上传到 OSS",
                    "尚未配置 OSS 参数（Bucket / AK / SK）。\n请先执行：ReunionMovement → Addressables → OSS 上传配置…",
                    "去配置", "取消");
                EditorWindow.GetWindow<OSSUploadConfigWindow>("OSS 上传配置");
                return;
            }

            // 2. 读取 version.json
            if (!File.Exists(VersionJsonPath))
            {
                EditorUtility.DisplayDialog("上传到 OSS",
                    $"未找到 {VersionJsonPath}。\n请先执行「构建 Content」生成版本清单。", "确定");
                return;
            }
            string manifest = File.ReadAllText(VersionJsonPath);

            string uploadFolder = ReadJsonString(manifest, "remoteUploadFolder");
            string platform = ReadJsonString(manifest, "platform");
            string buildVersion = ReadJsonString(manifest, "version");

            // 容错：remoteUploadFolder 可能是目录，也可能是 settings.json 文件路径（旧版 version.json），
            // 若指向文件则取其所在目录。
            if (!string.IsNullOrEmpty(uploadFolder) && !Directory.Exists(uploadFolder))
            {
                string parent = Path.GetDirectoryName(uploadFolder);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    uploadFolder = parent;
            }
            if (string.IsNullOrEmpty(uploadFolder) || !Directory.Exists(uploadFolder))
            {
                EditorUtility.DisplayDialog("上传到 OSS",
                    $"version.json 中 remoteUploadFolder 无效：{uploadFolder}\n请确认已构建 Content。", "确定");
                return;
            }

            string version = string.IsNullOrEmpty(VersionOverride) ? buildVersion : VersionOverride;
            string prefix = Prefix.Replace("{version}", version);

            // 3. 收集待上传文件（.bundle + catalog_*.json，递归）
            var files = new List<string>();
            foreach (var f in Directory.GetFiles(uploadFolder, "*.bundle", SearchOption.AllDirectories))
                files.Add(f);
            foreach (var f in Directory.GetFiles(uploadFolder, "catalog_*.json", SearchOption.AllDirectories))
                files.Add(f);
            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("上传到 OSS", "remoteUploadFolder 下未找到 .bundle 或 catalog_*.json，无可上传内容。", "确定");
                return;
            }

            // 4. 加载上次上传状态，计算增量
            var prevState = LoadUploadState();
            var toUpload = new List<string>();
            var skipped = 0;
            foreach (var f in files)
            {
                string rel = Path.GetFileName(f);
                string key = $"{prefix}/{platform}/{rel}";
                string md5 = Md5File(f);
                if (prevState.TryGetValue(key, out string prevMd5) && prevMd5 == md5)
                {
                    skipped++; // md5 未变，跳过
                    continue;
                }
                toUpload.Add(f);
            }

            if (toUpload.Count == 0)
            {
                EditorUtility.DisplayDialog("上传到 OSS",
                    $"增量对比完成：全部 {files.Count} 个文件已是最新（跳过 {skipped} 个），无需上传。", "确定");
                return;
            }

            // 5. 确认
            if (!EditorUtility.DisplayDialog("上传到 OSS",
                    $"将上传 {toUpload.Count} 个文件（跳过 {skipped} 个已是最新）到：\n{prefix}/{platform}/\nBucket: {Bucket}\n\n是否继续？",
                    "上传", "取消"))
                return;

            // 6. 逐个上传
            int ok = 0;
            var errors = new List<string>();
            var newState = new Dictionary<string, string>(prevState);
            for (int i = 0; i < toUpload.Count; i++)
            {
                string f = toUpload[i];
                string rel = Path.GetFileName(f);
                string key = $"{prefix}/{platform}/{rel}";
                EditorUtility.DisplayProgressBar("上传到 OSS",
                    $"[{i + 1}/{toUpload.Count}] {rel}", (float)i / toUpload.Count);

                string err;
                if (PutObject(key, f, out err))
                {
                    newState[key] = Md5File(f);
                    ok++;
                }
                else
                {
                    errors.Add($"{rel}: {err}");
                }
            }
            EditorUtility.ClearProgressBar();

            SaveUploadState(newState);

            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("上传到 OSS",
                    $"上传完成：{ok}/{toUpload.Count} 成功，跳过 {skipped} 个未变文件。", "确定");
                Debug.Log($"[OSSUpload] 完成：{ok}/{toUpload.Count}，跳过 {skipped}，目标 {prefix}/{platform}/");
            }
            else
            {
                string msg = $"成功 {ok}/{toUpload.Count}，失败 {errors.Count} 个：\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("上传到 OSS（部分失败）", msg, "确定");
                Debug.LogError($"[OSSUpload] 部分失败：\n{msg}");
            }
        }

        // ============================================================
        //  OSS PUT 上传（Header 携带签名）
        // ============================================================
        /// <summary>
        /// 规范化 Bucket 名称：若误填了完整 Endpoint（如 lla-game-asset.oss-cn-beijing.aliyuncs.com），
        /// 自动剥离 ".oss-xxx.aliyuncs.com" 后缀，只保留纯桶名（如 lla-game-asset）。
        /// </summary>
        internal static string NormalizeBucketName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            string b = raw.Trim();
            // 处理用户可能粘贴了完整 URL（带协议）
            int scheme = b.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) b = b.Substring(scheme + 3);
            // 处理带路径（如 bucket.oss-xxx.aliyuncs.com/... 或 /bucket）
            int slash = b.IndexOf('/');
            if (slash >= 0) b = b.Substring(0, slash);
            // 剥离 .oss-...aliyuncs.com 后缀
            int dot = b.IndexOf(".oss-", StringComparison.OrdinalIgnoreCase);
            if (dot > 0) b = b.Substring(0, dot);
            return b;
        }

        /// <summary>上传单个文件到 OSS，返回是否成功</summary>
        private static bool PutObject(string objectKey, string localPath, out string error)
        {
            error = null;
            try
            {
                // OSS 为 HTTPS，确保使用 TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                string bucket = NormalizeBucketName(Bucket);
                if (string.IsNullOrEmpty(bucket))
                {
                    error = "Bucket 名称为空，请先在「OSS 上传配置…」填写。";
                    return false;
                }

                DateTime utcNow = DateTime.UtcNow;
                string date = utcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                    System.Globalization.CultureInfo.InvariantCulture);
                string contentType = objectKey.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? "application/json"
                    : "application/octet-stream";
                string canonicalResource = $"/{bucket}/{objectKey}";

                // OSS Header 签名规则（StringToSign）：
                //   VERB\n Content-MD5\n Content-Type\n Date\n CanonicalizedOSSHeaders\n CanonicalizedResource
                // 注意：x-oss-* 自定义头必须小写并按字母序参与签名，且须与请求头完全一致。
                // 不设置对象 ACL：阿里云默认禁止 Put public object acl（AccessDenied），
                // 对象公共可读改为依赖 Bucket 级权限（OSS 控制台把 Bucket 设为公共读即可）。
                string stringToSign = $"PUT\n\n{contentType}\n{date}\n{canonicalResource}";

                using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(SecretKey)))
                {
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
                    string signature = Convert.ToBase64String(hash);
                    string authorization = $"OSS {AccessKey}:{signature}";

                    // Endpoint 允许不带协议填写（如 oss-cn-beijing.aliyuncs.com），自动补 https://
                    string endpoint = Endpoint.Trim().TrimEnd('/');
                    if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        endpoint = "https://" + endpoint;
                    }
                    var endpointUri = new Uri(endpoint);

                    // 阿里云 OSS 强制要求 virtual-hosted style（三级域名）访问：
                    //   https://{bucket}.oss-cn-beijing.aliyuncs.com/{object}
                    // 不允许 path-style（https://oss-cn-beijing.aliyuncs.com/{bucket}/{object}）
                    // （否则返回 403 SecondLevelDomainForbidden）
                    string url = $"https://{bucket}.{endpointUri.Host}/{objectKey}";
                    Debug.Log($"[OSSUpload] PUT -> {url}"); // 打印实际请求 URL，便于核对是否为三级域名
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "PUT";
                    req.Date = utcNow; // 与签名 date 保持一致
                    req.Headers["Authorization"] = authorization;
                    req.ContentType = contentType;

                    byte[] data = File.ReadAllBytes(localPath);
                    req.ContentLength = data.Length;
                    using (var stream = req.GetRequestStream())
                        stream.Write(data, 0, data.Length);

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        int code = (int)resp.StatusCode;
                        if (code < 200 || code >= 300)
                        {
                            error = $"HTTP {code}: {ReadResponseBody(resp)}";
                            return false;
                        }
                    }
                    return true;
                }
            }
            catch (WebException ex)
            {
                error = ex.Message;
                if (ex.Response is HttpWebResponse r)
                {
                    error += $" (HTTP {(int)r.StatusCode}): {ReadResponseBody(r)}";
                    // 附带本机签名串，便于与 OSS 返回的 StringToSign 对比
                    error += $" | 本机StringToSign=[{BuildStringToSign(objectKey)}]";
                }
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>读取 HTTP 错误/成功响应体文本（OSS 错误为 XML，含 Code/Message/StringToSign）</summary>
        private static string ReadResponseBody(HttpWebResponse resp)
        {
            try
            {
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(body)) return "(空响应体)";
                    // 截断，避免日志过长
                    return body.Length > 800 ? body.Substring(0, 800) : body;
                }
            }
            catch (Exception ex)
            {
                return $"(读取响应体失败: {ex.Message})";
            }
        }

        /// <summary>仅用于诊断：重建与 PutObject 相同的 StringToSign（不含 secret）</summary>
        private static string BuildStringToSign(string objectKey)
        {
            string contentType = objectKey.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "application/octet-stream";
            string date = DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                System.Globalization.CultureInfo.InvariantCulture);
            string bucket = NormalizeBucketName(Bucket);
            string canonicalResource = $"/{bucket}/{objectKey}";
            return $"PUT\n\n{contentType}\n{date}\n{canonicalResource}";
        }

        // ============================================================
        //  上传状态（增量记录）
        // ============================================================
        private static Dictionary<string, string> LoadUploadState()
        {
            var dict = new Dictionary<string, string>();
            if (!File.Exists(UploadStatePath)) return dict;
            try
            {
                string json = File.ReadAllText(UploadStatePath);
                // 简单解析 {"files": {"key": "md5", ...}}
                string filesSection = ExtractBetween(json, "\"files\"", "}");
                if (string.IsNullOrEmpty(filesSection)) return dict;
                foreach (var pair in SplitPairs(filesSection))
                {
                    string key = Unquote(SubBefore(pair, ':'));
                    string val = Unquote(SubAfter(pair, ':'));
                    if (!string.IsNullOrEmpty(key)) dict[key] = val;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OSSUpload] 读取上传状态失败（忽略，将全量对比）: {ex.Message}");
            }
            return dict;
        }

        private static void SaveUploadState(Dictionary<string, string> state)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"files\": {");
                int i = 0;
                foreach (var kv in state)
                {
                    sb.Append("    \"").Append(kv.Key).Append("\": \"").Append(kv.Value).Append("\"");
                    sb.AppendLine(++i < state.Count ? "," : "");
                }
                sb.AppendLine("  }");
                sb.AppendLine("}");
                string dir = Path.GetDirectoryName(Path.GetFullPath(UploadStatePath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(UploadStatePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OSSUpload] 写入上传状态失败: {ex.Message}");
            }
        }

        // ============================================================
        //  简易 JSON 取值（避免引入 JSON 库依赖，仅解析已知结构）
        // ============================================================
        private static string ReadJsonString(string json, string field)
        {
            int idx = json.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return "";
            int start = json.IndexOf('"', colon);
            if (start < 0) return "";
            int end = json.IndexOf('"', start + 1);
            if (end < 0) return "";
            return json.Substring(start + 1, end - start - 1);
        }

        private static string ExtractBetween(string s, string startToken, string endToken)
        {
            int i = s.IndexOf(startToken, StringComparison.Ordinal);
            if (i < 0) return "";
            i = s.IndexOf('{', i);
            if (i < 0) return "";
            int depth = 0;
            for (int j = i; j < s.Length; j++)
            {
                if (s[j] == '{') depth++;
                else if (s[j] == '}') { depth--; if (depth == 0) return s.Substring(i + 1, j - i - 1); }
            }
            return "";
        }

        private static string[] SplitPairs(string section)
        {
            var list = new List<string>();
            int depth = 0;
            var cur = new StringBuilder();
            for (int i = 0; i < section.Length; i++)
            {
                char c = section[i];
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                if (c == ',' && depth == 0) { list.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list.ToArray();
        }

        private static string SubBefore(string s, char c)
        {
            int i = s.IndexOf(c);
            return i < 0 ? s : s.Substring(0, i);
        }
        private static string SubAfter(string s, char c)
        {
            int i = s.IndexOf(c);
            return i < 0 ? "" : s.Substring(i + 1);
        }
        private static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2);
            return s;
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
    }

    /// <summary>
    /// OSS 上传配置窗口（Endpoint / Bucket / AK / SK / Prefix / Version）。
    /// 配置存 EditorPrefs（本机），AK/SK 请使用最小权限 RAM 子账号。
    /// </summary>
    public class OSSUploadConfigWindow : EditorWindow
    {
        private string endpoint;
        private string bucket;
        private string ak;
        private string sk;
        private string prefix;
        private string version;
        private bool showSecret;

        public static void Open()
        {
            var win = GetWindow<OSSUploadConfigWindow>("OSS 上传配置");
            win.minSize = new Vector2(460, 280);
        }

        private void OnEnable()
        {
            endpoint = AddressablesCdnUploader.Endpoint;
            bucket = AddressablesCdnUploader.Bucket;
            ak = AddressablesCdnUploader.AccessKey;
            sk = AddressablesCdnUploader.SecretKey;
            prefix = AddressablesCdnUploader.Prefix;
            version = AddressablesCdnUploader.VersionOverride;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("阿里云 OSS 上传配置", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("配置仅保存在本机（EditorPrefs），不会提交到版本库。", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            endpoint = EditorGUILayout.TextField("Endpoint", endpoint);
            bucket = EditorGUILayout.TextField("Bucket", bucket);
            ak = EditorGUILayout.TextField("AccessKey (AK)", ak);
            showSecret = EditorGUILayout.Toggle("显示 SecretKey", showSecret);
            sk = showSecret
                ? EditorGUILayout.TextField("SecretKey (SK)", sk)
                : EditorGUILayout.PasswordField("SecretKey (SK)", sk);
            prefix = EditorGUILayout.TextField("对象前缀 Prefix", prefix);
            EditorGUILayout.HelpBox("Prefix 支持 {version} 占位，如 reunion/{version}，将生成 reunion/v0.1.0/{platform}/ 目录。", MessageType.Info);
            version = EditorGUILayout.TextField("Version（留空用 version.json）", version);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存", GUILayout.Height(30)))
            {
                string ep = endpoint.Trim().TrimEnd('/');
                if (!ep.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !ep.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    ep = "https://" + ep;
                }
                AddressablesCdnUploader.Endpoint = ep;
                AddressablesCdnUploader.Bucket = AddressablesCdnUploader.NormalizeBucketName(bucket); // 自动剥离 .oss-xxx.aliyuncs.com 后缀
                AddressablesCdnUploader.AccessKey = ak.Trim();
                AddressablesCdnUploader.SecretKey = sk.Trim();
                AddressablesCdnUploader.Prefix = string.IsNullOrWhiteSpace(prefix) ? "reunion/{version}" : prefix.Trim();
                AddressablesCdnUploader.VersionOverride = version.Trim();
                EditorUtility.DisplayDialog("OSS 上传配置", "配置已保存。", "确定");
                Close();
            }
            if (GUILayout.Button("取消", GUILayout.Height(30)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("注意事项：", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. AK/SK 建议使用最小权限 RAM 子账号（仅 oss:PutObject）。\n" +
                "2. 上传不设置对象 ACL；请把 Bucket 权限设为「公共读」（阿里云默认禁止 public ACL，对象需靠 Bucket 级权限公开可读）。\n" +
                "3. 增量依据本机 Build/Addressables/upload_state.json，删除该文件即强制全量。",
                MessageType.Warning);
        }
    }
}
