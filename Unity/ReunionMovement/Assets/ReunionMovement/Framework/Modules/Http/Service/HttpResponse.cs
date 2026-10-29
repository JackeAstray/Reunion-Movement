using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util.HttpService
{
    /// <summary>
    /// 响应
    /// </summary>
    public class HttpResponse
    {
        public string url { get; set; }
        public bool isSuccessful { get; set; }
        public bool isHttpError { get; set; }
        public bool isNetworkError { get; set; }
        public long statusCode { get; set; }
        public byte[] bytes { get; set; }
        public string text { get; set; }
        public string error { get; set; }
        public Texture2D texture { get; set; }
        public Dictionary<string, string> responseHeaders { get; set; }
    }
}
