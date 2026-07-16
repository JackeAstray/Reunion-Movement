using System.Diagnostics;
using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 日志工具入口，封装了日志开关检查并转发到底层日志实现。
    ///
    /// 0GC 优化说明：
    /// - Debug / Info 级别使用 [Conditional] 特性，在 Release（非 UNITY_EDITOR 且非 DEVELOPMENT_BUILD）构建中
    ///   编译器会完全移除调用点，包括调用方传入的字符串插值表达式，实现真正的零分配。
    /// - Warning / Error / Fatal 级别提供 format+args 重载，允许调用方传入常量格式字符串而非插值字符串，
    ///   避免调用侧的字符串分配。
    /// - 所有方法均标记 [HideInCallstack]，在 Console 中双击日志条目将直接跳转到原始调用位置。
    ///
    /// 频道支持：
    /// - 所有方法均提供可选的 LogChannel 参数，默认值为 LogChannel.General。
    /// - 通过 Config.Enable_Channel_XXX 可按子系统过滤日志输出。
    /// - 示例：Log.Info("连接成功", LogChannel.Network);
    /// </summary>
    public static class Log
    {
        // ============================================================
        //  内部辅助：检查日志级别 + 频道是否均开启
        // ============================================================
        [HideInCallstack]
        private static bool IsEnabled(bool levelEnabled, LogChannel channel)
        {
            return Config.Enable_LOG && levelEnabled && Config.IsChannelEnabled(channel);
        }

        // ============================================================
        //  Debug — Release 构建中完全剔除（0GC）
        // ============================================================

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(object message, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, channel))
                GameLogger.Debug(message, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, channel))
                GameLogger.Debug(message, context, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, channel))
                GameLogger.Debug(format, arg0, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, channel))
                GameLogger.Debug(format, arg0, arg1, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, channel))
                GameLogger.Debug(format, arg0, arg1, arg2, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, channel))
                GameLogger.Debug(format, arg0, arg1, arg2, arg3, channel);
        }

        /// <summary>
        /// 5+ 参数兜底重载（使用 params object[]，会产生数组分配）。
        /// 默认使用 General 频道；如需指定频道请使用其他重载。
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, params object[] args)
        {
            if (IsEnabled(Config.Enable_Debug_LOG, LogChannel.General))
                GameLogger.Debug(format, args);
        }

        // ============================================================
        //  Info — Release 构建中完全剔除（0GC）
        // ============================================================

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(object message, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Info_LOG, channel))
                GameLogger.Info(message, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Info_LOG, channel))
                GameLogger.Info(message, context, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Info_LOG, channel))
                GameLogger.Info(format, arg0, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Info_LOG, channel))
                GameLogger.Info(format, arg0, arg1, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Info_LOG, channel))
                GameLogger.Info(format, arg0, arg1, arg2, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Info_LOG, channel))
                GameLogger.Info(format, arg0, arg1, arg2, arg3, channel);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, params object[] args)
        {
            if (IsEnabled(Config.Enable_Info_LOG, LogChannel.General))
                GameLogger.Info(format, args);
        }

        // ============================================================
        //  Warning — 始终开启，提供 format 重载降低分配
        // ============================================================

        [HideInCallstack]
        public static void Warning(object message, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, channel))
                GameLogger.Warning(message, channel);
        }

        [HideInCallstack]
        public static void Warning(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, channel))
                GameLogger.Warning(message, context, channel);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, channel))
                GameLogger.Warning(format, arg0, channel);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, channel))
                GameLogger.Warning(format, arg0, arg1, channel);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, channel))
                GameLogger.Warning(format, arg0, arg1, arg2, channel);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, channel))
                GameLogger.Warning(format, arg0, arg1, arg2, arg3, channel);
        }

        [HideInCallstack]
        public static void Warning(string format, params object[] args)
        {
            if (IsEnabled(Config.Enable_Warning_LOG, LogChannel.General))
                GameLogger.Warning(format, args);
        }

        // ============================================================
        //  Error — 始终开启，提供 format 重载降低分配
        // ============================================================

        [HideInCallstack]
        public static void Error(object message, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Error_LOG, channel))
                GameLogger.Error(message, channel);
        }

        [HideInCallstack]
        public static void Error(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Error_LOG, channel))
                GameLogger.Error(message, context, channel);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Error_LOG, channel))
                GameLogger.Error(format, arg0, channel);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Error_LOG, channel))
                GameLogger.Error(format, arg0, arg1, channel);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Error_LOG, channel))
                GameLogger.Error(format, arg0, arg1, arg2, channel);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Error_LOG, channel))
                GameLogger.Error(format, arg0, arg1, arg2, arg3, channel);
        }

        [HideInCallstack]
        public static void Error(string format, params object[] args)
        {
            if (IsEnabled(Config.Enable_Error_LOG, LogChannel.General))
                GameLogger.Error(format, args);
        }

        // ============================================================
        //  Fatal — 始终开启，提供 format 重载降低分配
        // ============================================================

        [HideInCallstack]
        public static void Fatal(object message, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, channel))
                GameLogger.Fatal(message, channel);
        }

        [HideInCallstack]
        public static void Fatal(string message, Object context, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, channel))
                GameLogger.Fatal(message, context, channel);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, channel))
                GameLogger.Fatal(format, arg0, channel);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, channel))
                GameLogger.Fatal(format, arg0, arg1, channel);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, channel))
                GameLogger.Fatal(format, arg0, arg1, arg2, channel);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2, object arg3, LogChannel channel = LogChannel.General)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, channel))
                GameLogger.Fatal(format, arg0, arg1, arg2, arg3, channel);
        }

        [HideInCallstack]
        public static void Fatal(string format, params object[] args)
        {
            if (IsEnabled(Config.Enable_Fatal_LOG, LogChannel.General))
                GameLogger.Fatal(format, args);
        }
    }
}