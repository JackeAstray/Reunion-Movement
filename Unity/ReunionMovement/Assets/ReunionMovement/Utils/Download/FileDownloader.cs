using Cysharp.Threading.Tasks;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
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

        internal readonly SemaphoreLocker Locker = new SemaphoreLocker();

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
        // 被 Cancel(string uri) 标记为取消的 pendingUris 索引（按位置而非 URI 值，兼容重复 URI）。
        // Cancel 只移除 executors 而不动 pendingUris，必须同步标记位置才能维持双队列 FIFO 对齐。
        private readonly HashSet<int> cancelledPendingIndices = new HashSet<int>();

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
                for (int i = 0; i < executors.Count; i++)
                {
                    totalBytes += executors[i].BytesDownloaded;
                    totalElapsed += executors[i].ElapsedTime;
                }
                for (int i = 0; i < executorsOld.Count; i++)
                {
                    totalBytes += executorsOld[i].BytesDownloaded;
                    totalElapsed += executorsOld[i].ElapsedTime;
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
            // Uris 已在上方守卫保证非空非空数组，Clone 替代 LINQ ToArray
            pendingUris = (string[])Uris.Clone();
            pendingOffset = 0;
            cancelledPendingIndices.Clear();
            numFilesRemaining = Uris.Length;
            startTime = Environment.TickCount;
            downloading = true;

            // 二次 Download() 支持：上一轮下载结束后 executors 已被 Dispatch 清空，
            // 通过 Uris setter 重建执行器队列（含 URI 过滤、事件订阅与请求头深拷贝），否则会卡死到超时。
            if (executors.Count == 0)
            {
                executorsOld.Clear();
                string[] currentUris = Uris;
                Uris = currentUris;
            }

            initialCount = Uris.Length;
            int threadCount = Math.Min(MaxConcurrency, numFilesRemaining);
            if (threadCount <= 0)
            {
                Log.Error("{0}.错误：MaxConcurrency 需要大于 0", GetType().FullName);
                // 回滚 downloading 状态，避免实例永久卡在 Downloading 状态无法再次调用
                downloading = false;
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
            bool timedOut = false;

            while (Downloading && !(DidError && !ContinueAfterFailure))
            {
                if (waited >= maxWaitSeconds)
                {
                    Log.Error("下载超时：已等待 {0} 秒，仍有 {1} 个文件未完成，强制取消下载", maxWaitSeconds, NumFilesRemaining);
                    timedOut = true;
                    await Cancel();
                    break;
                }
                await UniTask.Delay((int)(pollInterval * 1000));
                waited += pollInterval;
            }

            // 返回真实结果：未超时 且（无失败 或 配置为容忍失败继续）才算成功。
            // 修复：原先无条件 return true，超时/失败时调用方无法区分。
            return !timedOut && (!DidError || ContinueAfterFailure);
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

            if (FindExecutor(executors, uri) == null && FindExecutor(executorsOld, uri) == null)
            {
                var idf = DownloadExecutorFactory.CreateFromClassName(iDownloadExecutorClassName);
                idf.Uri = uri;
                idf.DownloadPath = DownloadPath;
                idf.IsMD5Name = IsMD5Name;
                idf.DownloadToRoot = DownloadToRoot;
                idf.AbandonOnFailure = AbandonOnFailure;
                idf.Timeout = Timeout;
                // pendingUris 可能为 null（未先调用无参 Download()），需判空后再追加。
                // 使用项目自带的 string[] 扩展 Add（非 LINQ），避免 Append+ToArray 分配
                pendingUris = (pendingUris ?? Array.Empty<string>()).Add(uri);
                executors.Add(idf);
                numFilesRemaining++;
            }
            else if (FindExecutor(executorsOld, uri) != null)
            {
                var idf = FindExecutor(executorsOld, uri);
                executorsOld.Remove(idf);
                pendingUris = (pendingUris ?? Array.Empty<string>()).Add(uri);
                executors.Add(idf);
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
            // 跳过被 Cancel(string uri) 标记的位置，维持 pendingUris 与 executors 的 FIFO 对齐
            // （Cancel 已从 executors 移除对应执行器并递减 numFilesRemaining，此处只推进偏移）
            while (pendingUris != null && pendingOffset < pendingUris.Length &&
                   cancelledPendingIndices.Contains(pendingOffset))
            {
                cancelledPendingIndices.Remove(pendingOffset);
                pendingOffset++;
            }

            if (pendingUris == null || pendingOffset >= pendingUris.Length)
            {
                return ReturnFalseAsync();
            }

            // executors 与异步回调访问 pendingUris 不同步，需越界保护
            if (executors.Count == 0)
            {
                Log.Error("Dispatch: executors 列表为空，无法分发任务");
                return ReturnFalseAsync();
            }

            string uri = pendingUris[pendingOffset];
            IDownloadExecutor idf = executors[0];
            pendingOffset++;
            // List 原地操作：头部移除 + 尾部追加，零数组分配
            // （原 Skip(1).ToArray() + Append().ToArray() 每次分发分配两个新数组）
            executors.RemoveAt(0);
            executorsOld.Add(idf);

            if (idf.CompletedMultipartDownload)
            {
                // 该 executor 已完成分块下载（可能被 Download(string uri) 从 executorsOld 重新入队）：
                // 必须走 DispatchCompletion 递减计数并继续分发下一个任务，
                // 否则 numFilesRemaining 永不归零 → OnDownloadsSuccess 永不触发，任务队列卡死。
                _ = DispatchCompletion(idf);
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
                        // 取消后 HEAD 完成回调不再发起首块下载，避免取消后仍产生网络 IO 与文件写入
                        if (!Downloading)
                        {
                            n--;
                            return;
                        }
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
                            // 本分支已完成一次 Head 请求（n++ 已配对），若不递减 n，
                            // NumThreads 永不归零 → OnDownloadsSuccess 永不触发
                            n--;
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
            bool invokeSuccess = false;
            await Locker.LockAsync(async () =>
            {
                if (!Downloading)
                {
                    return;
                }

                // 该 URI 未启动下载（Download 返回 null），视为已处理
                numFilesRemaining = Math.Max(0, numFilesRemaining - 1);
                // pendingUris 数组永不收缩，必须用 offset 判断是否还有待分发任务
                if (pendingOffset < pendingUris.Length)
                {
                    await Dispatch();
                }
                else if (NumThreads == 0)
                {
                    invokeSuccess = true;
                    endTime = Environment.TickCount;
                    downloading = false;
                }
            });

            // 用户回调移出锁外：避免锁内执行用户代码导致的重入死锁（SemaphoreSlim 非可重入）
            if (invokeSuccess)
            {
                OnDownloadsSuccess?.Invoke();
            }
        }

        /// <summary>
        /// 异步方式处理单个下载完成，不阻塞等待。
        /// </summary>
        /// <param name="idf"></param>
        /// <returns></returns>
        internal async UniTask DispatchCompletion(IDownloadExecutor idf)
        {
            bool invokeSuccess = false;
            await Locker.LockAsync(async () =>
            {
                if (!Downloading)
                {
                    idf.Cancel();
                    return;
                }

                if (idf.DidError)
                {
                    // 记录下载器级失败状态（Download() 据此返回结果，调用方可区分成败）
                    didError = true;
                    numFilesRemaining = Math.Max(0, numFilesRemaining - 1);
                    if (ContinueAfterFailure && pendingOffset < pendingUris.Length)
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
                    numFilesRemaining = Math.Max(0, numFilesRemaining - 1);
                    if (pendingOffset < pendingUris.Length)
                    {
                        _ = Dispatch();
                    }
                    else if (NumThreads == 0)
                    {
                        invokeSuccess = true;
                        endTime = Environment.TickCount;
                        downloading = false;
                    }
                }
            });

            // 用户回调移出锁外（同上：避免锁内执行用户代码）
            if (invokeSuccess)
            {
                OnDownloadsSuccess?.Invoke();
            }
        }

        /// <summary>
        /// 获取对应的执行器。
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public IDownloadExecutor GetExecutor(string uri)
        {
            var exec = FindExecutor(executors, uri);
            if (exec != null)
            {
                return exec;
            }
            return FindExecutor(executorsOld, uri);
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
            for (int i = 0; i < executors.Count; i++)
            {
                try { executors[i].Cancel(); } catch (Exception ex) { Log.Error("Cancel executor 异常: {0}", ex.Message); }
            }
            for (int i = 0; i < executorsOld.Count; i++)
            {
                try { executorsOld[i].Cancel(); } catch (Exception ex) { Log.Error("Cancel executor 异常: {0}", ex.Message); }
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

            int idx = FindExecutorIndex(executors, uri);
            if (idx >= 0)
            {
                var exec = executors[idx];
                exec.Cancel();
                executors.RemoveAt(idx);
                executorsOld.Add(exec);

                // 关键修复：同步标记 pendingUris 对应位置为跳过，否则 Dispatch 取到的
                // URI 与 executors[0] 错位 → 文件被写到错误执行器的路径/请求头，甚至卡死。
                // executors 与 pendingUris[pendingOffset..] 严格对齐，故对应索引为 pendingOffset + idx。
                int pendingIndex = pendingOffset + idx;
                if (pendingUris != null && pendingIndex < pendingUris.Length)
                {
                    cancelledPendingIndices.Add(pendingIndex);
                }
                // 该 URI 的任务被终结（未分发即无在途 UWR，不会再有 completed 回调递减计数）
                numFilesRemaining = Math.Max(0, numFilesRemaining - 1);
            }
            else if (FindExecutor(executorsOld, uri) == null)
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
                for (int i = 0; i < executors.Count; i++) executors[i].Cancel();
                for (int i = 0; i < executorsOld.Count; i++) executorsOld[i].Cancel();
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
            cancelledPendingIndices.Clear();
            pendingUris = Array.Empty<string>();
            downloadedUris = new List<string>();
            incompletedUris = new List<string>();
            executors.Clear();
            executorsOld.Clear();
            Uris = Array.Empty<string>();
            n = 0;
            initialCount = 0;
        }

        /// <summary>在 executor 列表中按 URI 查找（零分配，替代 LINQ FirstOrDefault/Any）</summary>
        private static IDownloadExecutor FindExecutor(List<IDownloadExecutor> list, string uri)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Uri == uri) return list[i];
            }
            return null;
        }

        /// <summary>在 executor 列表中按 URI 查找索引（Cancel 需要索引以标记 pendingUris 对应位置）</summary>
        private static int FindExecutorIndex(List<IDownloadExecutor> list, string uri)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Uri == uri) return i;
            }
            return -1;
        }
    }
}
