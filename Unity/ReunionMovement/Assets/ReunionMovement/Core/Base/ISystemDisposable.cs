namespace ReunionMovement.Core.Base
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
}
