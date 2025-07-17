using ICSharpCode.SharpZipLib.Core;
using ReunionMovement.Common.Util.HttpService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Rendering;
using UnityEngine;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// 下载管理器
    /// </summary>
    public class DownloadMgr : SingletonMgr<DownloadMgr>
    {
        private readonly Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, string> mimeTypeToExtension = new Dictionary<string, string>
        {
            {"text/html",".html"},
            {"text/plain",".txt"},
            {"text/xml",".xml"},

            {"image/gif", ".gif"},
            {"image/jpeg", ".jpg"},
            {"image/png", ".png"},
            {"image/webp", ".webp"},

            {"audio/mp3",".mp3"},
            {"audio/wav",".wav"},
            {"audio/ogg",".ogg"},
            {"audio/mid",".mid"},

            {"video/mpeg4",".mp4"},
            {"video/avi",".avi"},

            {"application/pdf",".pdf"},
            {"application/msword",".doc"},
            {"application/json",".json"},
            {"application/javascript",".js"},
            {"application/xml",".xml"},
            {"application/zip",".zip"},
            {"application/7z",".7z"},
        };

        public void ClearData()
        {
            imageCache.Clear();
            Log.Debug("DownloadManagerModule 清除数据");
        }

        public void OnDestroy()
        {
            imageCache.Clear();
            mimeTypeToExtension.Clear();
        }

        /// <summary>
        /// 下载图片（HTTP方式）
        /// </summary>
        /// <param name="url"></param>
        /// <param name="onProgress"></param>
        /// <param name="onComplete"></param>
        /// <param name="suffix"></param>
        public void DownloadImage_Http(string url, Action<float> onProgress, Action<Texture2D> onComplete)
        {
            if (imageCache.TryGetValue(url, out Texture2D cachedTexture))
            {
                onComplete?.Invoke(cachedTexture);
                return;
            }

            string suffix = GetExtensionFromUrl(url);

            string localPath = GetLocalFilePath(url, suffix);
            if (TryLoadFromLocal(localPath, out Texture2D texture))
            {
                Log.Debug($"从本地加载图片成功: {localPath}");
                imageCache[url] = texture;
                onComplete?.Invoke(texture);
            }
            else
            {
                HttpMgr.GetTexture(url)
                    .OnDownloadProgress(onProgress)
                    .OnSuccess(response =>
                    {
                        if (response.Texture != null)
                        {
                            imageCache[url] = response.Texture;
                            SaveToLocal(response.Texture, localPath);
                            onComplete?.Invoke(response.Texture);
                        }
                    })
                    .OnError(error => Log.Error($"下载图片失败: {error}"))
                    .Send();
            }
        }

        /// <summary>
        /// 下载文件（Http方式）
        /// </summary>
        /// <param name="url"></param>
        /// <param name="onProgress"></param>
        /// <param name="onComplete"></param>
        public void DownloadFile_Http(string url, Action<float> onProgress, Action<HttpResponse> onComplete)
        {
            string fileName = FileOperationUtil.GetFileName(url);
            string localPath = Path.Combine(PathUtil.GetLocalPath(DownloadType.PersistentFile), fileName);

            HttpMgr.Get(url)
                .OnDownloadProgress(onProgress)
                .OnSuccess(onComplete)
                .OnError(error => Log.Error($"下载文件失败: {error}"))
                .Send();
        }

        /// <summary>
        /// 文件下载（多文件）
        /// </summary>
        /// <param name="url"></param>
        /// <param name="savePath"></param>
        /// <param name="uiPlane"></param>
        /// <param name="set"></param>
        /// <param name="action"></param>
        public async void DownloadFiles(List<string> url, string savePath, Action<float> progress = null, Action action = null, Action<string> error = null)
        {
            FileDownloader ufd = new FileDownloader(savePath, true, 3, true, false, true, url);

            ufd.OnDownloadSuccess += (string uri) =>
            {
                Log.Debug("OnDownloadSuccess = " + ufd.Progress);
                progress?.Invoke(ufd.Progress);
            };

            ufd.OnDownloadChunkedSucces += (string uri) =>
            {
                Log.Debug("进度 for " + uri + " is " + ufd.GetProgress(uri));
            };

            ufd.OnDownloadsSuccess += () =>
            {
                Log.Debug("已下载所有文件。");
                action?.Invoke();
            };

            ufd.OnDownloadError += (string uri, int errorCode, string errorMsg) =>
            {
                Log.Error($"错误代码={errorCode}, EM={errorMsg}, URU={uri}");
                error?.Invoke(errorMsg);
            };

            await ufd.Download();
        }

        /// <summary>
        /// 获取本地文件路径
        /// </summary>
        /// <param name="url"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public string GetLocalFilePath(string url, string suffix)
        {
            string urlHash = StringUtil.CreateMD5(url);
            return $"{PathUtil.GetLocalPath(DownloadType.CacheImage)}/{urlHash}{suffix}";
        }

        /// <summary>
        /// 尝试从本地加载图片
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        public bool TryLoadFromLocal(string filePath, out Texture2D texture)
        {
            texture = null;
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                byte[] imageBytes = File.ReadAllBytes(filePath);
                texture = new Texture2D(2, 2);
                texture.LoadImage(imageBytes);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"从本地加载失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存图片到本地
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="filePath"></param>
        public void SaveToLocal(Texture2D texture, string filePath)
        {
            try
            {
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);
                Log.Debug($"保存到本地成功: {filePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通过MIME类型获取文件后缀
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        public string GetExtensionFromMimeType(string mimeType)
        {
            if (mimeTypeToExtension.TryGetValue(mimeType, out string extension))
            {
                return extension;
            }
            return ".asset";
        }

        /// <summary>
        /// 从Url获取文件后缀
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return ".asset";
            }

            try
            {
                // 去除查询参数和片段
                var uri = new Uri(url);
                string path = uri.AbsolutePath;
                int lastDot = path.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < path.Length - 1)
                {
                    string ext = path.Substring(lastDot);
                    // 简单校验：只允许字母数字和部分常见符号
                    if (ext.Length <= 8 && ext.All(c => char.IsLetterOrDigit(c) || c == '.'))
                    {
                        return ext.ToLower();
                    }
                }
            }
            catch
            {
                // url 不是标准格式时，尝试直接查找
                int lastDot = url.LastIndexOf('.');
                int lastSlash = url.LastIndexOf('/');
                if (lastDot > lastSlash && lastDot >= 0 && lastDot < url.Length - 1)
                {
                    string ext = url.Substring(lastDot);
                    if (ext.Length <= 8 && ext.All(c => char.IsLetterOrDigit(c) || c == '.'))
                    {
                        return ext.ToLower();
                    }
                }
            }
            return ".asset";
        }
    }
}