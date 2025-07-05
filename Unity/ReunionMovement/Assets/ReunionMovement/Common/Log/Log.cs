using UnityEngine;

namespace ReunionMovement.Common
{
    public static class Log
    {
        /// <summary>
        /// 检查日志是否启用
        /// </summary>
        /// <param name="levelSwitch"></param>
        /// <returns></returns>
        private static bool IsLogEnabled(bool levelSwitch)
        {
            return Config.Enable_LOG && levelSwitch;
        }

        public static void Debug(object message)
        {
            if (IsLogEnabled(Config.Enable_Debug_LOG))
            {
                GameLogger.Debug(message);
            }
        }

        public static void Info(object message)
        {
            if (IsLogEnabled(Config.Enable_Info_LOG))
            {
                GameLogger.Info(message);
            }
        }

        public static void Warning(object message)
        {
            if (IsLogEnabled(Config.Enable_Warning_LOG))
            {
                GameLogger.Warning(message);
            }
        }

        public static void Error(object message)
        {
            if (IsLogEnabled(Config.Enable_Error_LOG))
            {
                GameLogger.Error(message);
            }
        }

        public static void Fatal(object message)
        {
            if (IsLogEnabled(Config.Enable_Fatal_LOG))
            {
                GameLogger.Fatal(message);
            }
        }
    }
}