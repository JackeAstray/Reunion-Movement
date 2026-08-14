namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 固定步长更新系统 —— 实现此接口的模块会被 GameEngine 在 FixedUpdate 驱动。
    /// 用于物理/确定性逻辑（当前业务代码 0 处 FixedUpdate，此接口补齐引擎桥接能力）。
    /// 注意：Time.timeScale=0 时 Unity 的 FixedUpdate 停摆（PauseSystem 暂停期间不会被驱动），
    /// 需要暂停期间继续运行的逻辑请使用 ISystemUpdatable 的 realTime 参数自行判断。
    /// </summary>
    public interface ISystemFixedUpdatable
    {
        /// <summary>
        /// 固定步长更新
        /// </summary>
        /// <param name="fixedDeltaTime">固定时间步长（Time.fixedDeltaTime）</param>
        void FixedUpdate(float fixedDeltaTime);
    }
}
