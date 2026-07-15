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

    /// <summary>
    /// 日志频道（子系统分类），用于按模块过滤日志输出。
    /// 使用示例：Log.Info("连接成功", LogChannel.Network);
    /// </summary>
    public enum LogChannel
    {
        /// <summary>通用（默认）</summary>
        General = 0,

        /// <summary>网络</summary>
        Network,

        /// <summary>UI</summary>
        UI,

        /// <summary>AI</summary>
        AI,

        /// <summary>音频</summary>
        Audio,

        /// <summary>输入</summary>
        Input,

        /// <summary>场景</summary>
        Scene,

        /// <summary>资源加载</summary>
        Resource,

        /// <summary>自定义频道 1</summary>
        Custom1,

        /// <summary>自定义频道 2</summary>
        Custom2,

        /// <summary>自定义频道 3</summary>
        Custom3,
    }
}
