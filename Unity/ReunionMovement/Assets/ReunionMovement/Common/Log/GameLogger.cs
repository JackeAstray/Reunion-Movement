using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static void Debug(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(LogLevel.Debug, message);
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Info(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(LogLevel.Info, message);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Warning(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(LogLevel.Warning, message);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Error(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(LogLevel.Error, message);
        }

        /// <summary>
        /// 打印严重错误级别日志，建议在发生严重错误，可能导致游戏崩溃或异常时使用，此时应尝试重启进程或重建游戏框架。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Fatal(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(LogLevel.Fatal, message);
        }
    }
}
