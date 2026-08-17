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

        /// <summary>本地日志文件最大字节数（超过后轮转到 .prev）</summary>
        public const long MaxLogFileBytes = 1 << 20; // 1MB

        /// <summary>同类错误连续出现 N 次后，向文件补写一条聚合标记行</summary>
        private const int AggregateFlushInterval = 10;

        /// <summary>最近错误缓冲（线程安全）</summary>
        private static readonly List<string> recentErrors = new List<string>(MaxBufferedEntries);
        private static readonly object syncRoot = new object();

        // ===== 同类错误聚合（错误风暴防护）=====
        // 同一 logString+stackTrace 连续出现时：内存缓冲只保留一条带 [×N] 计数的条目，
        // 文件仅首次写入 + 每 N 次补写聚合标记行，避免同错误刷屏时主线程反复同步 IO
        private static string lastLogKey;      // 上一条错误的 logString + 首行堆栈（去重键）
        private static int lastLogCount;       // 上一条错误连续次数
        private static string lastLogEntryBase; // 上一条错误的完整条目（含时间戳/类型，聚合时更新计数）

        /// <summary>错误上报事件（订阅者可自行处理：弹窗 / 额外上报渠道）</summary>
        public static event Action<string> ErrorReported;

        /// <summary>派发重入防护：订阅者内部再 LogError 时直接写缓冲，避免无限递归</summary>
        private static bool isDispatching = false;

        /// <summary>是否已订阅全局日志回调（与 IsInitialized 分离：Initialize 早退不得阻断订阅恢复）</summary>
        private static bool isSubscribed = false;

        /// <summary>上报载荷（POST JSON）</summary>
        [Serializable]
        private class ErrorReportPayload
        {
            public string log;
            public string device;
            public string appVersion;
            public string platform;
        }

        /// <summary>
        /// 跨 Play 会话复位：关闭 Domain Reload 时静态字段不会自动重置，
        /// 上一会话的 recentErrors 会被带到下一会话并误上传；同时重置订阅标记，
        /// 保证每次会话重新订阅（防御式重订阅幂等且无害）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewSession()
        {
            lock (syncRoot)
            {
                recentErrors.Clear();
                // 跨会话清空聚合状态：否则下一会话首条错误若恰与上会话末条相同会被误判为重复
                lastLogKey = null;
                lastLogCount = 0;
                lastLogEntryBase = null;
            }
            IsInitialized = false;
            isSubscribed = false;
            isDispatching = false;
        }

        /// <summary>初始化（幂等）：订阅全局日志回调并准备日志目录</summary>
        public static void Initialize()
        {
            // 订阅恢复必须放在早退判断之前：域重载关闭时静态 IsInitialized 跨会话保持，
            // 若订阅因任何原因失效，Initialize 早退后将永远无法重新订阅（原实现中 -= / += 是死代码）
            if (!isSubscribed)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                Application.logMessageReceived += OnLogMessageReceived;
                isSubscribed = true;
            }

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
                Log.Warning($"[ErrorReporter] 创建日志目录失败: {ex.Message}");
            }

            Log.Info("[ErrorReporter] 全局错误捕获已启用");
        }

        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            // 只捕获错误级，避免 Warning/Log 噪音
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            // 取首行堆栈参与去重键：堆栈尾部地址在不同平台/构建下抖动，首行已足够区分来源
            string stackHead = null;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int nl = stackTrace.IndexOf('\n');
                stackHead = nl > 0 ? stackTrace.Substring(0, nl) : stackTrace;
            }
            string key = (logString ?? string.Empty) + "\n" + (stackHead ?? string.Empty);

            var sb = new StringBuilder(256);
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] [")
              .Append(type).Append("] ").AppendLine(logString);
            if (!string.IsNullOrEmpty(stackTrace))
            {
                sb.AppendLine(stackTrace);
            }
            string entry = sb.ToString();

            bool aggregated = false;
            lock (syncRoot)
            {
                if (key == lastLogKey && recentErrors.Count > 0)
                {
                    // 同类错误：更新最后一条缓冲为带计数版本，避免环形缓冲被同错误填满
                    lastLogCount++;
                    string aggregatedEntry = lastLogEntryBase + string.Format("[×{0} 重复]\n", lastLogCount);
                    recentErrors[recentErrors.Count - 1] = aggregatedEntry;
                    aggregated = true;
                }
                else
                {
                    lastLogKey = key;
                    lastLogCount = 1;
                    lastLogEntryBase = entry;
                    recentErrors.Add(entry);
                    if (recentErrors.Count > MaxBufferedEntries)
                    {
                        recentErrors.RemoveAt(0);
                    }
                }
            }

            // 追加写入本地文件（崩溃前尽量落盘）。同类错误只首次写全文，
            // 之后每 AggregateFlushInterval 次写一条聚合标记行（轮转限制总大小）
            if (!aggregated)
            {
                AppendToFile(entry);
            }
            else if (lastLogCount % AggregateFlushInterval == 0)
            {
                AppendToFile(string.Format("[{0}] [{1}] 上述错误已重复 {2} 次\n",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), type, lastLogCount));
            }

            // 重入防护：订阅者内部再触发错误日志时直接返回，防止 logMessageReceived 无限递归
            if (isDispatching) return;
            isDispatching = true;
            try
            {
                try
                {
                    ErrorReported?.Invoke(entry);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ErrorReporter] 上报订阅者异常: {ex.Message}");
                }
            }
            finally
            {
                isDispatching = false;
            }
        }

        /// <summary>追加写入本地文件，超过 MaxLogFileBytes 时轮转（error_log.txt → .prev）</summary>
        private static void AppendToFile(string text)
        {
            try
            {
                var fi = new FileInfo(LogFilePath);
                if (fi.Exists && fi.Length >= MaxLogFileBytes)
                {
                    string prevPath = LogFilePath + ".prev";
                    try { if (File.Exists(prevPath)) File.Delete(prevPath); } catch { }
                    try { File.Move(LogFilePath, prevPath); } catch { /* 移动失败则直接继续追加 */ }
                }
                File.AppendAllText(LogFilePath, text);
            }
            catch (Exception ex)
            {
                Log.Warning($"[ErrorReporter] 写入日志文件失败: {ex.Message}");
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
                // 与文档一致：未配置 URL 视为“无需上报”，回调 true，
                // 与“暂无错误”分支语义统一，调用方不得把未配置误判为上传失败
                onComplete?.Invoke(true);
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
                Log.Warning($"[ErrorReporter] 上传失败: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }
    }
}
