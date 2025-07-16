using ReunionMovement.Common.Util.Coroutiner;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.HttpService
{
    /// <summary>
    /// HTTP管理器
    /// </summary>
    public class HttpMgr : SingletonMgr<HttpMgr>
    {
        private IHttpService service;
        private Dictionary<string, string> superHeaders;
        private Dictionary<IHttpRequest, Coroutine> httpRequests;

        public bool isInit = false;

        public void Start()
        {
            Init(new UnityHttpService());
        }

        /// <summary>
        /// 初始化Http
        /// </summary>
        /// <param name="service"></param>
        public void Init(IHttpService service)
        {
            superHeaders = new Dictionary<string, string>();
            httpRequests = new Dictionary<IHttpRequest, Coroutine>();
            this.service = service;
            isInit = true;
        }

        #region Super Headers

        /// <summary>
        /// SuperHeaders是键值对，将被添加到每个后续的HttpRequest中。
        /// </summary>
        /// <returns>A dictionary of super-headers.</returns>
        public Dictionary<string, string> GetSuperHeaders()
        {
            return new Dictionary<string, string>(superHeaders);
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

            return superHeaders.Remove(key);
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
            var enumerator = SendCoroutine(request, onSuccess, onError, onNetworkError);
            var coroutine = StartCoroutine(enumerator);
            httpRequests.Add(request, coroutine);
        }

        /// <summary>
        /// 用于发送请求和处理响应的协程
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onError"></param>
        /// <param name="onNetworkError"></param>
        /// <returns></returns>
        private IEnumerator SendCoroutine(IHttpRequest request, Action<HttpResponse> onSuccess = null,
            Action<HttpResponse> onError = null, Action<HttpResponse> onNetworkError = null)
        {
            yield return service.Send(request, onSuccess, onError, onNetworkError);
            httpRequests.Remove(request);
        }

        /// <summary>
        /// 中止请求并将其从活动请求列表中删除
        /// </summary>
        /// <param name="request"></param>
        internal void Abort(IHttpRequest request)
        {
            service.Abort(request);

            if (httpRequests.TryGetValue(request, out Coroutine coroutine))
            {
                if (httpRequests.ContainsKey(request))
                {
                    StopCoroutine(httpRequests[request]);
                }

                httpRequests.Remove(request);
            }
        }

        public void Update()
        {
            foreach (var httpRequest in httpRequests.Keys)
            {
                (httpRequest as IUpdateProgress)?.UpdateProgress();
            }
        }
    }
}
