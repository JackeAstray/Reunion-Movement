using Cysharp.Threading.Tasks;
using ReunionMovement.Core;
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
            var cts = new CancellationTokenSource();
            httpRequests[request] = cts;
            SendAsync(request, cts, onSuccess, onError, onNetworkError).Forget();
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
            }
        }

        /// <summary>
        /// MonoBehaviour Update 兜底：仅在 GameEngine 未运行时自行轮询进度，
        /// 引擎运行时会通过 ISystemUpdatable.Update 驱动，避免双重轮询。
        /// </summary>
        public void Update()
        {
            if (GameEngine.Current != null && GameEngine.Current.State == EngineState.Running) return;
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
            requests.CopyTo(updateSnapshot, 0);
            int count = requests.Count;
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
        }
    }
}
