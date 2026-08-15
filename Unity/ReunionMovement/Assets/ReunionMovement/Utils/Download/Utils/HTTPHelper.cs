using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// HTTP响应类
    /// </summary>
    public class HTTPResponse
    {
        public string responseText;
        public int responseCode;
        public int downloadedBytes = 0;
        public Dictionary<string, string> headers;
        public bool didError;

        public override string ToString()
        {
            return $"ResponseText={responseText}, ResponseCode={responseCode}, DidError={didError}";
        }
    }

    /// <summary>
    /// HTTP请求帮助类 —— 文件下载体系（Utils/Download）的底层请求封装。
    /// 与通用业务请求体系（Utils/Http 的 HttpMgr/UnityHttpService）分工不同，超时与错误约定统一收口于
    /// ReunionMovement.Common.Util.HttpDefaults。错误约定：不抛异常，通过 didError 标记表达。
    /// </summary>
    public static class HTTPHelper
    {
        /// <summary>
        /// 发送GET请求
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="headers"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public static async UniTask<HTTPResponse> Get(string uri, Dictionary<string, string> headers = null, int timeoutSeconds = HttpDefaults.DefaultMetadataTimeoutSeconds)
        {
            var resp = new HTTPResponse();

            using (var req = UnityWebRequest.Get(uri))
            {
                req.timeout = timeoutSeconds;
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        req.SetRequestHeader(kvp.Key, kvp.Value);
                    }
                }

                // 注意：内置 ToUniTask 在错误时抛 UnityWebRequestException，这里按原契约吞掉异常，
                // 由下方 req.result / req.error 统一收集错误信息（保持“错误不抛出、didError 标记”语义）。
                try
                {
                    await req.SendWebRequest();
                }
                catch (UnityWebRequestException)
                {
                }

                resp.responseText = req.result == UnityWebRequest.Result.Success
                    ? req.downloadHandler.text
                    : req.error;
                resp.didError = req.result != UnityWebRequest.Result.Success;
                resp.responseCode = (int)req.responseCode;
                resp.headers = req.GetResponseHeaders();
            }
            return resp;
        }

        /// <summary>
        /// 从Uri获取相对路径（已做路径遍历防护）。
        /// 解码后按 / 与 \ 双分隔符逐段过滤：丢弃空段、"."、".."、含盘符（冒号）的段，
        /// 防止 "%2e%2e" 编码绕过与 Windows 盘符段（Path.Combine 遇带盘符第二参数会直接返回该参数）。
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static string GetRelativePathFromUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return string.Empty;
            }

            try
            {
                var u = new Uri(uri);
                // 去掉开头的斜杠；先解码让编码的穿越段（%2e%2e）暴露，再逐段过滤。
                // AbsolutePath 不含 query/fragment，无需额外剥离。
                string decoded = Uri.UnescapeDataString(u.AbsolutePath.TrimStart('/'));
                // 同时按 / 与 \ 分段：Windows 下反斜杠同样是目录分隔符，
                // "x%3A%5C..%5C.." 解码后是单段 "x:\..\.."，不拆段就无法拦截其中的穿越。
                var segments = decoded.Split('/', '\\');
                var safeSegments = new List<string>(segments.Length);
                foreach (var seg in segments)
                {
                    if (string.IsNullOrEmpty(seg) || seg == ".") continue;
                    if (seg == "..") continue; // 丢弃目录穿越段
                    // 拒绝含冒号的段：Windows 盘符（如 "x:"）经 Path.Combine 会直接覆盖下载目录前缀；
                    // 同时拦截 UNC 主机名与 ADS 流（file.txt:stream）
                    if (seg.IndexOf(':') >= 0) continue;
                    safeSegments.Add(seg);
                }
                return string.Join("/", safeSegments);
            }
            catch
            {
                return FileOperationUtil.GetFileName(uri);
            }
        }

        /// <summary>
        /// 从Uri获取文件名（已做路径遍历防护）。
        /// 使用 Path.GetFileName 剥离目录分隔符，防止 "../../../etc/passwd" 类攻击。
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static string GetFilenameFromUriNaively(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return string.Empty;
            }

            var arr = uri.Split('/');
            var v = arr[^1];
            // 剥离 query/fragment：带签名参数的 URL（file.bin?token=x）若不去掉 "?token=x"，
            // 会生成非法 Windows 文件名导致 DownloadHandlerFile 写入失败
            int queryIdx = v.IndexOf('?');
            if (queryIdx >= 0) v = v.Substring(0, queryIdx);
            int fragIdx = v.IndexOf('#');
            if (fragIdx >= 0) v = v.Substring(0, fragIdx);
            // 注意：不要对含 % 的文件名做 Split('%') 截断 —— 编码文件名（如 report%20final.png）
            // 会被错误截成 "20 final.png"。% 解码已由下方的 Uri.UnescapeDataString 处理。

            // 路径遍历防护：使用 Path.GetFileName 剥离任何目录穿越字符
            // 先做 URL 解码以防编码绕过（如 %2e%2e%2f）
            try
            {
                string decoded = Uri.UnescapeDataString(v);
                string safe = Path.GetFileName(decoded);
                if (!string.IsNullOrEmpty(safe))
                    v = safe;
            }
            catch
            {
                // 解码失败时使用原始值，由 Path.GetFileName 做最终净化
                v = Path.GetFileName(v);
            }

            // 过滤空文件名和纯扩展名（如 ".bashrc"）
            if (string.IsNullOrEmpty(v) || v.StartsWith(".") && v.Length < 3)
                v = "download.dat";

            return v;
        }

        /// <summary>
        /// 发送HEAD请求
        /// </summary>
        /// <param name="req"></param>
        /// <param name="uri"></param>
        /// <param name="headers"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public static UnityWebRequestAsyncOperation Head(ref UnityWebRequest req, string uri, Dictionary<string, string> headers = null, int timeoutSeconds = HttpDefaults.DefaultMetadataTimeoutSeconds)
        {
            req = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbHEAD)
            {
                timeout = timeoutSeconds
            };

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    req.SetRequestHeader(kvp.Key, kvp.Value);
                }
            }

            Log.Debug("Head URI={0}", uri);
            if (headers != null)
            {
                foreach (var str in headers)
                {
                    Log.Debug("[{0}={1}]", str.Key, str.Value);
                }
            }

            return req.SendWebRequest();
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="req"></param>
        /// <param name="uri"></param>
        /// <param name="path"></param>
        /// <param name="abandonOnFailure"></param>
        /// <param name="append"></param>
        /// <param name="headers"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public static UnityWebRequestAsyncOperation Download(
            ref UnityWebRequest req,
            string uri,
            string path = null,
            bool isMd5Name = false,
            bool downloadToRoot = false,
            bool abandonOnFailure = false,
            bool append = false,
            Dictionary<string, string> headers = null,
            int timeoutSeconds = HttpDefaults.DefaultMetadataTimeoutSeconds
        )
        {
            path ??= Application.persistentDataPath;

            if (headers != null)
            {
                foreach (var str in headers)
                {
                    Log.Debug("[{0}={1}]", str.Key, str.Value);
                }
            }

            req = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET);

            string filename = "";

            if (isMd5Name)
            {
                filename = PathUtil.GetFileNameByUrl(uri);
            }
            else
            {
                filename = GetFilenameFromUriNaively(uri);
            }

            string tempPath;

            if (downloadToRoot)
            {
                // filename 已由 GetFilenameFromUriNaively（URL 解码）或 PathUtil.GetFileNameByUrl（MD5）处理，
                // 不要再对路径做 Split('%') 截断，否则会破坏编码文件名与目录路径
                tempPath = Path.Combine(path, filename).Replace("/", Path.DirectorySeparatorChar.ToString());
            }
            else
            {
                string relativePath = GetRelativePathFromUri(uri);
                tempPath = Path.Combine(path, relativePath).Replace("/", Path.DirectorySeparatorChar.ToString());
            }

            req.downloadHandler = new DownloadHandlerFile(tempPath, append)
            {
                removeFileOnAbort = abandonOnFailure
            };

            req.timeout = timeoutSeconds;

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    req.SetRequestHeader(kvp.Key, kvp.Value);
                }
            }
            return req.SendWebRequest();
        }
    }
}