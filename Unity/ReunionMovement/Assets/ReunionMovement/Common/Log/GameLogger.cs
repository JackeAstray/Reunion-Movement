using UnityEngine;

namespace ReunionMovement.Common
{
    public static partial class GameLogger
    {
        private static ILogHelper logHelper = new LogHelper();

        /// <summary>
        /// 设置游戏框架日志辅助器。
        /// </summary>
        /// <param name="logHelper">要设置的游戏框架日志辅助器。</param>
        public static void SetLogHelper(ILogHelper helper)
        {
            logHelper = helper;
        }

        /// <summary>
        /// 打印调试级别日志，用于记录调试类日志信息。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [HideInCallstack]
        public static void Debug(object message)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Debug, message);
        }

        [HideInCallstack]
        public static void Debug(string message, Object context)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Debug, message, context);
        }

        /// <summary>
        /// 打印调试级别格式化日志，format 可使用常量字符串避免 GC 分配。
        /// </summary>
        [HideInCallstack]
        public static void Debug(string format, object arg0)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, format, arg0);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Debug(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, format, args);
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [HideInCallstack]
        public static void Info(object message)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Info, message);
        }

        [HideInCallstack]
        public static void Info(string message, Object context)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Info, message, context);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, format, arg0);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Info(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, format, args);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [HideInCallstack]
        public static void Warning(object message)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Warning, message);
        }

        [HideInCallstack]
        public static void Warning(string message, Object context)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Warning, message, context);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, format, arg0);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Warning(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, format, args);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [HideInCallstack]
        public static void Error(object message)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Error, message);
        }

        [HideInCallstack]
        public static void Error(string message, Object context)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Error, message, context);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, format, arg0);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Error(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, format, args);
        }

        /// <summary>
        /// 打印严重错误级别日志，建议在发生严重错误，可能导致游戏崩溃或异常时使用，此时应尝试重启进程或重建游戏框架。
        /// </summary>
        /// <param name="message">日志内容。</param>
        [HideInCallstack]
        public static void Fatal(object message)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Fatal, message);
        }

        [HideInCallstack]
        public static void Fatal(string message, Object context)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Fatal, message, context);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, format, arg0);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Fatal(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, format, args);
        }
    }
}
