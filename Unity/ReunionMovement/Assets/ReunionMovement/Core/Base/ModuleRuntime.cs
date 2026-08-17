namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 模块运行时状态 —— 引擎层（GameEngine）与工具层（Utils 的 MonoBehaviour 单例兜底路径）共享的轻量状态。
    /// 用途：HttpMgr/NetworkMgr/TimerMgr 等"同时是 MonoBehaviour 单例 + 引擎模块"的组件，
    /// 在 MonoBehaviour.Update 兜底时需要判断引擎是否正在驱动模块，避免双重驱动。
    /// 提取此类型是为了打破 Utils → Core 的反向引用（Utils 程序集不得依赖 Core 程序集）。
    /// </summary>
    public static class ModuleRuntime
    {
        /// <summary>
        /// 引擎是否处于 Running 状态（由 GameEngine 启动/失败/销毁时维护）。
        /// setter 为 public：GameEngine 位于 Core 程序集，而本类型位于 Base 程序集，
        /// internal setter 跨程序集不可见；该标志仅为运行时状态，公开写入无害。
        /// </summary>
        public static bool IsEngineRunning { get; set; }
    }
}
