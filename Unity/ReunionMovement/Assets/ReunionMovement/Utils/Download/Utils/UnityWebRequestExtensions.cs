using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// UnityWebRequest 扩展 —— await UnityWebRequestAsyncOperation 的零分配实现。
    /// 直接复用 UniTask 内置的 UnityWebRequestAsyncOperationConfiguredSource（池化）与 ToUniTask，
    /// 无闭包、无 async 状态机分配（旧实现 UniTask.Create(async ...) 每次 await 均分配）。
    /// 注意：网络错误（ConnectionError/DataProcessingError/ProtocolError）会抛 UnityWebRequestException，
    /// 需要“错误不抛出”语义时在调用侧捕获（见 HTTPHelper.Get）。
    /// </summary>
    public static class UnityWebRequestExtensions
    {
        /// <summary>
        /// 将 UnityWebRequestAsyncOperation 转为可 await（转发 UniTask 内置零分配实现）
        /// </summary>
        public static UniTask<UnityWebRequest>.Awaiter GetAwaiter(this UnityWebRequestAsyncOperation reqOp)
        {
            return reqOp.ToUniTask().GetAwaiter();
        }
    }
}