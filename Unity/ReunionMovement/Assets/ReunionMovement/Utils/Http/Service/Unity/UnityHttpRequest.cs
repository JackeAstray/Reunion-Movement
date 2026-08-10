using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.HttpService
{
    public class UnityHttpRequest : IHttpRequest, IUpdateProgress, IDisposable
    {
        internal UnityWebRequest UnityWebRequest => unityWebRequest;

        private readonly UnityWebRequest unityWebRequest;
        private readonly Dictionary<string, string> headers;
        // 防重入：Send() 只能调用一次，重复调用会覆盖请求跟踪并产生无效的第二次协程
        private bool sent;

        private event Action<float> onUploadProgress;
        private event Action<float> onDownloadProgress;
        private event Action<HttpResponse> onSuccess;
        private event Action<HttpResponse> onError;
        private event Action<HttpResponse> onNetworkError;

        private float downloadProgress;
        private float uploadProgress;

        public UnityHttpRequest(UnityWebRequest unityWebRequest)
        {
            this.unityWebRequest = unityWebRequest;
            headers = new Dictionary<string, string>(HttpMgr.Instance.GetSuperHeaders());
        }

        /// <summary>
        /// 移除所有超级头
        /// </summary>
        /// <returns></returns>
        public IHttpRequest RemoveSuperHeaders()
        {
            foreach (var kvp in HttpMgr.Instance.GetSuperHeaders())
            {
                headers.Remove(kvp.Key);
            }

            return this;
        }

        /// <summary>
        /// 设置请求头
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IHttpRequest SetHeader(string key, string value)
        {
            headers[key] = value;
            return this;
        }

        /// <summary>
        /// 设置多个请求头
        /// </summary>
        /// <param name="headers"></param>
        /// <returns></returns>
        public IHttpRequest SetHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach (var kvp in headers)
            {
                SetHeader(kvp.Key, kvp.Value);
            }

            return this;
        }

        /// <summary>
        /// 设置上传进度回调
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public IHttpRequest OnUploadProgress(Action<float> onProgress)
        {
            onUploadProgress += onProgress;
            return this;
        }

        /// <summary>
        /// 设置下载进度回调
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public IHttpRequest OnDownloadProgress(Action<float> onProgress)
        {
            onDownloadProgress += onProgress;
            return this;
        }

        /// <summary>
        /// 设置成功回调
        /// </summary>
        /// <param name="onSuccess"></param>
        /// <returns></returns>
        public IHttpRequest OnSuccess(Action<HttpResponse> onSuccess)
        {
            this.onSuccess += onSuccess;
            return this;
        }

        /// <summary>
        /// 设置错误回调
        /// </summary>
        /// <param name="onError"></param>
        /// <returns></returns>
        public IHttpRequest OnError(Action<HttpResponse> onError)
        {
            this.onError += onError;
            return this;
        }

        /// <summary>
        /// 设置网络错误回调
        /// </summary>
        /// <param name="onNetworkError"></param>
        /// <returns></returns>
        public IHttpRequest OnNetworkError(Action<HttpResponse> onNetworkError)
        {
            this.onNetworkError += onNetworkError;
            return this;
        }

        /// <summary>
        /// 移除指定的请求头
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool RemoveHeader(string key)
        {
            return headers.Remove(key);
        }

        /// <summary>
        /// 设置请求超时时间
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        public IHttpRequest SetTimeout(int duration)
        {
            unityWebRequest.timeout = duration;
            return this;
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <returns></returns>
        public IHttpRequest Send()
        {
            // 防重入：重复 Send 会覆盖 HttpMgr 的请求跟踪条目（首条请求失去取消能力）
            if (sent) return this;
            sent = true;

            foreach (var header in headers)
            {
                unityWebRequest.SetRequestHeader(header.Key, header.Value);
            }

            HttpMgr.Instance.Send(this, onSuccess, onError, onNetworkError);
            return this;
        }

        /// <summary>
        /// 释放底层 UnityWebRequest 原生资源。
        /// 取消路径（协程被 cts 掐断、using 不执行）由 HttpMgr.Abort 调用，防止原生泄漏。
        /// </summary>
        public void Dispose()
        {
            unityWebRequest?.Dispose();
        }

        /// <summary>
        /// 设置重定向限制
        /// </summary>
        /// <param name="redirectLimit"></param>
        /// <returns></returns>
        public IHttpRequest SetRedirectLimit(int redirectLimit)
        {
            unityWebRequest.redirectLimit = redirectLimit;
            return this;
        }

        /// <summary>
        /// 更新下载和上传进度
        /// </summary>
        public void UpdateProgress()
        {
            UpdateProgress(ref downloadProgress, unityWebRequest.downloadProgress, onDownloadProgress);
            UpdateProgress(ref uploadProgress, unityWebRequest.uploadProgress, onUploadProgress);
        }

        /// <summary>
        /// 中止请求
        /// </summary>
        public void Abort()
        {
            HttpMgr.Instance.Abort(this);
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="currentProgress"></param>
        /// <param name="progress"></param>
        /// <param name="onProgress"></param>
        private void UpdateProgress(ref float currentProgress, float progress, Action<float> onProgress)
        {
            if (currentProgress < progress)
            {
                currentProgress = progress;
                onProgress?.Invoke(currentProgress);
            }
        }
    }
}
