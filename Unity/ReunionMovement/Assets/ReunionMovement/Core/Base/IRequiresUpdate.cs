using System;

namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// [已废弃] 标记接口：请改用 ISystemUpdatable。
    /// 实现 ISystemUpdatable 的模块会被 GameEngine 每帧驱动 Update。
    /// 不实现此接口的模块不会被遍历，避免空调用开销。
    /// </summary>
    [Obsolete("请直接实现 ISystemUpdatable 接口（包含 Update 方法）", false)]
    public interface IRequiresUpdate : ISystemUpdatable
    {
        // 继承 ISystemUpdatable 的 Update 方法签名，确保旧代码编译通过
        // 新代码应直接实现 ISystemUpdatable
    }
}
