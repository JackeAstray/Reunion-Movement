namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 可延迟更新系统 —— 实现此接口的模块会被 GameEngine 在每帧模块 Update 之后、
    /// 摄像机渲染之前（Unity LateUpdate 时机）驱动 LateUpdate。
    /// 适用于依赖其他模块当帧状态的逻辑（如跟随动画、相机、UI 布局收尾）。
    /// 与 ISystemUpdatable 一样支持暂停期间驱动（logicTime 归零，realTime 继续）。
    /// </summary>
    public interface ISystemLateUpdatable
    {
        /// <summary>
        /// 延迟更新模块时间（在 ISystemUpdatable 全部执行完毕后调用）
        /// </summary>
        /// <param name="logicTime">逻辑流逝时间（受 timeScale 影响）</param>
        /// <param name="realTime">真实流逝时间（不受 timeScale 影响）</param>
        void LateUpdate(float logicTime, float realTime);
    }
}
