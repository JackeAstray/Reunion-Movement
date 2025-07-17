﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 路径工具类
    /// </summary>
    public static class PathUtil
    {
        public static readonly string[] PathHeadDefine = { "jar://", "jar:file://", "file://", "file:///", "http://", "https://", "ftp://", "content://", "data:" };

        /// <summary>
        /// 获取路径头
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static string GetPathHead(string path)
        {
            foreach (var head in PathHeadDefine)
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
            string result = GetRegularPath(Path.Combine(Application.streamingAssetsPath, path));

            if (isUwrPath && !path.Contains("file://"))
            {
                //使用UnityWebRequest访问 统一加file://头
                result = "file://" + result;
            }

            return result;
        }

        /// <summary>
        /// 获取在读写区下的完整路径
        /// </summary>
        public static string GetReadWritePath(string path, bool isUwrPath = false)
        {
            string result = GetRegularPath(Path.Combine(Application.persistentDataPath, path));

            if (isUwrPath && !path.Contains("file://"))
            {
                //使用UnityWebRequest访问 统一加file://头
                result = "file://" + result;
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
