using ReunionMovement.Common.Util.HttpService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 全局错误捕获与上报 —— 监听 Application.logMessageReceived，
    /// 捕获 Error/Exception/Assert 写入本地文件并保留最近 N 条内存缓冲，可选通过 HttpMgr 上传。
    /// 用法：ErrorReporter.Initialize()（推荐在 Bootstrap 启动早期调用一次，幂等）。
    /// 上报由调用方在合适时机触发（如网络就绪后调用 UploadErrorLog）。
    /// </summary>
    public static class ErrorReporter
    {
        /// <summary>是否已初始化（防重复订阅）</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>错误日志文件路径（persistentDataPath/Logs/error_log.txt）</summary>
        public static string LogFilePath { get; private set; }

        /// <summary>上报接口地址（null/空表示不上传；可在初始化后配置）</summary>
        public static string UploadUrl { get; set; }

        /// <summary>内存缓冲上限</summary>
        public const int MaxBufferedEntries = 50;

        /// <summary>最近错误缓冲（线程安全）</summary>
        private static readonly List<string> recentErrors = new List<string>(MaxBufferedEntries);
        private static readonly object syncRoot = new object();

        /// <summary>错误上报事件（订阅者可自行处理：弹窗 / 额外上报渠道）</summary>
        public static event Action<string> ErrorReported;

        /// <summary>上报载荷（POST JSON）</summary>
        [Serializable]
        private class ErrorReportPayload
        {
            public string log;
            public string device;
            public string appVersion;
            public string platform;
        }

        /// <summary>初始化（幂等）：订阅全局日志回调并准备日志目录</summary>
        public static void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            LogFilePath = Path.Combine(Application.persistentDataPath, "Logs", "error_log.txt");
            try
            {
                var dir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ErrorReporter] 创建日志目录失败: {ex.Message}");
            }

            Application.logMessageReceived += OnLogMessageReceived;
            Debug.Log("[ErrorReporter] 全局错误捕获已启用");
        }

        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            // 只捕获错误级，避免 Warning/Log 噪音
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            var sb = new StringBuilder(256);
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] [")
              .Append(type).Append("] ").AppendLine(logString);
            if (!string.IsNullOrEmpty(stackTrace))
            {
                sb.AppendLine(stackTrace);
            }
            string entry = sb.ToString();

            // 内存缓冲（环形，线程安全）
            lock (syncRoot)
            {
                recentErrors.Add(entry);
                if (recentErrors.Count > MaxBufferedEntries)
                {
                    recentErrors.RemoveAt(0);
                }
            }

            // 追加写入本地文件（崩溃前尽量落盘）
            try
            {
                File.AppendAllText(LogFilePath, entry);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ErrorReporter] 写入日志文件失败: {ex.Message}");
            }

            try
            {
                ErrorReported?.Invoke(entry);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ErrorReporter] 上报订阅者异常: {ex.Message}");
            }
        }

        /// <summary>获取最近错误副本（线程安全）</summary>
        public static IReadOnlyList<string> GetRecentErrors()
        {
            lock (syncRoot)
            {
                return recentErrors.ToArray();
            }
        }

        /// <summary>
        /// 上传错误日志到 UploadUrl（POST JSON，body 含 log/device/appVersion/platform）。
        /// 未配置 UploadUrl 或暂无错误时直接回调 true（无需上报）。
        /// </summary>
        public static void UploadErrorLog(Action<bool> onComplete = null)
        {
            if (string.IsNullOrEmpty(UploadUrl))
            {
                onComplete?.Invoke(false);
                return;
            }

            string log;
            lock (syncRoot)
            {
                log = string.Join("\n", recentErrors);
            }
            if (string.IsNullOrEmpty(log))
            {
                onComplete?.Invoke(true);
                return;
            }

            var payload = new ErrorReportPayload
            {
                log = log,
                device = SystemInfo.deviceModel,
                appVersion = Application.version,
                platform = Application.platform.ToString(),
            };

            try
            {
                HttpMgr.PostJson(UploadUrl, payload)
                    .OnSuccess(_ => onComplete?.Invoke(true))
                    .OnError(_ => onComplete?.Invoke(false))
                    .OnNetworkError(_ => onComplete?.Invoke(false))
                    .Send();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ErrorReporter] 上传失败: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }
    }
}
