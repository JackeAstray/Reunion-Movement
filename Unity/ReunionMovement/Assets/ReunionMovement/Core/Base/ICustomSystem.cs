using Cysharp.Threading.Tasks;

namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 自定义模块（完整接口）—— 继承三个细粒度接口。
    ///
    /// 遵循接口隔离原则 (ISP)：
    /// - 如果模块仅需 Init，可实现 ISystemInitializable
    /// - 如果模块需要每帧 Update，额外实现 ISystemUpdatable
    /// - 如果模块需要清理逻辑，额外实现 ISystemDisposable
    /// - 如果模块需要全部功能，实现 ICustomSystem（等同于三者全部）
    /// </summary>
    public interface ICustomSystem : ISystemInitializable, ISystemUpdatable, ISystemDisposable
    {
    }
}