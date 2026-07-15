namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏引擎生命周期状态
    /// </summary>
    public enum EngineState
    {
        /// <summary>未初始化</summary>
        Uninitialized,
        /// <summary>初始化前（加载配置等）</summary>
        BeforeInit,
        /// <summary>正在初始化各模块</summary>
        Initializing,
        /// <summary>正在启动游戏</summary>
        Starting,
        /// <summary>运行中</summary>
        Running,
        /// <summary>初始化失败</summary>
        Failed,
        /// <summary>已销毁</summary>
        Disposed
    }
}
