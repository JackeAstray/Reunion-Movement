using ReunionMovement.Common.Util.HttpService;
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// 清除下载管理器数据
        /// </summary>
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

            string localPath = PathUtil.GetLocalFilePath(url);

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
        public async void DownloadFiles(List<string> url,
            string savePath,
            Action<float> progress = null,
            Action action = null,
            Action<string> error = null,
            bool skipIfExists = true, // 存在则跳过
            bool deleteIfExists = false // 存在则删除
        )
        {
            // 检查本地文件是否存在
            List<string> urlsToDownload = new List<string>();
            foreach (var fileUrl in url)
            {
                string fileName = FileOperationUtil.GetFileName(fileUrl);
                string localFilePath = Path.Combine(savePath, fileName);

                if (File.Exists(localFilePath))
                {
                    if (skipIfExists)
                    {
                        Log.Debug($"文件已存在，跳过下载: {localFilePath}");
                        continue;
                    }
                    else if (deleteIfExists)
                    {
                        try
                        {
                            File.Delete(localFilePath);
                            Log.Debug($"已删除旧文件: {localFilePath}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"删除文件失败: {localFilePath}, {ex.Message}");
                            error?.Invoke($"删除文件失败: {localFilePath}, {ex.Message}");
                            continue;
                        }
                    }
                }
                urlsToDownload.Add(fileUrl);
            }

            if (urlsToDownload.Count == 0)
            {
                Log.Debug("所有文件均已存在，无需下载。");
                action?.Invoke();
                return;
            }

            FileDownloader ufd = new FileDownloader(savePath, true, true, 3, true, false, true, url);

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
    }
}