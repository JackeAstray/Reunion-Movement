using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 路径工具类
    /// </summary>
    public static class PathUtil
    {
        // 将较长的前缀放在前面以确保正确匹配（例如 "file:///" 与 "file://"）
        public static readonly string[] pathHeadDefine = { "jar:file://", "jar://", "file:///", "file://", "https://", "http://", "ftp://", "content://", "data:" };

        /// <summary>
        /// 获取路径头
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static string GetPathHead(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            foreach (var head in pathHeadDefine)
            {
                if (path.StartsWith(head, StringComparison.OrdinalIgnoreCase))
                {
                    return head;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取规范的路径
        /// </summary>
        public static string GetRegularPath(string path) => string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');

        /// <summary>
        /// 验证路径（是否为真路径）
        /// </summary>
        public static bool IsSureDir(string path) => !string.IsNullOrEmpty(path) && (path.Contains("/") || path.Contains("\\"));

        /// <summary>
        /// 验证路径（是否为全路径）
        /// </summary>
        public static bool IsFullPath(string path) => !string.IsNullOrEmpty(path) && (path.Contains(":/") || path.Contains(":\\"));

        /// <summary>
        /// 持续化路径
        /// </summary>
        public static string AppDataPath() => Application.persistentDataPath + "/";

        /// <summary>
        /// 判断是否包含 URI scheme（如 http://、file://），比简单查找 "://" 更严格，避免把 "C:/" 误判为 scheme
        /// </summary>
        private static bool HasUriScheme(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            int idx = path.IndexOf("://", StringComparison.Ordinal);
            if (idx <= 0) return false;
            string scheme = path.Substring(0, idx);
            if (string.IsNullOrEmpty(scheme)) return false;
            // scheme 必须以字母开头，并且只包含允许的字符
            if (!char.IsLetter(scheme[0])) return false;
            return scheme.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '-' || c == '.');
        }

        /// <summary>
        /// 获取完整路径
        /// </summary>
        /// <param name="url"></param>
        /// <param name="newPath"></param>
        /// <returns></returns>
        public static bool GetFullPath(string url, out string newPath)
        {
            newPath = string.Empty;

            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // 如果这是一个非 file 协议的 URI，则原样返回（本地文件不存在）
            if (HasUriScheme(url) && !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                newPath = url;
                return false;
            }

            // 如果是 file:// URI，去掉前缀并检查底层路径
            string pathToCheck = url;
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                pathToCheck = url.Substring("file://".Length);
            }

            // 如果是绝对路径则直接获取完整路径；否则与 AppDataPath 结合
            try
            {
                if (Path.IsPathRooted(pathToCheck))
                {
                    newPath = Path.GetFullPath(pathToCheck);
                }
                else
                {
                    newPath = Path.GetFullPath(AppDataPath() + pathToCheck);
                }
            }
            catch (Exception)
            {
                newPath = pathToCheck;
                return false;
            }

            return File.Exists(newPath);
        }

        /// <summary>
        /// 获取在只读区下的完整路径
        /// </summary>
        public static string GetReadOnlyPath(string path, bool isUwrPath = false)
        {
            string tempPath;

            // 如果路径为空使用 streamingAssetsPath
            if (string.IsNullOrEmpty(path))
            {
                tempPath = Application.streamingAssetsPath;
            }
            else
            {
                // 如果传入路径已经包含 URI scheme（例如 "http://", "file://"），则不要与 streamingAssetsPath 合并
                if (HasUriScheme(path))
                {
                    tempPath = path;
                }
                else if (path.StartsWith("/") || path.StartsWith("\\"))
                {
                    // Path.Combine 会把以目录分隔符开头的路径视为根路径并忽略第一部分。
                    // 手动拼接以保留 streamingAssetsPath
                    tempPath = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + path.TrimStart('/', '\\');
                }
                else
                {
                    tempPath = Path.Combine(Application.streamingAssetsPath, path);
                }
            }

            string result = GetRegularPath(tempPath);

            if (isUwrPath)
            {
                // 只有当计算结果尚未包含 URI scheme 时才添加 "file://" 前缀
                if (!HasUriScheme(result) && !result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    result = "file://" + result;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取在读写区下的完整路径
        /// </summary>
        public static string GetReadWritePath(string path, bool isUwrPath = false)
        {
            string tempPath;

            if (string.IsNullOrEmpty(path))
            {
                tempPath = Application.persistentDataPath;
            }
            else
            {
                if (HasUriScheme(path))
                {
                    tempPath = path;
                }
                else if (path.StartsWith("/") || path.StartsWith("\\"))
                {
                    tempPath = Application.persistentDataPath.TrimEnd('/', '\\') + "/" + path.TrimStart('/', '\\');
                }
                else
                {
                    tempPath = Path.Combine(Application.persistentDataPath, path);
                }
            }

            string result = GetRegularPath(tempPath);

            if (isUwrPath)
            {
                // 只有当计算结果尚未包含 URI scheme 时才添加 "file://" 前缀
                if (!HasUriScheme(result) && !result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    result = "file://" + result;
                }
            }

            return result;
        }

        /// <summary>
        /// 从路径的末尾向前截取指定级别的目录
        /// </summary>
        /// <param name="fullPath">完整路径</param>
        /// <param name="levels">向前级别</param>
        /// <returns></returns>
        public static string TruncatePath(string fullPath, int levels)
        {
            if (string.IsNullOrEmpty(fullPath) || levels <= 0) return fullPath;

            string current = fullPath;
            for (int i = 0; i < levels; i++)
            {
                try
                {
                    current = Path.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(current))
                    {
                        return string.Empty;
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }
            return current;
        }

        /// <summary>
        /// 获取本地路径
        /// </summary>
        /// <param name="downloadType"> 路径类型 </param>
        /// <returns></returns>
        public static string GetLocalPath(DownloadType downloadType = DownloadType.PersistentFile)
        {
            string savePath = downloadType switch
            {
                DownloadType.PersistentAssets => Application.persistentDataPath + "/Assets",
                DownloadType.PersistentFile => Application.persistentDataPath + "/File",
                DownloadType.PersistentImage => Application.persistentDataPath + "/Picture",
                DownloadType.PersistentJson => Application.persistentDataPath + "/Json",

                DownloadType.StreamingAssets => Application.streamingAssetsPath + "/Download",
                DownloadType.StreamingAssetsFile => Application.streamingAssetsPath + "/File",
                DownloadType.StreamingAssetsImage => Application.streamingAssetsPath + "/Picture",
                DownloadType.StreamingAssetsJson => Application.streamingAssetsPath + "/Json",

                DownloadType.CacheAssets => Application.temporaryCachePath + "/Assets",
                DownloadType.CacheFile => Application.temporaryCachePath + "/File",
                DownloadType.CacheImage => Application.temporaryCachePath + "/Picture",
                DownloadType.CacheJson => Application.temporaryCachePath + "/Json",
                _ => Application.persistentDataPath + "/File"
            };

            // 对于 streamingAssets 的路径不应尝试创建目录（许多平台为只读）
            bool isStreaming = savePath.StartsWith(Application.streamingAssetsPath, StringComparison.OrdinalIgnoreCase);

            if (!isStreaming)
            {
                try
                {
                    if (!Directory.Exists(savePath))
                    {
                        Directory.CreateDirectory(savePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"GetLocalPath: 创建目录失败 {savePath} -> {ex}");
                }
            }

            return savePath;
        }

        /// <summary>
        /// 获取本地文件路径
        /// </summary>
        /// <param name="url"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static string GetLocalFilePath(string url)
        {
            string urlHash = StringUtil.CreateMD5(url ?? string.Empty);
            return $"{GetLocalPath(DownloadType.CacheImage)}/{urlHash}{GetExtensionFromUrl(url)}";
        }

        /// <summary>
        /// 获取文件名通过Url和后缀
        /// </summary>
        /// <param name="url"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static string GetFileNameByUrl(string url)
        {
            string urlHash = StringUtil.CreateMD5(url ?? string.Empty);
            return $"{urlHash}{GetExtensionFromUrl(url)}";
        }

        /// <summary>
        /// 从Url获取文件后缀
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return ".asset";
            }

            // 去除参数和 fragment
            int queryIndex = url.IndexOf('?');
            int fragmentIndex = url.IndexOf('#');
            int endIndex = url.Length;
            if (queryIndex >= 0) endIndex = queryIndex;
            if (fragmentIndex >= 0 && fragmentIndex < endIndex) endIndex = fragmentIndex;
            string cleanUrl = url.Substring(0, endIndex);

            // 用 Path.GetExtension 获取后缀
            string ext;
            try
            {
                ext = Path.GetExtension(cleanUrl);
            }
            catch
            {
                return ".asset";
            }

            if (!string.IsNullOrEmpty(ext) && ext.Length <= 8 && ext.All(c => char.IsLetterOrDigit(c) || c == '.'))
            {
                return ext.ToLower();
            }

            return ".asset";
        }
    }
}