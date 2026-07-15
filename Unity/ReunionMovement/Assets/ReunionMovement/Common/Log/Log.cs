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
    /// </summary>
    public static class Log
    {
        // ============================================================
        //  Debug — Release 构建中完全剔除（0GC）
        // ============================================================

        /// <summary>
        /// 输出调试日志（仅在调试级别开启时输出）。
        /// Release 构建中本方法及调用方的字符串插值会被编译器完全移除。
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string message, Object context)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(message, context);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(format, arg0);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(format, arg0, arg1);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(format, arg0, arg1, arg2);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(format, arg0, arg1, arg2, arg3);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Debug(string format, params object[] args)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
                GameLogger.Debug(format, args);
        }

        // ============================================================
        //  Info — Release 构建中完全剔除（0GC）
        // ============================================================

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string message, Object context)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(message, context);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(format, arg0);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(format, arg0, arg1);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(format, arg0, arg1, arg2);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(format, arg0, arg1, arg2, arg3);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Info(string format, params object[] args)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
                GameLogger.Info(format, args);
        }

        // ============================================================
        //  Warning — 始终开启，提供 format 重载降低分配
        // ============================================================

        [HideInCallstack]
        public static void Warning(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(message);
        }

        [HideInCallstack]
        public static void Warning(string message, Object context)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(message, context);
        }

        [HideInCallstack]
        public static void Warning<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(message);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(format, arg0);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Warning(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Warning(string format, params object[] args)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
                GameLogger.Warning(format, args);
        }

        // ============================================================
        //  Error — 始终开启，提供 format 重载降低分配
        // ============================================================

        [HideInCallstack]
        public static void Error(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(message);
        }

        [HideInCallstack]
        public static void Error(string message, Object context)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(message, context);
        }

        [HideInCallstack]
        public static void Error<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(message);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(format, arg0);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Error(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Error(string format, params object[] args)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
                GameLogger.Error(format, args);
        }

        // ============================================================
        //  Fatal — 始终开启，提供 format 重载降低分配
        // ============================================================

        [HideInCallstack]
        public static void Fatal(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(message);
        }

        [HideInCallstack]
        public static void Fatal(string message, Object context)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(message, context);
        }

        [HideInCallstack]
        public static void Fatal<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(message);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(format, arg0);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(format, arg0, arg1);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(format, arg0, arg1, arg2);
        }

        [HideInCallstack]
        public static void Fatal(string format, object arg0, object arg1, object arg2, object arg3)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(format, arg0, arg1, arg2, arg3);
        }

        [HideInCallstack]
        public static void Fatal(string format, params object[] args)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
                GameLogger.Fatal(format, args);
        }
    }
}