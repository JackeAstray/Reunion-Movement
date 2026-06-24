using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 日志工具入口，封装了日志开关检查并转发到底层日志实现
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 输出调试日志（仅在调试级别开启时输出）
        /// </summary>
        /// <param name="message">要输出的消息</param>
        public static void Debug(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
            {
                GameLogger.Debug(message);
            }
        }

        /// <summary>
        /// 输出调试日志（泛型版本，便于调用方直接传入任意类型）
        /// </summary>
        public static void Debug<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Debug_LOG)
            {
                GameLogger.Debug(message);
            }
        }

        /// <summary>
        /// 输出信息日志（仅在信息级别开启时输出）
        /// </summary>
        public static void Info(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
            {
                GameLogger.Info(message);
            }
        }

        /// <summary>
        /// 输出信息日志（泛型版本，便于调用方直接传入任意类型）
        /// </summary>
        public static void Info<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Info_LOG)
            {
                GameLogger.Info(message);
            }
        }

        /// <summary>
        /// 输出警告日志（仅在警告级别开启时输出）
        /// </summary>
        public static void Warning(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
            {
                GameLogger.Warning(message);
            }
        }

        /// <summary>
        /// 输出警告日志（泛型版本，便于调用方直接传入任意类型）
        /// </summary>
        public static void Warning<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Warning_LOG)
            {
                GameLogger.Warning(message);
            }
        }

        /// <summary>
        /// 输出错误日志（仅在错误级别开启时输出）
        /// </summary>
        public static void Error(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
            {
                GameLogger.Error(message);
            }
        }

        /// <summary>
        /// 输出错误日志（泛型版本，便于调用方直接传入任意类型）
        /// </summary>
        public static void Error<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Error_LOG)
            {
                GameLogger.Error(message);
            }
        }

        /// <summary>
        /// 输出致命错误日志（仅在致命级别开启时输出）
        /// </summary>
        public static void Fatal(object message)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
            {
                GameLogger.Fatal(message);
            }
        }

        /// <summary>
        /// 输出致命错误日志（泛型版本，避免值类型装箱）
        /// </summary>
        public static void Fatal<T>(T message)
        {
            if (Config.Enable_LOG && Config.Enable_Fatal_LOG)
            {
                GameLogger.Fatal(message);
            }
        }
    }
}