using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;
using Newtonsoft.Json;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 文件操作工具类
    /// </summary>
    public static class FileOperationUtil
    {
        /// <summary>
        /// 获取文件名（包含后缀）或不包含后缀
        /// </summary>
        /// <param name="path"></param>
        /// <param name="withSuffix"></param>
        /// <returns></returns>
        public static string GetFileName(string path, bool withSuffix = true) => withSuffix ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);

        /// <summary>
        /// 获取文件夹下所有文件大小（单位KB，向上取整）
        /// 保留原方法签名但改为向上取整以减少精度损失
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static int GetAllFileSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long totalBytes = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch (Exception e)
                {
                    Log.Debug("GetAllFileSize: 无法读取文件大小 {0} -> {1}", file, e.Message);
                }
            }
            // 向上取整到KB
            return Convert.ToInt32(Math.Ceiling(totalBytes / 1024.0));
        }

        /// <summary>
        /// 获取文件夹下所有文件大小（字节，精确）
        /// 新增方法，返回精确字节数
        /// </summary>
        public static long GetAllFileSizeBytes(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long totalBytes = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch (Exception e)
                {
                    Log.Debug("GetAllFileSizeBytes: 无法读取文件大小 {0} -> {1}", file, e.Message);
                }
            }
            return totalBytes;
        }

        /// <summary>
        /// 获取指定文件大小（字节）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static long GetFileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

        /// <summary>
        /// 获取文件夹下所有文件名（不含.meta）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<string> GetAllFilesName(string path)
        {
            List<string> fileList = new List<string>();

            if (!Directory.Exists(path))
            {
                return fileList;
            }

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    fileList.Add(Path.GetFileName(file));
                }
            }

            return fileList;
        }

        /// <summary>
        /// 无视锁文件，直接读bytes，增加存在性检查和异常处理
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Log.Debug("ReadAllBytes: 文件不存在 {0}", path);
                return Array.Empty<byte>();
            }

            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var length = fs.Length;
                if (length == 0) return Array.Empty<byte>();
                var bytes = new byte[length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = fs.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                // 文件在读取期间被截断时 offset < bytes.Length：必须截断数组返回。
                // 原实现直接返回定长数组，尾部是 0 填充 —— 对调用方是静默的脏数据
                //（JSON 解析失败、二进制资源损坏，且没有任何提示）。
                if (offset != bytes.Length)
                {
                    Log.Warning("ReadAllBytes() 文件 {0} 在读取期间被截断（预期 {1} 字节，实际 {2} 字节）", path, bytes.Length, offset);
                    var truncated = new byte[offset];
                    if (offset > 0) Buffer.BlockCopy(bytes, 0, truncated, 0, offset);
                    return truncated;
                }
                return bytes;
            }
            catch (Exception e)
            {
                Log.Error("ReadAllBytes() 路径:{0}, 错误:{1}", path, e.Message);
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="fullpath">完整路径</param>
        /// <param name="content">内容</param>
        /// <returns></returns>
        public static async UniTask SaveFile(string fullpath, string content) => await SaveFileAsync(fullpath, Encoding.UTF8.GetBytes(content));

        /// <summary>
        /// 同步保存文件（Editor 工具等无异步上下文的场景使用，
        /// 避免 fire-and-forget 异步写盘与 AssetDatabase.Refresh 的竞态）
        /// </summary>
        /// <param name="fullpath"></param>
        /// <param name="content"></param>
        /// <returns>写入的 UTF-8 字节数，失败返回 -1</returns>
        public static int SaveFileSync(string fullpath, string content)
        {
            try
            {
                content ??= string.Empty;
                var dir = Path.GetDirectoryName(fullpath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                // 与 SaveFileAsync 保持同一语义：写入无 BOM 的 UTF-8 字节并返回真实字节数。
                // 原实现返回 content.Length（字符数），文档却写明"字节数"，
                // 含中文/emoji 时调用方按字节记账会明显偏小。
                var bytes = Encoding.UTF8.GetBytes(content);
                File.WriteAllBytes(fullpath, bytes);
                return bytes.Length;
            }
            catch (Exception e)
            {
                Log.Error("SaveFileSync() 路径:{0}, 错误:{1}", fullpath, e.Message);
                return -1;
            }
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="fullpath"></param>
        /// <param name="content"></param>
        /// <returns>写入字节数，失败返回 -1</returns>
        public static UniTask<int> SaveFileAsync(string fullpath, byte[] content)
        {
            try
            {
                content ??= Array.Empty<byte>();
                var dir = Path.GetDirectoryName(fullpath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 同步写入：File.WriteAllBytesAsync 走 FileStream 异步路径（依赖线程池），
                // 在 WebGL 上会导致浏览器挂死（Unity 官方文档：FileStream 异步方法不可用），
                // 故全平台统一用同步 IO。
                File.WriteAllBytes(fullpath, content);
                return UniTask.FromResult(content.Length);
            }
            catch (Exception e)
            {
                Log.Error("SaveFile() 路径:{0}, 错误:{1}", fullpath, e.Message);
                return UniTask.FromResult(-1);
            }
        }

        /// <summary>
        /// 非法文件名字符（与 SaveSystem 对齐，固定集合，防路径穿越写出 Json 目录）。
        /// 刻意不用 Path.GetInvalidFileNameChars()：它是平台相关的，同一文件名在 Windows 与
        /// Android/iOS 上会被净化为不同结果，导致跨端读写找不到同一文件。
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<char> s_invalidNameChars = BuildInvalidNameChars();

        private static System.Collections.Generic.HashSet<char> BuildInvalidNameChars()
        {
            var set = new System.Collections.Generic.HashSet<char>("<>:\"/\\|?*");
            for (char c = '\0'; c < ' '; c++)
            {
                set.Add(c);
            }
            return set;
        }

        /// <summary>
        /// 净化 Json 文件名：剥离目录分隔符与非法字符，防止外部输入含 ../ 写出 Json 目录。
        /// 净化结果为空时返回 null（调用方应放弃读写）。
        /// </summary>
        private static string SanitizeJsonName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var sb = new StringBuilder(fileName.Length);
            foreach (var c in fileName)
            {
                if (c == '\\' || c == '/' || s_invalidNameChars.Contains(c)) continue;
                sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// 加载Json，增加异常捕获并记录错误
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T LoadJson<T>(string fileName)
        {
            var safeName = SanitizeJsonName(fileName);
            if (safeName == null)
            {
                Log.Warning("LoadJson() 文件名为空或全部为非法字符: {0}", fileName);
                return default;
            }
            var fileAbslutePath = Path.Combine(Application.persistentDataPath, "Json", safeName + ".json");
            if (!File.Exists(fileAbslutePath))
            {
                return default;
            }

            try
            {
                var tempStr = File.ReadAllText(fileAbslutePath);
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
                return JsonConvert.DeserializeObject<T>(tempStr, settings);
            }
            catch (Exception e)
            {
                Log.Error("LoadJson() 路径:{0}, 错误:{1}", fileAbslutePath, e.Message);
                return default;
            }
        }

        /// <summary>
        /// 保存Json，新增返回写入是否成功的状态（true 成功，false 失败）
        /// </summary>
        /// <param name="jsonStr"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static UniTask<bool> SaveJson(string jsonStr, string fileName)
        {
            var safeName = SanitizeJsonName(fileName);
            if (safeName == null)
            {
                Log.Warning("SaveJson() 文件名为空或全部为非法字符: {0}", fileName);
                return UniTask.FromResult(false);
            }
            var filePath = Path.Combine(Application.persistentDataPath, "Json");
            try
            {
                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }
                var fileAbslutePath = Path.Combine(filePath, safeName + ".json");
                // 同步写入：File.WriteAllTextAsync 走 FileStream 异步路径（依赖线程池），
                // 在 WebGL 上会导致浏览器挂死，故全平台统一用同步 IO。
                File.WriteAllText(fileAbslutePath, jsonStr);
                return UniTask.FromResult(true);
            }
            catch (Exception e)
            {
                Log.Error("SaveJson() 路径:{0}, 文件:{1}, 错误:{2}", filePath, safeName, e.Message);
                return UniTask.FromResult(false);
            }
        }

        /// <summary>
        /// 净化相对路径片段：按 '/' 分段，丢弃空段、'.'、'..' 与非法字符。
        /// filePath 与 fileName 都参与字符串拼接，含 '../' 时可写出 persistentDataPath 之外。
        /// </summary>
        private static string SanitizeRelativePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return string.Empty;
            var parts = relativePath.Replace('\\', '/').Split('/');
            var sb = new StringBuilder(relativePath.Length);
            foreach (var part in parts)
            {
                if (part.Length == 0 || part == "." || part == "..") continue;
                var cleaned = SanitizeJsonName(part);
                if (string.IsNullOrEmpty(cleaned)) continue;
                if (sb.Length > 0) sb.Append('/');
                sb.Append(cleaned);
            }
            return sb.ToString();
        }

        /// <summary>按 '/' 分段做 URI 转义（不能整体转义，否则分隔符也会被编码）</summary>
        private static string EscapeUriSegments(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return string.Empty;
            var parts = relativePath.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Uri.EscapeDataString(parts[i]);
            }
            return string.Join("/", parts);
        }

        /// <summary>
        /// 游戏开始把StreamingAssets文件复制到持久化目录
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static IEnumerator CopyFileToTarget(string filePath, string fileName)
        {
            // 两个参数都会被拼进路径，必须先净化：原实现直接插值，
            // 传入 "../../.." 即可写到 persistentDataPath 之外
            filePath = SanitizeRelativePath(filePath);
            fileName = SanitizeJsonName(fileName);
            if (string.IsNullOrEmpty(fileName))
            {
                Log.Error("CopyFileToTarget 文件名非法（为空或全为非法字符）: {0}", fileName);
                yield break;
            }

            var originalPath = $"{Application.streamingAssetsPath}/{filePath}/{fileName}";
            var targetDir = $"{Application.persistentDataPath}/{filePath}";
            var targetPath = $"{targetDir}/{fileName}";

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    // 文件名可能含空格等字符：不转义时 '#' 会被当作 fragment、空格会破坏 URL，
                    // 结果是复制静默失败
                    using (var www = UnityWebRequest.Get($"{Application.streamingAssetsPath}/{EscapeUriSegments(filePath)}/{Uri.EscapeDataString(fileName)}"))
                    {
                        yield return www.SendWebRequest();
                        if (www.result != UnityWebRequest.Result.Success)
                        {
                            Log.Debug("复制文件失败：{0}", www.error);
                        }
                        else
                        {
                            try
                            {
                                File.WriteAllBytes(targetPath, www.downloadHandler.data);
                            }
                            catch (Exception e)
                            {
                                Log.Error("CopyFileToTarget 写入失败:{0} -> {1}", targetPath, e.Message);
                            }
                        }
                    }
                    break;
                case RuntimePlatform.IPhonePlayer:
                    originalPath = $"{Application.dataPath}/Raw/{filePath}/{fileName}";
                    if (!File.Exists(targetPath))
                    {
                        try
                        {
                            File.Copy(originalPath, targetPath);
                        }
                        catch (Exception e)
                        {
                            Log.Error("CopyFileToTarget 复制失败:{0} -> {1}", originalPath, e.Message);
                        }
                    }
                    break;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    if (!File.Exists(targetPath))
                    {
                        try
                        {
                            File.Copy(originalPath, targetPath);
                        }
                        catch (Exception e)
                        {
                            Log.Error("CopyFileToTarget 复制失败:{0} -> {1}", originalPath, e.Message);
                        }
                    }
                    break;
                case RuntimePlatform.WebGLPlayer:
                    // WebGL 的 StreamingAssets 走 HTTP，无法 File.Copy，需用 UnityWebRequest 拉取
                    // （此前无此分支：WebGL 平台静默跳过复制且无任何提示）
                    if (!File.Exists(targetPath))
                    {
                        using (var www = UnityWebRequest.Get(originalPath))
                        {
                            yield return www.SendWebRequest();
                            if (www.result != UnityWebRequest.Result.Success)
                            {
                                Log.Warning("CopyFileToTarget WebGL 复制失败：{0}", www.error);
                            }
                            else
                            {
                                try
                                {
                                    File.WriteAllBytes(targetPath, www.downloadHandler.data);
                                }
                                catch (Exception e)
                                {
                                    Log.Error("CopyFileToTarget WebGL 写入失败:{0} -> {1}", targetPath, e.Message);
                                }
                            }
                        }
                    }
                    break;
                default:
                    Log.Warning("CopyFileToTarget 未支持的平台 {0}，跳过复制 {1}", Application.platform, fileName);
                    break;
            }
            yield return null;
        }
    }
}
