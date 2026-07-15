using System;
using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 日志辅助器接口。
    /// </summary>
    public interface ILogHelper
    {
        /// <summary>
        /// 记录日志。
        /// </summary>
        public void Log(LogLevel level, object message);

        /// <summary>
        /// 记录日志（带 Unity Object 上下文，Console 中点击可定位到具体 GameObject）。
        /// </summary>
        public void Log(LogLevel level, string message, UnityEngine.Object context);

        /// <summary>
        /// 记录日志（指定频道）。
        /// </summary>
        public void Log(LogLevel level, object message, LogChannel channel)
        {
            Log(level, message);
        }

        /// <summary>
        /// 记录日志（指定频道 + Unity Object 上下文）。
        /// </summary>
        public void Log(LogLevel level, string message, UnityEngine.Object context, LogChannel channel)
        {
            Log(level, message, context);
        }

        /// <summary>
        /// 记录格式化日志（单参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0)
        {
            Log(level, string.Format(format, arg0));
        }

        /// <summary>
        /// 记录格式化日志（单参数 + 频道）。
        /// </summary>
        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0)
        {
            Log(level, string.Format(format, arg0), channel);
        }

        /// <summary>
        /// 记录格式化日志（双参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1)
        {
            Log(level, string.Format(format, arg0, arg1));
        }

        /// <summary>
        /// 记录格式化日志（双参数 + 频道）。
        /// </summary>
        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0, object arg1)
        {
            Log(level, string.Format(format, arg0, arg1), channel);
        }

        /// <summary>
        /// 记录格式化日志（三参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2)
        {
            Log(level, string.Format(format, arg0, arg1, arg2));
        }

        /// <summary>
        /// 记录格式化日志（三参数 + 频道）。
        /// </summary>
        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0, object arg1, object arg2)
        {
            Log(level, string.Format(format, arg0, arg1, arg2), channel);
        }

        /// <summary>
        /// 记录格式化日志（四参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2, object arg3)
        {
            Log(level, string.Format(format, arg0, arg1, arg2, arg3));
        }

        /// <summary>
        /// 记录格式化日志（四参数 + 频道）。
        /// </summary>
        public void LogFormat(LogLevel level, LogChannel channel, string format, object arg0, object arg1, object arg2, object arg3)
        {
            Log(level, string.Format(format, arg0, arg1, arg2, arg3), channel);
        }

        /// <summary>
        /// 记录格式化日志（5+ 参数兜底）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, params object[] args)
        {
            Log(level, string.Format(format, args));
        }

        /// <summary>
        /// 记录格式化日志（5+ 参数兜底 + 频道）。
        /// </summary>
        public void LogFormat(LogLevel level, LogChannel channel, string format, params object[] args)
        {
            Log(level, string.Format(format, args), channel);
        }
    }
}