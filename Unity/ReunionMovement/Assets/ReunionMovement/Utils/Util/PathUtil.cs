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
        public static readonly string[] pathHeadDefine = { "jar://", "jar:file://", "file://", "file:///", "http://", "https://", "ftp://", "content://", "data:" };

        /// <summary>
        /// 获取路径头
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static string GetPathHead(string path)
        {
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
        public static string GetRegularPath(string path) => path.Replace('\\', '/');

        /// <summary>
        /// 验证路径（是否为真路径）
        /// </summary>
        public static bool IsSureDir(string path) => path.Contains("/") || path.Contains("\\");

        /// <summary>
        /// 验证路径（是否为全路径）
        /// </summary>
        public static bool IsFullPath(string path) => path.Contains(":/") || path.Contains(":\\");

        /// <summary>
        /// 持续化路径
        /// </summary>
        public static string AppDataPath() => Application.persistentDataPath + "/";

        /// <summary>
        /// 获取完整路径
        /// </summary>
        /// <param name="url"></param>
        /// <param name="newPath"></param>
        /// <returns></returns>
        public static bool GetFullPath(string url, out string newPath)
        {
            newPath = Path.GetFullPath(AppDataPath() + url);
            return File.Exists(newPath);
        }

        /// <summary>
        /// 获取在只读区下的完整路径
        /// </summary>
        public static string GetReadOnlyPath(string path, bool isUwrPath = false)
        {
            string tempPath;

            // If path is empty use streamingAssetsPath
            if (string.IsNullOrEmpty(path))
            {
                tempPath = Application.streamingAssetsPath;
            }
            else
            {
                // If the provided path already contains a URI scheme (e.g. "http://", "file://"), don't try to combine with streamingAssetsPath
                if (path.IndexOf("://", StringComparison.Ordinal) >= 0)
                {
                    tempPath = path;
                }
                else if (path.StartsWith("/") || path.StartsWith("\\"))
                {
                    // Path.Combine will treat a leading directory separator as rooted and ignore the first part.
                    // Manually concatenate to preserve streamingAssetsPath.
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
                // Only prefix with file:// when the computed result does not already contain a URI scheme
                // e.g. on Android streamingAssetsPath may already be "jar:file://..." so don't add another prefix
                if (!result.Contains("://") && !result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
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
                if (path.IndexOf("://", StringComparison.Ordinal) >= 0)
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
                // Only prefix with file:// when the computed result does not already contain a URI scheme
                if (!result.Contains("://") && !result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
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
            for (int i = 0; i < levels && !string.IsNullOrEmpty(fullPath); i++)
            {
                fullPath = Path.GetDirectoryName(fullPath);
            }
            return fullPath;
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

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
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
            string urlHash = StringUtil.CreateMD5(url);
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
            string urlHash = StringUtil.CreateMD5(url);
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

            // 用Path.GetExtension获取后缀
            string ext = Path.GetExtension(cleanUrl);
            if (!string.IsNullOrEmpty(ext) && ext.Length <= 8 && ext.All(c => char.IsLetterOrDigit(c) || c == '.'))
            {
                return ext.ToLower();
            }

            return ".asset";
        }
    }
}
