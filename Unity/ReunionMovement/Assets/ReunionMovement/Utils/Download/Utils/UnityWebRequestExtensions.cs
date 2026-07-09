using Cysharp.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// UnityWebRequest扩展（UniTask 零 GC）
    /// </summary>
    public static class UnityWebRequestExtensions
    {
        /// <summary>
        /// 将UnityWebRequest转换为字符串
        /// </summary>
        public static string ToString(this UnityWebRequest uwr)
        {
            if (uwr == null)
                return "UnityWebRequest: null";

            return $"TYPE: {uwr.method}\nURL: {uwr.url}\nURI: {uwr.uri}\nResponseCode: {uwr.responseCode}\nError: {uwr.error}";
        }

        /// <summary>
        /// 将UnityWebRequestAsyncOperation转换为UniTask awaiter（零 GC）
        /// </summary>
        public static UniTask<UnityWebRequest.Result>.Awaiter GetAwaiter(this UnityWebRequestAsyncOperation reqOp)
        {
            return UniTask.Create(async () =>
            {
                var uwr = await reqOp.ToUniTask();
                return uwr.result;
            }).GetAwaiter();
        }
    }
}