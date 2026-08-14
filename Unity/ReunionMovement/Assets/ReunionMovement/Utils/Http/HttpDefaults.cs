namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 双 HTTP 体系的共享默认约定（集中管理，修改约定只改本文件）。
    /// 体系分工：
    ///   1) Utils/Http（HttpMgr + UnityHttpService + IHttpService）：通用业务请求
    ///      （GET/POST/PUT/DELETE/HEAD、JSON、纹理、表单），协程驱动；
    ///      错误约定：不抛异常，按 onSuccess / onError(HTTP错误) / onNetworkError(连接错误) 三回调分发。
    ///   2) Utils/Download（DownloadMgr + HTTPHelper + UWRExecutor/FileDownloader）：文件下载
    ///      （整包/分块），UniTask 驱动；错误约定：不抛异常，通过 DidError / OnDownloadError 表达。
    /// 两体系职责不同，但超时与错误约定统一收口于此；各常量当前数值保持不变（不影响既有行为）。
    /// </summary>
    public static class HttpDefaults
    {
        /// <summary>通用 API 请求默认超时（秒）—— Utils/Http 体系</summary>
        public const int DefaultRequestTimeoutSeconds = 15;

        /// <summary>下载元数据请求（HEAD / 普通 Get）默认超时（秒）—— HTTPHelper</summary>
        public const int DefaultMetadataTimeoutSeconds = 3;

        /// <summary>文件下载（分块单块 / 整体）默认超时（秒）—— UWRExecutor / FileDownloader</summary>
        public const int DefaultChunkTimeoutSeconds = 6;
    }
}
