using Cysharp.Threading.Tasks;

namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 自定义模块（基础接口）—— 仅要求 Init，遵循接口隔离原则 (ISP)。
    ///
    /// 细粒度接口：
    /// - ISystemInitializable：所有模块必须实现（Init + InitProgress）
    /// - ISystemUpdatable：需要每帧 Update 的模块额外实现
    /// - ISystemDisposable：需要清理逻辑的模块额外实现
    ///
    /// 推荐用法：
    /// - 仅需 Init 的模块：实现 ICustomSystem
    /// - 需要 Update：额外实现 ISystemUpdatable
    /// - 需要清理：额外实现 ISystemDisposable
    /// - 全部需要：同时实现三者（GameEngine 自动检测）
    /// </summary>
    public interface ICustomSystem : ISystemInitializable
    {
    }
}