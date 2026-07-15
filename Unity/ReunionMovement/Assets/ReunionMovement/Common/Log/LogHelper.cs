using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement.Common
{
    public class LogHelper : ILogHelper
    {
        // 预计算的Tag常量，避免每次Log调用都分配颜色Tag字符串
        private const string kTagDebug   = "<color=#80FF00>[调试] ";
        private const string kTagInfo    = "<color=#00FF00>[信息] ";
        private const string kTagWarning = "<color=#FFCC00>[警告] ";
        private const string kTagError   = "<color=#FF0040>[错误] ";
        private const string kTagFatal   = "<color=#FF0000>[致命] ";
        private const string kTagUnknown = "<color=#FF0040>[未知日志等级] ";
        private const string kTagClose   = "</color>";

        /// <summary>
        /// 记录日志（使用 string.Concat 替代 + 拼接，减少中间字符串分配）。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        public void Log(LogLevel level, object message)
        {
            Log(level, message?.ToString(), null);
        }

        /// <summary>
        /// 记录日志（带 Unity Object 上下文，Console 中点击可定位到具体 GameObject）。
        /// </summary>
        public void Log(LogLevel level, string message, Object context)
        {
            switch (level)            {
                case LogLevel.Debug:
                    Debug.Log(string.Concat(kTagDebug, message, kTagClose), context);
                    break;

                case LogLevel.Info:
                    Debug.Log(string.Concat(kTagInfo, message, kTagClose), context);
                    break;

                case LogLevel.Warning:
                    Debug.LogWarning(string.Concat(kTagWarning, message, kTagClose), context);
                    break;

                case LogLevel.Error:
                    Debug.LogError(string.Concat(kTagError, message, kTagClose), context);
                    break;

                case LogLevel.Fatal:
                    Debug.LogError(string.Concat(kTagFatal, message, kTagClose), context);
                    break;

                default:
                    Debug.LogError(string.Concat(kTagUnknown, level.ToString(), ": ", message, kTagClose), context);
                    break;
            }
        }

        /// <summary>
        /// 记录格式化日志（单参数），避免 params object[] 数组分配。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(string.Concat(kTagDebug, string.Format(format, arg0), kTagClose));
                    break;
                case LogLevel.Info:
                    Debug.Log(string.Concat(kTagInfo, string.Format(format, arg0), kTagClose));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(string.Concat(kTagWarning, string.Format(format, arg0), kTagClose));
                    break;
                case LogLevel.Error:
                    Debug.LogError(string.Concat(kTagError, string.Format(format, arg0), kTagClose));
                    break;
                case LogLevel.Fatal:
                    Debug.LogError(string.Concat(kTagFatal, string.Format(format, arg0), kTagClose));
                    break;
                default:
                    Debug.LogError(string.Concat(kTagUnknown, level.ToString(), ": ", string.Format(format, arg0), kTagClose));
                    break;
            }
        }

        /// <summary>
        /// 记录格式化日志（双参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(string.Concat(kTagDebug, string.Format(format, arg0, arg1), kTagClose));
                    break;
                case LogLevel.Info:
                    Debug.Log(string.Concat(kTagInfo, string.Format(format, arg0, arg1), kTagClose));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(string.Concat(kTagWarning, string.Format(format, arg0, arg1), kTagClose));
                    break;
                case LogLevel.Error:
                    Debug.LogError(string.Concat(kTagError, string.Format(format, arg0, arg1), kTagClose));
                    break;
                case LogLevel.Fatal:
                    Debug.LogError(string.Concat(kTagFatal, string.Format(format, arg0, arg1), kTagClose));
                    break;
                default:
                    Debug.LogError(string.Concat(kTagUnknown, level.ToString(), ": ", string.Format(format, arg0, arg1), kTagClose));
                    break;
            }
        }

        /// <summary>
        /// 记录格式化日志（三参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(string.Concat(kTagDebug, string.Format(format, arg0, arg1, arg2), kTagClose));
                    break;
                case LogLevel.Info:
                    Debug.Log(string.Concat(kTagInfo, string.Format(format, arg0, arg1, arg2), kTagClose));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(string.Concat(kTagWarning, string.Format(format, arg0, arg1, arg2), kTagClose));
                    break;
                case LogLevel.Error:
                    Debug.LogError(string.Concat(kTagError, string.Format(format, arg0, arg1, arg2), kTagClose));
                    break;
                case LogLevel.Fatal:
                    Debug.LogError(string.Concat(kTagFatal, string.Format(format, arg0, arg1, arg2), kTagClose));
                    break;
                default:
                    Debug.LogError(string.Concat(kTagUnknown, level.ToString(), ": ", string.Format(format, arg0, arg1, arg2), kTagClose));
                    break;
            }
        }

        /// <summary>
        /// 记录格式化日志（四参数）。
        /// </summary>
        public void LogFormat(LogLevel level, string format, object arg0, object arg1, object arg2, object arg3)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(string.Concat(kTagDebug, string.Format(format, arg0, arg1, arg2, arg3), kTagClose));
                    break;
                case LogLevel.Info:
                    Debug.Log(string.Concat(kTagInfo, string.Format(format, arg0, arg1, arg2, arg3), kTagClose));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(string.Concat(kTagWarning, string.Format(format, arg0, arg1, arg2, arg3), kTagClose));
                    break;
                case LogLevel.Error:
                    Debug.LogError(string.Concat(kTagError, string.Format(format, arg0, arg1, arg2, arg3), kTagClose));
                    break;
                case LogLevel.Fatal:
                    Debug.LogError(string.Concat(kTagFatal, string.Format(format, arg0, arg1, arg2, arg3), kTagClose));
                    break;
                default:
                    Debug.LogError(string.Concat(kTagUnknown, level.ToString(), ": ", string.Format(format, arg0, arg1, arg2, arg3), kTagClose));
                    break;
            }
        }

        /// <summary>
        /// 记录格式化日志（5+ 参数兜底），0~4 参数请优先使用非 params 重载。
        /// </summary>
        public void LogFormat(LogLevel level, string format, params object[] args)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(string.Concat(kTagDebug, string.Format(format, args), kTagClose));
                    break;
                case LogLevel.Info:
                    Debug.Log(string.Concat(kTagInfo, string.Format(format, args), kTagClose));
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(string.Concat(kTagWarning, string.Format(format, args), kTagClose));
                    break;
                case LogLevel.Error:
                    Debug.LogError(string.Concat(kTagError, string.Format(format, args), kTagClose));
                    break;
                case LogLevel.Fatal:
                    Debug.LogError(string.Concat(kTagFatal, string.Format(format, args), kTagClose));
                    break;
                default:
                    Debug.LogError(string.Concat(kTagUnknown, level.ToString(), ": ", string.Format(format, args), kTagClose));
                    break;
            }
        }
    }
}