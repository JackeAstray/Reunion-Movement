using UnityEngine;

namespace ReunionMovement.Common
{
    public static partial class GameLogger
    {
        private static ILogHelper logHelper = new LogHelper();

        /// <summary>
        /// 设置游戏框架日志辅助器。
        /// </summary>
        public static void SetLogHelper(ILogHelper helper)
        {
            logHelper = helper;
        }

        // ============================================================
        //  Debug
        // ============================================================
        [HideInCallstack]
        public static void Debug(object message, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Debug, message, channel);
        }

        [HideInCallstack]
        public static void Debug(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Debug, message, context, channel);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, channel, format, arg0);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, channel, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, channel, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, channel, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Debug(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Debug, LogChannel.General, format, args);
        }

        // ============================================================
        //  Info
        // ============================================================
        [HideInCallstack]
        public static void Info(object message, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Info, message, channel);
        }

        [HideInCallstack]
        public static void Info(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Info, message, context, channel);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, channel, format, arg0);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, channel, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, channel, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, channel, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Info(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Info, LogChannel.General, format, args);
        }

        // ============================================================
        //  Warning
        // ============================================================
        [HideInCallstack]
        public static void Warning(object message, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Warning, message, channel);
        }

        [HideInCallstack]
        public static void Warning(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Warning, message, context, channel);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, channel, format, arg0);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, channel, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, channel, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, channel, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Warning(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Warning, LogChannel.General, format, args);
        }

        // ============================================================
        //  Error
        // ============================================================
        [HideInCallstack]
        public static void Error(object message, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Error, message, channel);
        }

        [HideInCallstack]
        public static void Error(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Error, message, context, channel);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, channel, format, arg0);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, channel, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, channel, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, channel, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Error(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Error, LogChannel.General, format, args);
        }

        // ============================================================
        //  Fatal
        // ============================================================
        [HideInCallstack]
        public static void Fatal(object message, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Fatal, message, channel);
        }

        [HideInCallstack]
        public static void Fatal(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.Log(LogLevel.Fatal, message, context, channel);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, channel, format, arg0);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, channel, format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, channel, format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, channel, format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Fatal(string format, params object[] args)
        {
            if (logHelper == null) return;
            logHelper.LogFormat(LogLevel.Fatal, LogChannel.General, format, args);
        }
    }
}
