using Cysharp.Threading.Tasks;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// Unity 文件下载器，负责调度并下载一组 URL 到本地。
    /// </summary>
    public class FileDownloader : IDownloader
    {
        public FileDownloader() { }

        public FileDownloader(
            string downloadPath,
            bool isMd5Name,
            bool downloadToRoot,
            int maxConcurrency,
            bool abandonOnFailure,
            bool continueAfterFailure,
            bool tryMultipartDownload,
            List<string> uris
        )
        {
            DownloadPath = downloadPath;
            IsMD5Name = isMd5Name;
            DownloadToRoot = downloadToRoot;
            MaxConcurrency = maxConcurrency;
            AbandonOnFailure = abandonOnFailure;
            ContinueAfterFailure = continueAfterFailure;
            this.tryMultipartDownload = tryMultipartDownload;
            Uris = uris?.ToArray() ?? Array.Empty<string>();
        }

        internal readonly static SemaphoreLocker Locker = new SemaphoreLocker();

        internal int initialCount;
        internal int timeout = 6;
        internal int maxConcurrency = 4;
        internal bool abandonOnFailure = true;
        internal bool continueAfterFailure = false;
        internal bool downloading = false;
        internal bool paused = false;
        internal bool didError = false;
        internal int numFilesRemaining = 0;
        internal long startTime = 0, endTime = 0;
        internal string downloadPath;
        internal bool downloadToRoot;
        internal bool isMd5Name;
        internal string[] pendingUris = null;
        // 待处理 URI 的 FIFO 取队偏移量，代替每次 Skip().ToArray()。
        private int pendingOffset = 0;

        #region Events/Actions
        public event Action OnDownloadsSuccess;
        public event Action OnDownloadInvoked;
        public event Action OnCancelInvoked;
        public event Action<string> OnCancelIndividual;
        public event Action<string> OnDownloadIndividualInvoked;
        public event Action OnCancel;
        #endregion

        public override string iDownloadExecutorClassName => "UWRExecutor";

        public override long StartTime => startTime;
        public override long EndTime => endTime;

        /// <summary>
        /// 每秒处理的文件数（下载速度指标）。
        /// </summary>
        /// <value></value>
        public override float NumFilesPerSecond
        {
            get
            {
                if (ElapsedTime == 0 || DownloadedUris == null || DownloadedUris.Length == 0)
                {
                    return 0f;
                }
                return (DownloadedUris.Length * 1000f) / ElapsedTime;
            }
        }

        public float MegabytesDownloadedPerSecond => BytesDownloadedPerSecond / 1000f;

        public float BytesDownloadedPerSecond
        {
            get
            {
                float totalBytes = 0;
                float totalElapsed = 0;
                foreach (var idf in executors.Concat(executorsOld))
                {
                    totalBytes += idf.BytesDownloaded;
                    totalElapsed += idf.ElapsedTime;
                }
                return totalElapsed > 0 ? totalBytes / totalElapsed : 0;
            }
        }

        public override int Timeout
        {
            get => timeout;
            set => timeout = value;
        }

        public override int MaxConcurrency
        {
            get => maxConcurrency;
            set => maxConcurrency = value;
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

        public override bool AbandonOnFailure
        {
            get => abandonOnFailure;
            set => abandonOnFailure = value;
        }

        public override bool ContinueAfterFailure
        {
            get => continueAfterFailure;
            set => continueAfterFailure = value;
        }

        public override bool Downloading => downloading;

        public override bool DidError
        {
            get => didError;
            protected set => didError = value;
        }

        public override bool Paused => paused;

        public override int NumFilesRemaining => numFilesRemaining;

        public override string[] PendingURIS => pendingUris;

        public override List<string> IncompletedURIS => incompletedUris;

        public int NumThreads => n;
        internal int n = 0;

        /// <summary>
        /// 下载所有 URI 并等待完成，超过最大等待时间（默认 = Timeout × 文件数量，最低 30 秒）则强制取消。
        /// </summary>
        /// <returns></returns>
        public override async UniTask<bool> Download()
        {
            if (Downloading || Uris == null || Uris.Length == 0)
            {
                Log.Error("{0}.Download() 调用失败：Uris 为 null 或为空，已取消下载", GetType().FullName);
                return false;
            }
            OnDownloadInvoked?.Invoke();
            pendingUris = Uris.ToArray();
            pendingOffset = 0;
            numFilesRemaining = Uris.Length;
            startTime = Environment.TickCount;
            downloading = true;

            initialCount = Uris.Length;
            int threadCount = Math.Min(MaxConcurrency, numFilesRemaining);
            if (threadCount <= 0)
            {
                Log.Error("{0}.错误：MaxConcurrency 需要大于 0", GetType().FullName);
                return false;
            }
            var tasks = new List<UniTask<bool>>(threadCount);
            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Dispatch());
            }

            await UniTask.WhenAll(tasks);

            // 超时等待循环：最多等待 (Timeout * 文件数 * 2) 秒，最低 30 秒
            int maxWaitSeconds = Math.Max(30, Timeout * Uris.Length * 2);
            float waited = 0f;
            const float pollInterval = 0.1f;

            while (Downloading && !(DidError && !ContinueAfterFailure))
            {
                if (waited >= maxWaitSeconds)
                {
                    Log.Error("下载超时：已等待 {0} 秒，仍有 {1} 个文件未完成，强制取消下载", maxWaitSeconds, NumFilesRemaining);
                    await Cancel();
                    break;
                }
                await UniTask.Delay((int)(pollInterval * 1000));
                waited += pollInterval;
            }

            return true;
        }

        /// <summary>
        /// 下载单个 URI。
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public override UniTask<bool> Download(string uri)
        {
            if (!Downloading)
            {
                downloading = true;
                startTime = Environment.TickCount;
                initialCount = 1;
            }

            if (!executors.Any(idf => idf.Uri == uri) && !executorsOld.Any(idf => idf.Uri == uri))
            {
                var idf = DownloadExecutorFactory.CreateFromClassName(iDownloadExecutorClassName);
                idf.Uri = uri;
                idf.DownloadPath = DownloadPath;
                idf.IsMD5Name = IsMD5Name;
                idf.DownloadToRoot = DownloadToRoot;
                idf.AbandonOnFailure = AbandonOnFailure;
                idf.Timeout = Timeout;
                // pendingUris 可能为 null（未先调用无参 Download()），需判空后再 Append
                pendingUris = (pendingUris ?? Array.Empty<string>()).Append(uri).ToArray();
                executors = executors.Append(idf).ToArray();
                numFilesRemaining++;
            }
            else if (executorsOld.Any(idf => idf.Uri == uri))
            {
                var idf = executorsOld.First(idf => idf.Uri == uri);
                executorsOld = executorsOld.Where(x => x != idf).ToArray();
                pendingUris = (pendingUris ?? Array.Empty<string>()).Append(uri).ToArray();
                executors = executors.Append(idf).ToArray();
            }

            OnDownloadIndividualInvoked?.Invoke(uri);
            return UniTask.FromResult(true);
        }

        /// <summary>
        /// 返回 false 的异步结果。
        /// </summary>
        /// <returns></returns>
        internal UniTask<bool> ReturnFalseAsync() => UniTask.FromResult(false);

        /// <summary>
        /// 分发下一个任务，使用偏移量代替 Skip+ToArray（避免每次重新分配数组）。
        /// </summary>
        internal UniTask<bool> Dispatch()
        {
            if (pendingUris == null || pendingOffset >= pendingUris.Length)
            {
                return ReturnFalseAsync();
            }

            // executors 与异步回调访问 pendingUris 不同步，需越界保护
            if (executors == null || executors.Length == 0)
            {
                Log.Error("Dispatch: executors 列表为空，无法分发任务");
                return ReturnFalseAsync();
            }

            string uri = pendingUris[pendingOffset];
            IDownloadExecutor idf = executors[0];
            pendingOffset++;
            executors = executors.Skip(1).ToArray();
            executorsOld = executorsOld.Append(idf).ToArray();

            if (idf.CompletedMultipartDownload)
            {
                return ReturnFalseAsync();
            }

            if (!idf.DidHeadReq && idf.TryMultipartDownload)
            {
                var treq = ((UWRExecutor)idf).HeadRequest();

                if (treq != null)
                {
                    n++;
                    treq.completed += (obj) =>
                    {
                        var rv = idf.Download();
                        if (rv != null)
                        {
                            rv.completed += resp =>
                            {
                                n--;
                                _ = DispatchCompletion(idf);
                            };
                        }
                        else
                        {
                            Log.Warning("Download for {0} returned null，未启动下载流程", idf.Uri);
                            // 继续分发下一个待下载的 URI，维持任务队列
                            if (pendingUris != null && pendingOffset < pendingUris.Length)
                            {
                                _ = Dispatch();
                            }
                        }
                    };
                }
                else
                {
                    // HeadRequest 返回 null，说明该 URI 不可分块，直接进入下一步
                    Log.Warning("HeadRequest for {0} returned null，跳过该 URI", idf.Uri);
                    _ = DispatchCompletion(idf);
                }

                return ReturnFalseAsync();
            }

            var req = idf.Download();
            if (req == null)
            {
                _ = DispatchCompletion();
                return ReturnFalseAsync();
            }
            n++;
            req.completed += resp =>
            {
                n--;
                _ = DispatchCompletion(idf);
            };
            return ReturnFalseAsync();
        }

        /// <summary>
        /// 下载指定的 IDF，用于分块下载。
        /// </summary>
        /// <param name="idf"></param>
        /// <returns></returns>
        internal UniTask<bool> Dispatch(IDownloadExecutor idf)
        {
            var req = idf.Download();
            if (req == null)
            {
                _ = DispatchCompletion();
                return ReturnFalseAsync();
            }
            n++;
            req.completed += resp =>
            {
                n--;
                _ = DispatchCompletion(idf);
            };
            return ReturnFalseAsync();
        }

        internal async UniTask DispatchCompletion()
        {
            await Locker.LockAsync(async () =>
            {
                if (!Downloading)
                {
                    return;
                }

                if (pendingUris.Length > 0)
                {
                    await Dispatch();
                }
                else if (NumThreads == 0)
                {
                    OnDownloadsSuccess?.Invoke();
                    endTime = Environment.TickCount;
                    downloading = false;
                }
            });
        }

        /// <summary>
        /// 异步方式处理单个下载完成，不阻塞等待。
        /// </summary>
        /// <param name="idf"></param>
        /// <returns></returns>
        internal async UniTask DispatchCompletion(IDownloadExecutor idf)
        {
            await Locker.LockAsync(async () =>
            {
                if (!Downloading)
                {
                    idf.Cancel();
                    return;
                }

                if (idf.DidError)
                {
                    if (ContinueAfterFailure && pendingUris.Length > 0)
                    {
                        _ = Dispatch();
                    }
                    else
                    {
                        await Cancel();
                    }
                }
                else
                {
                    if (!idf.CompletedMultipartDownload && idf.MultipartDownload)
                    {
                        _ = Dispatch(idf);
                        return;
                    }
                    if (pendingUris.Length > 0)
                    {
                        _ = Dispatch();
                    }
                    else if (NumThreads == 0)
                    {
                        OnDownloadsSuccess?.Invoke();
                        endTime = Environment.TickCount;
                        downloading = false;
                    }
                }
            });
        }

        /// <summary>
        /// 获取对应的执行器。
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public IDownloadExecutor GetExecutor(string uri)
        {
            var exec = executors.FirstOrDefault(idf => idf.Uri == uri);
            if (exec != null)
            {
                return exec;
            }
            return executorsOld.FirstOrDefault(idf => idf.Uri == uri);
        }

        /// <summary>
        /// 取消所有下载。
        /// </summary>
        /// <returns></returns>
        public override UniTask<bool> Cancel()
        {
            downloading = false;
            OnCancel?.Invoke();
            OnCancelInvoked?.Invoke();
            endTime = Environment.TickCount;

            // 总是中止所有在途 executor（无论 AbandonOnFailure 配置），
            // 避免取消后网络 IO 继续运行、文件被重新写回、回调仍触发。
            // UWRExecutor.Cancel 内部会 Abort 在途 UWR，并视 abandonOnFailure 删除未完成文件。
            foreach (var idf in executors.Concat(executorsOld))
            {
                try { idf.Cancel(); } catch (Exception ex) { Log.Error("Cancel executor 异常: {0}", ex.Message); }
            }

            return UniTask.FromResult(true);
        }

        /// <summary>
        /// 取消单个 URI 的下载。
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public override UniTask<bool> Cancel(string uri)
        {
            OnCancelIndividual?.Invoke(uri);

            var exec = executors.FirstOrDefault(idf => idf.Uri == uri);
            if (exec != null)
            {
                exec.Cancel();
                executors = executors.Where(x => x != exec).ToArray();
                executorsOld = executorsOld.Append(exec).ToArray();
            }
            else if (!executorsOld.Any(idf => idf.Uri == uri))
            {
                Log.Error("从未下载过该 URI，无法取消 {0}", uri);
                return UniTask.FromResult(false);
            }
            else
            {
                Log.Error("已完成的 URI 无法取消");
            }
            return UniTask.FromResult(true);
        }

        /// <summary>
        /// 若 AbandonOnFailure 为真，失败时取消并清理已下载的文件。
        /// </summary>
        internal void HandleAbandonOnFailure()
        {
            if (AbandonOnFailure)
            {
                foreach (var idf in executors.Concat(executorsOld))
                {
                    idf.Cancel();
                }
            }
        }

        /// <summary>
        /// 重置下载器为初始下载状态。
        /// </summary>
        public override void Reset()
        {
            if (Downloading)
            {
                Log.Error("下载中无法执行重置，请先取消下载。");
                return;
            }
            downloading = false;
            timeout = 6;
            maxConcurrency = 4;
            abandonOnFailure = true;
            continueAfterFailure = false;
            paused = false;
            didError = false;
            numFilesRemaining = 0;
            startTime = 0;
            endTime = 0;
            pendingOffset = 0;
            pendingUris = Array.Empty<string>();
            downloadedUris = new List<string>();
            incompletedUris = new List<string>();
            executors = Array.Empty<IDownloadExecutor>();
            executorsOld = Array.Empty<IDownloadExecutor>();
            Uris = Array.Empty<string>();
            n = 0;
            initialCount = 0;
        }
    }
}
