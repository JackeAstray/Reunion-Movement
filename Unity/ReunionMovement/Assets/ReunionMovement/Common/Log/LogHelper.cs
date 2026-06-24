using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement.Common
{
    public class LogHelper : ILogHelper
    {
        /// <summary>
        /// 记录日志。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        public void Log(LogLevel level, object message)
        {
            // 使用字符串拼接而非 string.Format，避免 message 中含 { 或 } 时抛出 FormatException
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log("<color=#80FF00>[调试] " + message + "</color>");
                    break;

                case LogLevel.Info:
                    Debug.Log("<color=#00FF00>[信息] " + message + "</color>");
                    break;

                case LogLevel.Warning:
                    Debug.LogWarning("<color=#FFCC00>[警告] " + message + "</color>");
                    break;

                case LogLevel.Error:
                    Debug.LogError("<color=#FF0040>[错误] " + message + "</color>");
                    break;

                case LogLevel.Fatal:
                    Debug.LogError("<color=#FF0000>[致命] " + message + "</color>");
                    break;

                default:
                    Debug.LogError($"<color=#FF0040>[未知日志等级] {level}: {message}</color>");
                    break;
            }
        }
    }
}