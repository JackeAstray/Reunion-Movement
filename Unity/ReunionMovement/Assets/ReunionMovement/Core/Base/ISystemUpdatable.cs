namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 可更新系统 —— 实现此接口的模块会被 GameEngine 每帧驱动 Update。
    /// 仅实现此接口而不实现 ICustomSystem 的类也会被注册到更新列表。
    /// 替代已废弃的 IRequiresUpdate 标记接口。
    /// </summary>
    public interface ISystemUpdatable
    {
        /// <summary>
        /// 更新模块时间
        /// </summary>
        /// <param name="logicTime">逻辑流逝时间（受 timeScale 影响）</param>
        /// <param name="realTime">真实流逝时间（不受 timeScale 影响）</param>
        void Update(float logicTime, float realTime);
    }
}
