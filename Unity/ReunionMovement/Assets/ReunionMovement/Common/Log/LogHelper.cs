using System;
using System.IO;
using Cysharp.Text;
using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 日志辅助器实现，负责格式化日志并输出到 Unity Console 以及可选的文件。
    ///
    /// 优化说明：
    /// - 使用 ZString 的 Utf8ValueStringBuilder（struct，池化缓冲）替代 StringBuilder，
    ///   消除 StringBuilder 本身的堆分配，实现真正的零分配日志格式化。
    /// - 统一的 Dispatch 方法消除了每个 Log/LogFormat 重载中重复的 switch(level) 分支。
    /// - 文件日志使用同步写入（短日志开销可控），避免异步带来的缓冲与排序复杂度。
    /// </summary>
    public class LogHelper : ILogHelper
    {
        // ============================================================
        //  颜色 Tag 常量（预计算，避免每次分配）
        // ============================================================
        private const string kTagDebug   = "<color=#80FF00>[调试] ";
        private const string kTagInfo    = "<color=#00FF00>[信息] ";
        private const string kTagWarning = "<color=#FFCC00>[警告] ";
        private const string kTagError   = "<color=#FF0040>[错误] ";
        private const string kTagFatal   = "<color=#FF0000>[致命] ";
        private const string kTagUnknown = "<color=#FF0040>[未知日志等级] ";
        private const string kTagClose   = "</color>";

        // ============================================================
        //  频道缩写（用于控制台显示）
        // ============================================================
        private static readonly string[] s_channelShortNames = new string[]
        {
            "",         // General
            "[Net] ",   // Network
            "[UI] ",    // UI
            "[AI] ",    // AI
            "[Audio] ", // Audio
            "[Input] ", // Input
            "[Scene] ", // Scene
            "[Res] ",   // Resource
            "[C1] ",    // Custom1
            "[C2] ",    // Custom2
            "[C3] ",    // Custom3
        };

        // ============================================================
        //  文件日志（缓存 StreamWriter，按批次 flush，避免每条日志都打开/关闭文件）
        // ============================================================
        private static string s_logFilePath;
        private static bool s_fileLogInitialized;
        // 文件日志锁：网络线程等多线程日志并发写同一文件会抛 IOException，必须串行化
        private static readonly object s_fileLogLock = new object();
        private static StreamWriter s_fileWriter;
        private static int s_pendingWrites;
        /// <summary>每 N 条日志 flush 一次（平衡同步 IO 开销与日志丢失风险）</summary>
        private const int FileLogFlushInterval = 50;

        /// <summary>
        /// 是否启用文件日志输出。
        /// </summary>
        public static bool EnableFileLog { get; set; } = true;

        /// <summary>
        /// 立即将缓冲的日志写入磁盘（应用退出/崩溃前调用，避免丢失日志）。
        /// </summary>
        public static void FlushFileLog()
        {
            lock (s_fileLogLock)
            {
                try { s_fileWriter?.Flush(); } catch { }
            }
        }

        /// <summary>
        /// 初始化文件日志路径与写入器（首次调用时自动执行）。
        /// </summary>
        private static void EnsureFileLogReady()
        {
            lock (s_fileLogLock)
            {
                if (s_fileLogInitialized) return;
                s_fileLogInitialized = true;

                // WebGL 无文件系统，直接禁用文件日志
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    EnableFileLog = false;
                    return;
                }

                try
                {
                    s_logFilePath = Path.Combine(Application.persistentDataPath,
                        $"game_log_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    s_fileWriter = new StreamWriter(s_logFilePath, append: true) { AutoFlush = false };
                }
                catch
                {
                    EnableFileLog = false;
                    s_fileWriter = null;
                }
            }
        }

        /// <summary>
        /// 写入一行到日志文件（缓存写入器 + 批量 flush）。
        /// </summary>
        private static void WriteLineToFile(string line)
        {
            if (!EnableFileLog || s_fileWriter == null) return;
            lock (s_fileLogLock)
            {
                try
                {
                    s_fileWriter.Write(line);
                    // 周期性 flush：避免每条日志同步落盘，也防止缓冲区无限增长
                    if (++s_pendingWrites >= FileLogFlushInterval)
                    {
                        s_pendingWrites = 0;
                        s_fileWriter.Flush();
                    }
                }
                catch { }
            }
        }

        // ============================================================
        //  核心方法：根据 LogLevel 获取颜色 Tag
        // ============================================================
        [HideInCallstack]
        private static string GetTag(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:   return kTagDebug;
                case LogLevel.Info:    return kTagInfo;
                case LogLevel.Warning: return kTagWarning;
                case LogLevel.Error:   return kTagError;
                case LogLevel.Fatal:   return kTagFatal;
                default:               return kTagUnknown;
            }
        }

        /// <summary>
        /// 根据 LogLevel 获取对应的 UnityEngine.Debug 输出方法并执行。
        /// 所有 Log/LogFormat 最终汇聚于此，消除散落的 switch-case。
        /// </summary>
        [HideInCallstack]
        private static void Dispatch(LogLevel level, string message, UnityEngine.Object context)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(message, context);
                    break;
                case LogLevel.Info:
                    Debug.Log(message, context);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(message, context);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message, context);
                    break;
                case LogLevel.Fatal:
                    Debug.LogError(message, context);
                    break;
                default:
                    Debug.LogError(message, context);
                    break;
            }
        }

        /// <summary>
        /// 构建带颜色 Tag 和频道前缀的完整日志字符串（ZString 零分配拼接）。
        /// </summary>
        [HideInCallstack]
        private static string BuildLogString(LogLevel level, LogChannel channel, string message)
        {
            using var sb = ZString.CreateStringBuilder();
            sb.Append(GetTag(level));

            // 频道前缀
            if (channel != LogChannel.General)
            {
                int idx = (int)channel;
                if (idx >= 0 && idx < s_channelShortNames.Length)
                    sb.Append(s_channelShortNames[idx]);
            }

            sb.Append(message);
            sb.Append(kTagClose);
            return sb.ToString();
        }

        /// <summary>
        /// 写入文件日志（纯文本，不含颜色 Tag，便于 grep）。
        /// </summary>
        private static void WriteToFile(string levelName, LogChannel channel, string message)
        {
            if (!EnableFileLog) return;
            EnsureFileLogReady();
            if (s_fileWriter == null) return;

            string channelStr = channel != LogChannel.General
                ? ZString.Format("[{0}] ", channel)
                : "";
            string line = ZString.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}{4}",
                DateTime.Now, levelName, channelStr, message, Environment.NewLine);
            WriteLineToFile(line);
        }

        // ============================================================
        //  ILogHelper 接口实现
        // ============================================================

        public void Log(LogLevel level, object message)
        {
            Log(level, message?.ToString(), null, LogChannel.General);
        }

        public void Log(LogLevel level, string message, UnityEngine.Object context)
        {
            Log(level, message, context, LogChannel.General);
        }

        public void Log(LogLevel level, object message, LogChannel channel)
        {
            Log(level, message?.ToString(), null, channel);
        }

        public void Log(LogLevel level, string message, UnityEngine.Object context, LogChannel channel)
        {
            string formatted = BuildLogString(level, channel, message);
            Dispatch(level, formatted, context);
            WriteToFile(level.ToString(), channel, message);
        }

        // ---- LogFormat 单参数 ----
        public void LogFormat(LogLevel level, string format, object arg0)
        {
            LogFormatInternal(level, LogChannel.General, format, arg0);
        }

        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0)
        {
            LogFormatInternal(level, channel, format, arg0);
        }

        // ---- LogFormat 双参数 ----
        public void LogFormat(LogLevel level, string format, object arg0, object arg1)
        {
            LogFormatInternal(level, LogChannel.General, format, arg0, arg1);
        }

        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0, object arg1)
        {
            LogFormatInternal(level, channel, format, arg0, arg1);
        }

        // ---- LogFormat 三参数 ----
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2)
        {
            LogFormatInternal(level, LogChannel.General, format, arg0, arg1, arg2);
        }

        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0, object arg1, object arg2)
        {
            LogFormatInternal(level, channel, format, arg0, arg1, arg2);
        }

        // ---- LogFormat 四参数 ----
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2, object arg3)
        {
            LogFormatInternal(level, LogChannel.General, format, arg0, arg1, arg2, arg3);
        }

        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0, object arg1, object arg2, object arg3)
        {
            LogFormatInternal(level, channel, format, arg0, arg1, arg2, arg3);
        }

        // ---- LogFormat params（5+ 参数兜底） ----
        public void LogFormat(LogLevel level, string format, params object[] args)
        {
            LogFormatInternal(level, LogChannel.General, format, args);
        }

        public void LogFormat(LogLevel level, LogChannel channel, string format, params object[] args)
        {
            LogFormatInternal(level, channel, format, args);
        }

        // ============================================================
        //  内部实现：使用 ZString Utf8ValueStringBuilder 零分配拼接
        // ============================================================
        [HideInCallstack]
        private void LogFormatInternal(LogLevel level, LogChannel channel, string format,
            object arg0)
        {
            using var sb = ZString.CreateStringBuilder();
            sb.Append(GetTag(level));
            if (channel != LogChannel.General)
            {
                int idx = (int)channel;
                if (idx >= 0 && idx < s_channelShortNames.Length)
                    sb.Append(s_channelShortNames[idx]);
            }
            sb.AppendFormat(format, arg0);
            sb.Append(kTagClose);
            string message = sb.ToString();
            Dispatch(level, message, null);
            // 写文件时用原始 format 还原纯文本（不带颜色 tag）
            WriteToFileFormatted(level, channel, format, arg0);
        }

        [HideInCallstack]
        private void LogFormatInternal(LogLevel level, LogChannel channel, string format,
            object arg0, object arg1)
        {
            using var sb = ZString.CreateStringBuilder();
            sb.Append(GetTag(level));
            if (channel != LogChannel.General)
            {
                int idx = (int)channel;
                if (idx >= 0 && idx < s_channelShortNames.Length)
                    sb.Append(s_channelShortNames[idx]);
            }
            sb.AppendFormat(format, arg0, arg1);
            sb.Append(kTagClose);
            Dispatch(level, sb.ToString(), null);
            WriteToFileFormatted(level, channel, format, arg0, arg1);
        }

        [HideInCallstack]
        private void LogFormatInternal(LogLevel level, LogChannel channel, string format,
            object arg0, object arg1, object arg2)
        {
            using var sb = ZString.CreateStringBuilder();
            sb.Append(GetTag(level));
            if (channel != LogChannel.General)
            {
                int idx = (int)channel;
                if (idx >= 0 && idx < s_channelShortNames.Length)
                    sb.Append(s_channelShortNames[idx]);
            }
            sb.AppendFormat(format, arg0, arg1, arg2);
            sb.Append(kTagClose);
            Dispatch(level, sb.ToString(), null);
            WriteToFileFormatted(level, channel, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        private void LogFormatInternal(LogLevel level, LogChannel channel, string format,
            object arg0, object arg1, object arg2, object arg3)
        {
            using var sb = ZString.CreateStringBuilder();
            sb.Append(GetTag(level));
            if (channel != LogChannel.General)
            {
                int idx = (int)channel;
                if (idx >= 0 && idx < s_channelShortNames.Length)
                    sb.Append(s_channelShortNames[idx]);
            }
            sb.AppendFormat(format, arg0, arg1, arg2, arg3);
            sb.Append(kTagClose);
            Dispatch(level, sb.ToString(), null);
            WriteToFileFormatted(level, channel, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        private void LogFormatInternal(LogLevel level, LogChannel channel, string format,
            params object[] args)
        {
            using var sb = ZString.CreateStringBuilder();
            sb.Append(GetTag(level));
            if (channel != LogChannel.General)
            {
                int idx = (int)channel;
                if (idx >= 0 && idx < s_channelShortNames.Length)
                    sb.Append(s_channelShortNames[idx]);
            }
            sb.AppendFormat(format, args);
            sb.Append(kTagClose);
            Dispatch(level, sb.ToString(), null);
            WriteToFileFormatted(level, channel, format, args);
        }

        private static void WriteToFileFormatted(LogLevel level, LogChannel channel,
            string format, object arg0)
        {
            if (!EnableFileLog) return;
            EnsureFileLogReady();
            if (s_fileWriter == null) return;
            string channelStr = channel != LogChannel.General ? ZString.Format("[{0}] ", channel) : "";
            string msg = ZString.Format(format, arg0);
            string line = ZString.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}{4}",
                DateTime.Now, level, channelStr, msg, Environment.NewLine);
            WriteLineToFile(line);
        }

        private static void WriteToFileFormatted(LogLevel level, LogChannel channel,
            string format, object arg0, object arg1)
        {
            if (!EnableFileLog) return;
            EnsureFileLogReady();
            if (s_fileWriter == null) return;
            string channelStr = channel != LogChannel.General ? ZString.Format("[{0}] ", channel) : "";
            string msg = ZString.Format(format, arg0, arg1);
            string line = ZString.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}{4}",
                DateTime.Now, level, channelStr, msg, Environment.NewLine);
            WriteLineToFile(line);
        }

        private static void WriteToFileFormatted(LogLevel level, LogChannel channel,
            string format, object arg0, object arg1, object arg2)
        {
            if (!EnableFileLog) return;
            EnsureFileLogReady();
            if (s_fileWriter == null) return;
            string channelStr = channel != LogChannel.General ? ZString.Format("[{0}] ", channel) : "";
            string msg = ZString.Format(format, arg0, arg1, arg2);
            string line = ZString.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}{4}",
                DateTime.Now, level, channelStr, msg, Environment.NewLine);
            WriteLineToFile(line);
        }

        private static void WriteToFileFormatted(LogLevel level, LogChannel channel,
            string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (!EnableFileLog) return;
            EnsureFileLogReady();
            if (s_fileWriter == null) return;
            string channelStr = channel != LogChannel.General ? ZString.Format("[{0}] ", channel) : "";
            string msg = ZString.Format(format, arg0, arg1, arg2, arg3);
            string line = ZString.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}{4}",
                DateTime.Now, level, channelStr, msg, Environment.NewLine);
            WriteLineToFile(line);
        }

        private static void WriteToFileFormatted(LogLevel level, LogChannel channel,
            string format, params object[] args)
        {
            if (!EnableFileLog) return;
            EnsureFileLogReady();
            if (s_fileWriter == null) return;
            string channelStr = channel != LogChannel.General ? ZString.Format("[{0}] ", channel) : "";
            string msg = ZString.Format(format, args);
            string line = ZString.Format("[{0:HH:mm:ss.fff}] [{1}] {2}{3}{4}",
                DateTime.Now, level, channelStr, msg, Environment.NewLine);
            WriteLineToFile(line);
        }
    }
}