using ReunionMovement.Common.Util.HttpService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// 下载管理器
    /// </summary>
    public class DownloadMgr : SingletonMgr<DownloadMgr>
    {
        private readonly Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>();
        private readonly LinkedList<string> imageCacheOrder = new LinkedList<string>(); // LRU 访问顺序
        private readonly object imageCacheLock = new object();
        private const int MaxImageCacheSize = 50;
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
            lock (imageCacheLock)
            {
                foreach (var tex in imageCache.Values)
                {
                    if (tex != null)
                    {
                        UnityEngine.Object.Destroy(tex);
                    }
                }
                imageCache.Clear();
                imageCacheOrder.Clear();
            }
            Log.Debug("DownloadManagerModule 清除数据");
        }

        private void OnDestroy()
        {
            // 仅在自身是当前活跃单例时才清理数据，避免重复实例被销毁时误清
            if (Instance != this) return;

            ClearData();

            // clear mappings
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
            // 1. 内存查找（命中时提升 LRU 顺序）—— 加锁防止竞态条件
            Texture2D cachedTexture = null;
            lock (imageCacheLock)
            {
                if (imageCache.TryGetValue(url, out cachedTexture) && cachedTexture != null)
                {
                    TouchCacheEntry(url);
                }
                else
                {
                    cachedTexture = null;
                }
            }
            if (cachedTexture != null)
            {
                onComplete?.Invoke(cachedTexture);
                return;
            }

            // 2. 本地查找
            string localPath = PathUtil.GetLocalFilePath(url);
            if (TryLoadFromLocal(localPath, out Texture2D localTexture) && localTexture != null)
            {
                // 加入内存缓存
                AddToImageCache(url, localTexture);
                onComplete?.Invoke(localTexture);
                return;
            }

            // 3. 网络下载
            HttpMgr.GetTexture(url)
                .OnDownloadProgress(onProgress)
                .OnSuccess(response =>
                {
                    if (response.texture != null)
                    {
                        lock (imageCacheLock)
                        {
                            if (imageCache.TryGetValue(url, out Texture2D oldTex) && oldTex != null)
                            {
                                UnityEngine.Object.Destroy(response.texture);
                                onComplete?.Invoke(oldTex);
                                return;
                            }
                        }
                        AddToImageCache(url, response.texture);

                        // 保存到本地（确保目录存在于SaveToLocal中）
                        SaveToLocal(response.texture, localPath);
                        onComplete?.Invoke(response.texture);
                    }
                })
                .OnError(error => 
                {
                    Log.Error($"下载图片失败: {error}");
                    onComplete?.Invoke(null);
                })
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
        public async Task DownloadFiles(List<string> url,
            string savePath,
            Action<float> progress = null,
            Action action = null,
            Action<string> error = null,
            bool skipIfExists = true, // 存在则跳过
            bool deleteIfExists = false, // 存在则删除
            bool useMd5Name = true // 是否使用 MD5 命名（默认 true），对 bundle 下载可设为 false
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

            // 将 useMd5Name 传递给 FileDownloader，以便控制命名策略
            FileDownloader ufd = new FileDownloader(savePath, useMd5Name, true, 3, true, false, true, urlsToDownload);

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

            try
            {
                await ufd.Download();
            }
            catch (Exception ex)
            {
                Log.Error($"下载过程中发生异常: {ex.Message}");
                error?.Invoke(ex.Message);
            }
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
                // 设置一个名称以帮助调试和跟踪
                try { texture.name = Path.GetFileName(filePath); } catch { }
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
                // 确保目录存在
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

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
        /// 加入图片缓存（含 LRU 淘汰）
        /// </summary>
        private void AddToImageCache(string url, Texture2D tex)
        {
            if (tex == null) return;

            lock (imageCacheLock)
            {
                // 淘汰最旧的条目直到低于上限
                while (imageCache.Count >= MaxImageCacheSize && imageCacheOrder.First != null)
                {
                    var oldest = imageCacheOrder.First.Value;
                    imageCacheOrder.RemoveFirst();
                    if (imageCache.TryGetValue(oldest, out var oldTex) && oldTex != null && oldTex != tex)
                    {
                        UnityEngine.Object.Destroy(oldTex);
                    }
                    imageCache.Remove(oldest);
                }

                imageCache[url] = tex;
                // 移到链表尾部（最新）
                imageCacheOrder.Remove(url);
                imageCacheOrder.AddLast(url);
            }
        }

        /// <summary>
        /// 更新缓存条目访问顺序（需在锁内调用）
        /// </summary>
        private void TouchCacheEntry(string url)
        {
            imageCacheOrder.Remove(url);
            imageCacheOrder.AddLast(url);
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