using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// UnityWebRequest 下载器执行器
    /// </summary>
    public class UWRExecutor : IDownloadExecutor
    {
        protected long expectedSize = 0;
        protected int chunkSize = 0;

        internal int startTime = 0;
        internal int endTime = 0;

        internal long bytesDownloaded;
        internal float progress = 0f;
        internal int timeout = 6;
        internal string uri = null;

        internal string downloadPath;
        internal bool downloadToRoot;

        internal bool isMd5Name;

        internal bool multipartDownload = false;
        internal bool abandonOnFailure = true;
        internal bool paused = false;

        public event Action OnDownloadSuccess;
        public event Action OnCancel;
        public event Action OnDownloadChunkedSucces;
        public event Action<int, string> OnDownloadError;

        public bool Completed => Progress == 1.0f;
        public override float Progress => progress;
        public override long BytesDownloaded => bytesDownloaded;

        public override string Uri
        {
            get => uri;
            set => uri = value;
        }

        public override string DownloadPath
        {
            get => downloadPath;
            set => downloadPath = value;
        }

        public override bool IsMD5Name
        {
            get => isMd5Name;
            set => isMd5Name = value;
        }

        public override bool DownloadToRoot
        {
            get => downloadToRoot;
            set => downloadToRoot = value;
        }

        public override bool MultipartDownload
        {
            get => multipartDownload;
            set => multipartDownload = value;
        }

        public override bool AbandonOnFailure
        {
            get => abandonOnFailure;
            set => abandonOnFailure = value;
        }

        public override int Timeout
        {
            get => timeout;
            set => timeout = value;
        }

        public override bool Paused => paused;

        internal bool didError = false;
        public override bool DidError
        {
            get => didError;
            set => didError = value;
        }

        /// <summary>当前在途的 UnityWebRequest（用于 Cancel 时 Abort，完成后 Dispose 防泄漏）</summary>
        internal UnityWebRequest currentRequest;
        /// <summary>已取消标记（防止 Cancel 被二次调用时 OnCancel 事件重复触发）</summary>
        private bool cancelCalled;

        public override int StartTime => startTime;
        public override int EndTime => endTime;

        /// <summary>
        /// 取消：中止在途请求并删除未完成文件
        /// </summary>
        /// <returns></returns>
        public override bool Cancel()
        {
            if (cancelCalled) return false;
            cancelCalled = true;

            // 中止在途的 UWR，避免取消后网络 IO 继续运行、文件被重新写回
            if (currentRequest != null && !currentRequest.isDone)
            {
                currentRequest.Abort();
            }
            currentRequest?.Dispose();
            currentRequest = null;

            OnCancel?.Invoke();
            if (abandonOnFailure && !string.IsNullOrEmpty(DownloadResultPath) && File.Exists(DownloadResultPath))
            {
                try
                {
                    File.Delete(DownloadResultPath);
                }
                catch (Exception ex)
                {
                    Log.Error("删除文件失败: {0}", ex);
                }
            }
            // 返回 true 表示本次确实执行了取消（幂等重入返回 false 见开头）
            return true;
        }

        /// <summary>
        /// 向URI提交head请求，以确定是否可以进行分块下载。
        /// </summary>
        /// <returns></returns>
        public UnityWebRequestAsyncOperation HeadRequest()
        {
            UnityWebRequest uwr = null;
            UnityWebRequestAsyncOperation hreq = HTTPHelper.Head(ref uwr, Uri, RequestHeaders, Timeout);
            DidHeadReq = true;
            // 挂载到 currentRequest：Cancel() 时可中止 HEAD 请求，
            // 避免取消后 HEAD 完成回调仍发起首块下载产生多余 IO 与文件写入。
            currentRequest = uwr;
            hreq.completed += (resp) =>
            {
                try
                {
                    // 已取消（currentRequest 被 Cancel 置空/中止）：不再解析，防止访问已 Dispose 的 UWR
                    if (uwr == null || currentRequest != uwr)
                    {
                        return;
                    }

                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        Log.Debug("URI {0} 不支持HEAD请求，因此不支持分块下载。 错误: {1}", Uri, uwr.error);
                        MultipartDownload = false;
                        return;
                    }

                    var headers = uwr.GetResponseHeaders();

                    if (headers == null ||
                        !headers.ContainsKey("Content-Length") ||
                        !headers.TryGetValue("Accept-Ranges", out var acceptRanges) ||
                        !string.Equals(acceptRanges, "bytes", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Debug("URI {0} 不支持分块下载。", Uri);
                        MultipartDownload = false;
                        return;
                    }

                    // 用 long 解析，避免 >2GB 文件的 Content-Length 超出 int 范围解析失败
                    if (!long.TryParse(headers["Content-Length"], out expectedSize))
                    {
                        Log.Debug("URI {0} 不支持分块下载。Content-Length 解析失败。", Uri);
                        MultipartDownload = false;
                        return;
                    }

                    chunkSize = InitialChunkSize;
                    MultipartDownload = expectedSize > chunkSize;
                }
                finally
                {
                    // 释放 HEAD 请求的 UWR 原生资源，避免每个文件泄漏（Dispose 幂等，Cancel 已释放时安全）
                    uwr?.Dispose();
                    if (currentRequest == uwr) currentRequest = null;
                }
            };
            return hreq;
        }

        /// <summary>
        /// 根据当前的 URI 和配置，发起文件下载请求，并处理单个或分块下载的流程
        /// </summary>
        /// <returns></returns>
        public override UnityWebRequestAsyncOperation Download()
        {
            if (CompletedMultipartDownload || string.IsNullOrEmpty(Uri) || string.IsNullOrEmpty(DownloadPath))
            {
                return null;
            }

            startTime = Environment.TickCount;

            UnityWebRequestAsyncOperation resp = null;
            UnityWebRequest uwr = null;

            if (!MultipartDownload)
            {
                resp = HTTPHelper.Download(ref uwr, Uri, DownloadPath, isMd5Name, DownloadToRoot, AbandonOnFailure, false, RequestHeaders, Timeout);
                currentRequest = uwr;
                resp.completed += (obj) =>
                {
                    // 必须校验 UWR 结果：失败时不能仅凭文件存在就误报成功
                    // （下方通用回调会处理失败分支：OnDownloadError + Cancel）
                    if (uwr == null || uwr.result != UnityWebRequest.Result.Success)
                    {
                        return;
                    }
                    if (!File.Exists(DownloadResultPath))
                    {
                        return;
                    }
                    progress = 1.0f;
                    OnDownloadSuccess?.Invoke();
                    endTime = Environment.TickCount;
                    bytesDownloaded = new FileInfo(DownloadResultPath).Length;
                };
            }
            else
            {
                try
                {
                    long fileSize = 0;
                    if (File.Exists(DownloadResultPath))
                    {
                        try
                        {
                            fileSize = new FileInfo(DownloadResultPath).Length;
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex.ToString());
                            return null;
                        }
                    }
                    long remaining = expectedSize - fileSize;
                    if (remaining <= 0)
                    {
                        Log.Warning("文件已存在且大小符合要求，跳过下载: {0}", DownloadResultPath);
                        return null;
                    }

                    int reqChunkSize = (int)Math.Min((long)chunkSize, remaining);

                    if (RequestHeaders == null)
                    {
                        RequestHeaders = new Dictionary<string, string>();
                    }
                    RequestHeaders.Remove("Range");
                    RequestHeaders.Add("Range", $"bytes={fileSize}-{fileSize + reqChunkSize - 1}");

                    resp = HTTPHelper.Download(ref uwr, Uri, DownloadPath, isMd5Name, DownloadToRoot, AbandonOnFailure, true, RequestHeaders, Timeout);
                    currentRequest = uwr;
                    // resp 是每块新建的 AsyncOperation，不存在重复订阅，直接 +=
                    resp.completed += OnCompleteMulti;
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                }
            }

            if (resp != null)
            {
                resp.completed += (obj) =>
                {
                    try
                    {
                        // Cancel() 已 Dispose 本请求：跳过回调，避免访问已释放的 UWR
                        if (uwr == null || currentRequest != uwr) return;
                        if (uwr.result != UnityWebRequest.Result.Success)
                        {
                            DidError = true;
                            OnDownloadError?.Invoke(0, uwr.error);
                            Cancel();
                        }
                    }
                    finally
                    {
                        // 请求结束：释放 UWR 原生资源，避免每个文件/分块泄漏
                        uwr?.Dispose();
                        if (currentRequest == uwr) currentRequest = null;
                    }
                };
            }
            return resp;
        }


        /// <summary>
        /// 当分块请求完成时调用。
        /// </summary>
        /// <param name="obj"></param>
        internal void OnCompleteMulti(AsyncOperation obj)
        {
            // 分块请求失败或已被 Cancel：不触发任何成功事件
            // （失败由通用回调走 OnDownloadError + Cancel 分支；
            //   若不校验，残留部分文件时会把失败误报为成功，甚至误删已完整文件）
            if (currentRequest == null || currentRequest.result != UnityWebRequest.Result.Success)
            {
                return;
            }

            // 分块响应必须是 206 Partial Content：服务器/代理忽略 Range 头返回 200 时，
            // DownloadHandlerFile(append:true) 会把全量内容追加到已有文件 → 文件损坏，
            // 且 fileSize > expectedSize 会导致后续块永不满足 == 判断而无限循环直到超时。
            if (currentRequest.responseCode != 206)
            {
                DidError = true;
                OnDownloadError?.Invoke(0, string.Format("服务器忽略 Range 头（HTTP {0}），中止分块下载以免文件被重复追加损坏", currentRequest.responseCode));
                Cancel();
                return;
            }

            if (!File.Exists(DownloadResultPath))
            {
                return;
            }
            long fileSize = new FileInfo(DownloadResultPath).Length;

            // 文件已超过预期大小：说明发生重复追加（如中途服务端改返回全量），中止并清理
            if (fileSize > expectedSize)
            {
                DidError = true;
                OnDownloadError?.Invoke(0, string.Format("分块下载文件大小超过预期（{0} > {1}），判定文件损坏，中止下载", fileSize, expectedSize));
                Cancel();
                return;
            }

            OnDownloadChunkedSucces?.Invoke();
            progress = expectedSize > 0 ? (float)fileSize / expectedSize : 0f;
            bytesDownloaded = fileSize;

            if (fileSize == expectedSize)
            {
                OnDownloadSuccess?.Invoke();
                endTime = Environment.TickCount;
                CompletedMultipartDownload = true;
            }
        }
    }
}