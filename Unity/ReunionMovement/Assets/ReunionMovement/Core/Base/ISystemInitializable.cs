using Cysharp.Threading.Tasks;

namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 可初始化系统 —— 仅需 Init 的轻量接口。
    /// 遵循接口隔离原则 (ISP)：不强制实现 Update 和 Clear。
    /// </summary>
    public interface ISystemInitializable
    {
        /// <summary>初始化进度（0~100）</summary>
        double InitProgress { get; }

        /// <summary>初始化（UniTask 零 GC 异步）</summary>
        UniTask Init();
    }
}
