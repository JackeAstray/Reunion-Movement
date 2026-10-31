namespace Gear.Framework.Runtime.Base
{
    /// <summary>
    /// 可清理系统 —— 提供 Clear 方法用于释放模块数据。
    /// 遵循接口隔离原则 (ISP)：不强制实现 Init 和 Update。
    /// </summary>
    public interface ISystemDisposable
    {
        /// <summary>清理模块数据（切换账号/低内存/引擎销毁时调用）</summary>
        void Clear();
    }

    /// <summary>
    /// 标记接口：该模块的 <see cref="ISystemDisposable.Clear"/> 必须晚于其他模块执行。
    /// 典型是资源提供者（ResourcesSystem）—— 其他模块清理时会归还它缓存的资源引用，
    /// 它必须先接收这些归还在最后统一 Unload。引擎按此标记排序，替代按具体类型硬编码。
    /// </summary>
    public interface ISystemClearLast
    {
    }
}
