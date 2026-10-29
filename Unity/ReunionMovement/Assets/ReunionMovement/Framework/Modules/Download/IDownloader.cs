using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// 下载器接口
    /// </summary>
    public abstract class IDownloader
    {
        /// <summary>
        /// URI列表
        /// </summary>
        protected string[] uris = Array.Empty<string>();
        /// <summary>
        /// 下载器的URI列表
        /// </summary>
        protected List<string> downloadedUris = new List<string>();
        /// <summary>
        /// 不完整的URI列表
        /// </summary>
        protected List<string> incompletedUris = new List<string>();
        /// <summary>
        /// 是否使用分块下载
        /// </summary>
        public Dictionary<string, string> requestHeaders = null;
        /// <summary>
        /// 是否使用分块下载
        /// </summary>
        public bool tryMultipartDownload = true;
        /// <summary>
        /// 下载执行器（List 而非数组：Dispatch 每分发一个文件都要从头部移除、尾部追加，
        /// 数组 + LINQ Skip/Append 会每次分配新数组，改为 List 后零分配）
        /// </summary>
        protected readonly List<IDownloadExecutor> executors = new List<IDownloadExecutor>();
        /// <summary>
        /// 旧的下载执行器
        /// </summary>
        protected readonly List<IDownloadExecutor> executorsOld = new List<IDownloadExecutor>();
        /// <summary>
        /// 下载执行器类名
        /// </summary>
        public virtual string iDownloadExecutorClassName => "UWRExecutor";
        /// <summary>
        /// 下载错误事件
        /// </summary>
        public event Action<string, int, string> OnDownloadError;
        /// <summary>
        /// 下载成功事件
        /// </summary>
        public event Action<string> OnDownloadSuccess;
        public event Action<string> OnDownloadChunkedSucces;

        public abstract UniTask<bool> Download();
        public abstract UniTask<bool> Download(string uri);
        public abstract UniTask<bool> Cancel(string uri);
        public abstract UniTask<bool> Cancel();
        public abstract void Reset();

        public float Progress
        {
            get
            {
                int total = executors.Count + executorsOld.Count;
                if (total == 0)
                {
                    return 0f;
                }

                float prog = 0f;
                for (int i = 0; i < executors.Count; i++) prog += executors[i].Progress;
                for (int i = 0; i < executorsOld.Count; i++) prog += executorsOld[i].Progress;

                return prog / total;
            }
        }

        public int NumFilesTotal => executors.Count;
        public bool Completed => Progress == 1.0f;
        public int MultipartChunkSize = 200000;

        public abstract string DownloadPath { get; set; }
        public abstract bool DownloadToRoot { get; set; }
        public abstract bool IsMD5Name { get; set; }

        /// <summary>
        /// 获取下载进度
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public float GetProgress(string uri)
        {
            if (string.IsNullOrEmpty(uri) || uris == null || uris.Length == 0)
            {
                return 0f;
            }

            foreach (var exec in executors)
            {
                if (exec.Uri == uri) return exec.Progress;
            }
            foreach (var exec in executorsOld)
            {
                if (exec.Uri == uri) return exec.Progress;
            }

            return 0f;
        }

        public string[] Uris
        {
            get => uris;
            set
            {
                if (value == null)
                {
                    return;
                }

                var validUris = new List<string>();
                var newExecutors = new List<IDownloadExecutor>();

                foreach (var str in value)
                {
                    if (string.IsNullOrWhiteSpace(str))
                    {
                        continue;
                    }

                    if (!Uri.TryCreate(str, UriKind.Absolute, out _))
                    {
                        Log.Error("URI {0} cannot be fed into {1}.Uris", str, GetType().Name);
                        continue;
                    }

                    var idf = DownloadExecutorFactory.CreateFromClassName(iDownloadExecutorClassName);
                    idf.Uri = str;
                    idf.DownloadPath = DownloadPath;
                    idf.IsMD5Name = IsMD5Name;
                    idf.DownloadToRoot = DownloadToRoot;
                    idf.AbandonOnFailure = AbandonOnFailure;
                    idf.Timeout = Timeout;
                    // 深拷贝 RequestHeaders：多个并发分块 executor 共享同一字典时，
                    // 各自的 Remove/Add("Range") 会互相覆盖导致 Range 头错乱、文件损坏
                    idf.RequestHeaders = requestHeaders != null
                        ? new Dictionary<string, string>(requestHeaders)
                        : null;
                    idf.TryMultipartDownload = tryMultipartDownload;
                    idf.InitialChunkSize = MultipartChunkSize;
                    newExecutors.Add(idf);

                    if (idf is UWRExecutor uwr)
                    {
                        uwr.OnDownloadChunkedSucces += () => OnDownloadChunkedSucces?.Invoke(idf.Uri);
                        uwr.OnDownloadError += (errorCode, errorMsg) =>
                        {
                            DidError = true;
                            OnDownloadError?.Invoke(idf.Uri, errorCode, errorMsg);
                            incompletedUris.Add(idf.Uri);
                        };
                        uwr.OnDownloadSuccess += () =>
                        {
                            downloadedUris.Add(idf.Uri);
                            OnDownloadSuccess?.Invoke(idf.Uri);
                        };
                    }

                    validUris.Add(str);
                }

                uris = validUris.ToArray();
                // List 原地替换（readonly 字段不可重新赋值）：先清空再填充，避免数组分配
                executors.Clear();
                executors.AddRange(newExecutors);
            }
        }

        public abstract int Timeout { get; set; }
        public abstract int MaxConcurrency { get; set; }
        public abstract float NumFilesPerSecond { get; }
        public abstract bool AbandonOnFailure { get; set; }
        public abstract bool ContinueAfterFailure { get; set; }
        public abstract bool Downloading { get; }
        public abstract bool Paused { get; }
        public abstract bool DidError { get; protected set; }
        public abstract int NumFilesRemaining { get; }
        public abstract long StartTime { get; }
        public abstract long EndTime { get; }

        public long ElapsedTime
        {
            get
            {
                if (StartTime == 0)
                {
                    return 0;
                }

                if (EndTime == 0)
                {
                    long diff = Environment.TickCount - StartTime;
                    return diff >= 0 ? diff : 0;
                }

                long endDiff = EndTime - StartTime;
                return endDiff >= 0 ? endDiff : 0;
            }
        }

        public abstract string[] PendingURIS { get; }
        public string[] DownloadedUris => downloadedUris.ToArray();
        public abstract List<string> IncompletedURIS { get; }
    }
}