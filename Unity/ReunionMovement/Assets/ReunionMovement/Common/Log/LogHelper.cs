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
            switch (level)
            {
                case LogLevel.Debug:
                    {
                        string msg = string.Format("<color=#80FF00>[调试] {0}</color>", message);
                        Debug.Log(msg);
                        break;
                    }

                case LogLevel.Info:
                    {
                        string msg = string.Format("<color=#00FF00>[信息] {0}</color>", message);
                        Debug.Log(msg);
                        break;
                    }

                case LogLevel.Warning:
                    {
                        string msg = string.Format("<color=#FFCC00>[警告] {0}</color>", message);
                        Debug.LogWarning(msg);
                        break;
                    }

                case LogLevel.Error:
                    {
                        string msg = string.Format("<color=#FF0040>[错误] {0}</color>", message);
                        Debug.LogError(msg);
                        break;
                    }

                case LogLevel.Fatal:
                    {
                        string msg = string.Format("<color=#FF0000>[致命] {0}</color>", message);
                        Debug.LogError(msg);
                        break;
                    }

                default:
                    throw new System.Exception($"未知日志等级: {message}");
            }
        }
    }
}