namespace Gear.Framework.Runtime.Base
{
    /// <summary>
    /// 分辨率应用能力 —— 由 <c>Framework/Modules</c> 的 ResolutionMgr 实现，
    /// 让 <c>GameOption</c> 不必直接引用 Modules 程序集。
    /// </summary>
    public interface IResolutionApplier
    {
        void ApplyResolutionFromOptions(int width, int height, bool fullscreen);
    }

    /// <summary>
    /// 分辨率应用者的运行时桥。ResolutionMgr 在 Awake 注册、OnDestroy 注销；
    /// 没有实现者时 GameOption 退回 <c>Screen.SetResolution</c>（与旧行为一致）。
    /// </summary>
    public static class ResolutionBridge
    {
        /// <summary>当前实现者；没有时为 null。</summary>
        public static IResolutionApplier Current { get; private set; }

        /// <summary>注册实现者（后注册的覆盖先前的）。</summary>
        public static void Register(IResolutionApplier applier)
        {
            if (applier != null) Current = applier;
        }

        /// <summary>注销实现者；只清除当前实例，避免误删新实例。</summary>
        public static void Unregister(IResolutionApplier applier)
        {
            if (ReferenceEquals(Current, applier)) Current = null;
        }

        /// <summary>清空实现者（跨 Play 会话复位用）。</summary>
        public static void Reset()
        {
            Current = null;
        }
    }
}
