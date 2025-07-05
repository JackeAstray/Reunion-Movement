using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 日志等级
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 致命错误
        /// </summary>
        Fatal
    }
}
