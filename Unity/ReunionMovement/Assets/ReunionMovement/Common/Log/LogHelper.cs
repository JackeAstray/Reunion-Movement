using ReunionMovement.Common;
using UnityEngine;

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
                string msg = string.Format("<color=#80FF00>{0}</color>", message);
                Debug.Log(msg);
                break;

            case LogLevel.Info:
                msg = string.Format("<color=#00FF00>{0}</color>", message);
                Debug.Log(msg);
                break;

            case LogLevel.Warning:
                msg = string.Format("<color=#FFCC00>{0}</color>", message);
                Debug.LogWarning(msg);
                break;

            case LogLevel.Error:
                msg = string.Format("<color=#FF0040>{0}</color>", message);
                Debug.LogError(msg);
                break;

            case LogLevel.Fatal:
                msg = string.Format("<color=#FF0000>{0}</color>", message);
                Debug.LogError(msg);
                break;

            default:
                throw new System.Exception(message.ToString());
        }
    }
}
