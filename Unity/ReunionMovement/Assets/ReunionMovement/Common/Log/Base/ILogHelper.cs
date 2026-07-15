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
        /// <param name="level">游戏框架日志等级。</param>
        /// <param name="message">日志内容。</param>
        public void Log(LogLevel level, object message);

        /// <summary>
        /// 记录日志（带 Unity Object 上下文，Console 中点击可定位到具体 GameObject）。
        /// 默认实现忽略上下文，具体实现类可覆写以调用 Debug.Log(message, context)。
        /// </summary>
        public void Log(LogLevel level, string message, UnityEngine.Object context)
        {
            Log(level, message);
        }

        /// <summary>
        /// 记录格式化日志（单参数，避免调用方字符串插值带来的 GC 分配）。
        /// 默认实现回退到 string.Format + Log，具体实现类可覆写以使用 Debug.LogFormat 优化。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0)
        {
            Log(level, string.Format(format, arg0));
        }

        /// <summary>
        /// 记录格式化日志（双参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1)
        {
            Log(level, string.Format(format, arg0, arg1));
        }

        /// <summary>
        /// 记录格式化日志（三参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2)
        {
            Log(level, string.Format(format, arg0, arg1, arg2));
        }

        /// <summary>
        /// 记录格式化日志（四参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2, object arg3)
        {
            Log(level, string.Format(format, arg0, arg1, arg2, arg3));
        }

        /// <summary>
        /// 记录格式化日志（5+ 参数兜底重载，使用 params object[]）。
        /// 0~4 参数请优先使用上面的非 params 重载以避免数组分配。
        /// </summary>
        public void LogFormat(LogLevel level, string format, params object[] args)
        {
            Log(level, string.Format(format, args));
        }
    }
}