using Cysharp.Threading.Tasks;
using ReunionMovement.Core.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.HttpService
{
    /// <summary>
    /// HTTP管理器 —— 同时作为 MonoBehaviour 单例和 GameEngine 模块（ISystemUpdatable 驱动进度轮询）。
    /// </summary>
    public class HttpMgr : SingletonMgr<HttpMgr>, ICustomSystem, ISystemUpdatable
    {
        /// <summary>HTTP 请求可能在切场景期间仍在途，保持跨场景存活</summary>
        protected override bool IsPersistentAcrossScenes => true;

        private IHttpService service;
        private Dictionary<string, string> superHeaders;
        private Dictionary<IHttpRequest, CancellationTokenSource> httpRequests;
        /// <summary>SuperHeaders 的只读包装缓存（superHeaders 变更时失效重建）</summary>
        private IReadOnlyDictionary<string, string> superHeadersReadOnlyCache;
        /// <summary>Update 进度轮询的复用快照数组（仅在扩容时分配，避免每帧分配）</summary>
        private IHttpRequest[] updateSnapshot = System.Array.Empty<IHttpRequest>();

        /// <summary>并发上限：在途请求数达到上限后新请求进入等待队列按 FIFO 派发；<=0 表示不限</summary>
        public int MaxConcurrentRequests = 8;

        /// <summary>等待队列上限：超出后新请求被直接拒绝（onError 429）；<=0 表示不限（防恶意调用无限排队）</summary>
        public int MaxPendingRequests = 256;

        /// <summary>等待派发的请求队列（并发上限生效时使用）</summary>
        private struct PendingHttpRequest
        {
            public IHttpRequest Request;
            public Action<HttpResponse> OnSuccess;
            public Action<HttpResponse> OnError;
            public Action<HttpResponse> OnNetworkError;
        }
        private readonly Queue<PendingHttpRequest> pendingRequests = new Queue<PendingHttpRequest>();

        protected override void Awake()
        {
            base.Awake();
            Init(new UnityHttpService());
        }

        /// <summary>ICustomSystem 初始化进度（恒为 100，HttpMgr 由 Awake 完成初始化）</summary>
        public double InitProgress => 100;

        /// <summary>
        /// ICustomSystem 初始化（幂等）：Awake 已通过 Init(IHttpService) 完成，这里仅保证接口契约。
        /// </summary>
        public UniTask Init()
        {
            if (service == null)
            {
                Init(new UnityHttpService());
            }
            return UniTask.CompletedTask;
        }

        /// <summary>ISystemUpdatable：GameEngine 运行时统一驱动进度轮询</summary>
        void ISystemUpdatable.Update(float logicTime, float realTime)
        {
            UpdateProgressPump();
        }

        /// <summary>
        /// 初始化Http
        /// </summary>
        /// <param name="service"></param>
        public void Init(IHttpService service)
        {
            superHeaders = new Dictionary<string, string>();
            httpRequests = new Dictionary<IHttpRequest, CancellationTokenSource>();
            // 重建字典后失效只读包装缓存,避免返回旧字典的包装
            superHeadersReadOnlyCache = null;
            this.service = service;
        }

        #region Super Headers
        /// <summary>
        /// SuperHeaders是键值对，将被添加到每个后续的HttpRequest中。
        /// 返回真正的只读包装，防止外部代码通过类型转换修改内部字典。
        /// </summary>
        /// <returns>A read-only wrapper of super-headers.</returns>
        public IReadOnlyDictionary<string, string> GetSuperHeaders()
        {
            // 缓存只读包装，避免每次请求 new ReadOnlyDictionary 堆分配；superHeaders 变更时置空重建
            return superHeadersReadOnlyCache ??= new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(superHeaders);
        }

        /// <summary>
        /// 将标头设置为SuperHeaders键值对，如果标头键已存在，则该值将被替换。
        /// </summary>
        /// <param name="key">要设置的标题键</param>
        /// <param name="value">要分配的标头值</param>
        public void SetSuperHeader(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("密钥不能为null或为空");
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("值不能为null或空，如果要删除该值，请使用RemoveSuperHeader（）方法。");
            }

            superHeaders[key] = value;
            superHeadersReadOnlyCache = null; // 失效缓存
        }

        /// <summary>
        /// 从“SuperHeaders”列表中删除标头
        /// </summary>
        /// <param name="key">要删除的标题键</param>
        /// <returns>如果元素移除成功</returns>
        public bool RemoveSuperHeader(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("密钥不能为null或为空");
            }

            bool removed = superHeaders.Remove(key);
            if (removed) superHeadersReadOnlyCache = null; // 失效缓存
            return removed;
        }

        #endregion

        #region 静态请求
        /// <summary>
        /// 创建一个配置为HTTP GET的HttpRequest
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static IHttpRequest Get(string uri)
        {
            return Instance.service.Get(uri);
        }

        /// <summary>
        /// 创建一个配置为HTTP GET的HttpRequest，用于获取纹理
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static IHttpRequest GetTexture(string uri)
        {
            return Instance.service.GetTexture(uri);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送字符串数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public static IHttpRequest Post(string uri, string postData)
        {
            return Instance.service.Post(uri, postData);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送表单数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="formData"></param>
        /// <returns></returns>
        public static IHttpRequest Post(string uri, WWWForm formData)
        {
            return Instance.service.Post(uri, formData);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送键值对形式的表单数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="formData"></param>
        /// <returns></returns>
        public static IHttpRequest Post(string uri, Dictionary<string, string> formData)
        {
            return Instance.service.Post(uri, formData);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送多部分表单数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="multipartForm"></param>
        /// <returns></returns>
        public static IHttpRequest Post(string uri, List<IMultipartFormSection> multipartForm)
        {
            return Instance.service.Post(uri, multipartForm);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送字节数组数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="bytes"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public static IHttpRequest Post(string uri, byte[] bytes, string contentType)
        {
            return Instance.service.Post(uri, bytes, contentType);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送JSON字符串数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        public static IHttpRequest PostJson(string uri, string json)
        {
            return Instance.service.PostJson(uri, json);
        }

        /// <summary>
        /// 创建一个配置为HTTP POST的HttpRequest，用于发送JSON对象数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uri"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public static IHttpRequest PostJson<T>(string uri, T payload) where T : class
        {
            return Instance.service.PostJson(uri, payload);
        }

        /// <summary>
        /// 创建一个配置为HTTP PUT的HttpRequest，用于发送字节数组数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="bodyData"></param>
        /// <returns></returns>
        public static IHttpRequest Put(string uri, byte[] bodyData)
        {
            return Instance.service.Put(uri, bodyData);
        }

        /// <summary>
        /// 创建一个配置为HTTP PUT的HttpRequest，用于发送字符串数据
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="bodyData"></param>
        /// <returns></returns>
        public static IHttpRequest Put(string uri, string bodyData)
        {
            return Instance.service.Put(uri, bodyData);
        }

        /// <summary>
        /// 创建一个配置为HTTP DELETE的HttpRequest
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static IHttpRequest Delete(string uri)
        {
            return Instance.service.Delete(uri);
        }

        /// <summary>
        /// 创建一个配置为HTTP HEAD的HttpRequest
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static IHttpRequest Head(string uri)
        {
            return Instance.service.Head(uri);
        }
        #endregion

        /// <summary>
        /// 发送请求并处理响应
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onError"></param>
        /// <param name="onNetworkError"></param>
        internal void Send(IHttpRequest request,
            Action<HttpResponse> onSuccess = null,
            Action<HttpResponse> onError = null,
            Action<HttpResponse> onNetworkError = null)
        {
            // 并发上限：超限请求入队，待在途请求完成后按 FIFO 派发（避免无限开连接压垮网络层）
            if (MaxConcurrentRequests > 0 && httpRequests.Count >= MaxConcurrentRequests)
            {
                // 等待队列上限：恶意/异常调用无限排队会内存无界增长，超限直接拒绝并回调错误
                if (MaxPendingRequests > 0 && pendingRequests.Count >= MaxPendingRequests)
                {
                    Log.Error("HttpMgr 等待队列已达上限 {0}，拒绝新请求 {1}", MaxPendingRequests, request.GetType().Name);
                    onError?.Invoke(new HttpResponse
                    {
                        isSuccessful = false,
                        isHttpError = true,
                        statusCode = 429,
                        error = "请求等待队列已满"
                    });
                    return;
                }

                Log.Debug("HttpMgr 并发上限 {0} 已达，请求 {1} 进入等待队列（队列长度 {2}）",
                    MaxConcurrentRequests, request.GetType().Name, pendingRequests.Count + 1);
                pendingRequests.Enqueue(new PendingHttpRequest
                {
                    Request = request,
                    OnSuccess = onSuccess,
                    OnError = onError,
                    OnNetworkError = onNetworkError,
                });
                return;
            }

            var cts = new CancellationTokenSource();
            httpRequests[request] = cts;
            SendAsync(request, cts, onSuccess, onError, onNetworkError).Forget();
        }

        /// <summary>
        /// 带重试发送：网络错误（连接失败/超时）时按指数退避重试，HTTP 错误（4xx/5xx）不重试。
        /// 通过 requestFactory 每次重试新建请求（UnityHttpRequest 的 UWR 不可重复发送）。
        /// </summary>
        /// <param name="requestFactory">请求工厂（每次重试都会重新调用）</param>
        /// <param name="maxRetries">网络错误最大重试次数（总发送次数 = maxRetries + 1）</param>
        /// <param name="retryDelaySeconds">首次重试等待秒数（指数退避 ×2）</param>
        public void SendWithRetry(Func<IHttpRequest> requestFactory, int maxRetries = 3, float retryDelaySeconds = 1f,
            Action<HttpResponse> onSuccess = null, Action<HttpResponse> onError = null, Action<HttpResponse> onNetworkError = null)
        {
            if (requestFactory == null)
            {
                throw new ArgumentException("requestFactory 不能为 null");
            }
            SendWithRetryAsync(requestFactory, Mathf.Max(0, maxRetries), Mathf.Max(0f, retryDelaySeconds),
                onSuccess, onError, onNetworkError).Forget();
        }

        /// <summary>带重试发送的实现（UniTask 驱动，指数退避）</summary>
        private async UniTaskVoid SendWithRetryAsync(Func<IHttpRequest> requestFactory, int maxRetries, float retryDelaySeconds,
            Action<HttpResponse> onSuccess, Action<HttpResponse> onError, Action<HttpResponse> onNetworkError)
        {
            float delay = retryDelaySeconds;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                IHttpRequest request;
                try
                {
                    request = requestFactory();
                }
                catch (Exception ex)
                {
                    Log.Error("HttpMgr.SendWithRetry 创建请求失败: {0}", ex.Message);
                    return;
                }

                // 单次尝试的结果信号（回调只转发一次，避免重复触发）
                var tcs = new UniTaskCompletionSource<HttpResponse>();
                bool networkFailed = false;
                try
                {
                    Send(request,
                        resp => { networkFailed = false; tcs.TrySetResult(resp); },
                        resp => { networkFailed = false; tcs.TrySetResult(resp); },
                        resp => { networkFailed = true; tcs.TrySetResult(resp); });

                    // Abort 悬挂防护：请求被外部 HttpMgr.Abort 时回调不会触发（协程被括断），
                    // tcs 永不完成 → 裸 await 永久挂起。与“请求已释放”竞速，及时终结本次尝试。
                    await UniTask.WhenAny(
                        tcs.Task,
                        UniTask.WaitUntil(() => request is UnityHttpRequest ur && ur.IsDisposed));

                    // 请求被外部 Abort（tcs 未完成但底层已释放）：尊重放弃语义，不再重试
                    if (request is UnityHttpRequest disposedReq && disposedReq.IsDisposed
                        && tcs.Task.Status != UniTaskStatus.Succeeded)
                    {
                        Log.Warning("HttpMgr.SendWithRetry 请求被外部 Abort，停止重试");
                        onError?.Invoke(new HttpResponse
                        {
                            isSuccessful = false,
                            isHttpError = true,
                            error = "请求已被取消"
                        });
                        return;
                    }

                    var resp = await tcs.Task;
                    if (!networkFailed || attempt >= maxRetries)
                    {
                        // 最终结果：按类别转发回调
                        if (networkFailed) onNetworkError?.Invoke(resp);
                        else if (resp.isSuccessful) onSuccess?.Invoke(resp);
                        else onError?.Invoke(resp);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("HttpMgr.SendWithRetry 第 {0} 次尝试异常: {1}", attempt + 1, ex.Message);
                    if (attempt >= maxRetries) return;
                }

                // 网络错误且未耗尽重试：指数退避后重试
                Log.Warning("HttpMgr.SendWithRetry 网络错误，{0}s 后重试（{1}/{2}）", delay, attempt + 1, maxRetries);
                await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true);
                delay *= 2f;
            }
        }

        /// <summary>
        /// 异步发送请求并处理响应（UniTask 零 GC）
        /// </summary>
        private async UniTaskVoid SendAsync(IHttpRequest request, CancellationTokenSource cts, Action<HttpResponse> onSuccess = null,
            Action<HttpResponse> onError = null, Action<HttpResponse> onNetworkError = null)
        {
            try
            {
                bool canceled = await service.Send(request, onSuccess, onError, onNetworkError).ToUniTask(cancellationToken: cts.Token).SuppressCancellationThrow();
                if (!canceled)
                    httpRequests.Remove(request);
            }
            catch (Exception ex)
            {
                // service.Send 同步抛异常（如类型强转失败）时，
                // 必须移除 httpRequests 条目，避免泄漏
                httpRequests.Remove(request);
                Debug.LogError("HttpMgr.SendAsync 异常: " + ex);
            }
            finally
            {
                // 请求完成：释放 CTS（Abort 路径的 Cancel+Dispose 与这里幂等，可重复调用）
                cts.Dispose();
                // 释放并发名额后，从等待队列派发下一个请求
                TryDispatchNext();
            }
        }

        /// <summary>从等待队列派发请求（在途数低于并发上限时按 FIFO 顺序逐个发送）</summary>
        private void TryDispatchNext()
        {
            while (pendingRequests.Count > 0
                   && (MaxConcurrentRequests <= 0 || httpRequests.Count < MaxConcurrentRequests))
            {
                var pending = pendingRequests.Dequeue();
                // 排队期间被 Abort 释放的请求直接跳过（UnityHttpRequest.Dispose 后不可再发送）
                if (pending.Request is UnityHttpRequest unityReq && unityReq.IsDisposed)
                {
                    continue;
                }
                var cts = new CancellationTokenSource();
                httpRequests[pending.Request] = cts;
                SendAsync(pending.Request, cts, pending.OnSuccess, pending.OnError, pending.OnNetworkError).Forget();
            }
        }

        /// <summary>
        /// MonoBehaviour Update 兜底：仅在 GameEngine 未运行时自行轮询进度，
        /// 引擎运行时会通过 ISystemUpdatable.Update 驱动，避免双重轮询。
        /// </summary>
        public void Update()
        {
            // 引擎运行中由 ISystemUpdatable.Update 驱动，避免双重轮询；
            // 引擎未运行（含运行前/销毁后）才用 MonoBehaviour Update 兜底
            if (ModuleRuntime.IsEngineRunning) return;
            UpdateProgressPump();
        }

        /// <summary>轮询所有在途请求的下载/上传进度（零分配快照）</summary>
        private void UpdateProgressPump()
        {
            // 快速路径：无请求时跳过
            if (httpRequests.Count == 0) return;

            // 复用快照数组（仅在扩容时分配），替代每次 new + CopyTo 的每帧分配；
            // 快照防止进度回调中修改集合导致异常
            var requests = httpRequests.Keys;
            if (updateSnapshot.Length < requests.Count)
            {
                updateSnapshot = new IHttpRequest[requests.Count];
            }
            int count = requests.Count;
            // 收缩：峰值并发过后回收过大快照（防止一次突发 500 并发后永久持有大数组）
            if (updateSnapshot.Length > count * 2 + 8)
            {
                updateSnapshot = new IHttpRequest[count];
            }
            requests.CopyTo(updateSnapshot, 0);
            for (int i = 0; i < count; i++)
            {
                (updateSnapshot[i] as IUpdateProgress)?.UpdateProgress();
            }
        }

        /// <summary>
        /// 中止请求并将其从活动请求列表中删除
        /// </summary>
        /// <param name="request"></param>
        internal void Abort(IHttpRequest request)
        {
            service.Abort(request);

            if (httpRequests.TryGetValue(request, out CancellationTokenSource cts))
            {
                cts?.Cancel();
                cts?.Dispose();
                httpRequests.Remove(request);
            }

            // 取消路径下协程被 cts 掐断,UnityHttpService.Send 的 using 不会执行 → UWR 原生资源泄漏。
            // 在此显式 Dispose（正常完成路径由协程 using 负责,幂等）
            (request as IDisposable)?.Dispose();

            // 若请求仍在等待队列中：已 Dispose 的请求会在派发时被跳过（见 TryDispatchNext），
            // 此处顺带派发队列，避免名额空置
            TryDispatchNext();
        }
    }
}
