using ReunionMovement.Common;

namespace ReunionMovement.Framework.Runtime.UI
{
    /// <summary>窗口生命周期状态。由 <see cref="UIController"/> 的钩子驱动，业务一般不直接读写。</summary>
    public enum UIWindowState
    {
        /// <summary>刚实例化，还没 OnInit。</summary>
        None = 0,
        /// <summary>已 OnInit，可以打开。</summary>
        Ready,
        /// <summary>已 OnOpen（显示中）。</summary>
        Open,
        /// <summary>已 OnClose（不再显示，可能被池化）。</summary>
        Closed,
    }

    /// <summary>
    /// <see cref="UIController"/> 的生命周期状态机（守卫层）。
    ///
    /// <para>
    /// 原来 7 个 virtual（<c>OnInit/BeforeOpen/OnOpen/OnReopen/OnClose/OnSpawned/OnDespawned</c>）
    /// 之间没有任何约束：重复 <c>OnOpen</c>、没 <c>OnClose</c> 就 <c>OnSpawned</c>（池化复用漏清理）都只是静默发生。
    /// 这里给它们加一个状态机：**签名与调用方式完全不变**，只是在每次进入钩子时校验转换是否合法，
    /// 非法就告警（不抛异常、不改变行为），把"靠自觉"变成"能看见"。
    /// </para>
    ///
    /// <para>
    /// 合法转换：
    /// <code>
    /// None ──OnInit──▶ Ready ──OnOpen──▶ Open ──OnClose──▶ Closed
    ///                    ▲                                   │
    ///                    └──── OnSpawned（池化复用）◀─────────┘
    /// </code>
    /// </para>
    /// </summary>
    public partial class UIController
    {
        private UIWindowState state = UIWindowState.None;

        /// <summary>当前生命周期状态。</summary>
        public UIWindowState State => state;

        /// <summary>是否处于打开状态（等价于 <see cref="IsVisiable"/> 的状态侧判断）。</summary>
        public bool IsOpenState => state == UIWindowState.Open;

        /// <summary>
        /// 进入状态并校验转换。只在基础类的钩子实现里调用，业务不要直接调。
        /// </summary>
        /// <param name="next">目标状态</param>
        /// <param name="hook">触发它的钩子名（仅用于日志）</param>
        internal void EnterState(UIWindowState next, string hook)
        {
            if (!IsTransitionAllowed(state, next))
            {
                // 只告警：不改变既有行为，避免把老工程直接打断
                Log.Warning("[UIController] {0}: 生命周期状态非法转换 {1} → {2}（钩子 {3}）。" +
                            "常见原因：未 OnClose 就被池化复用，或重复 OnOpen。",
                    GetType().Name, state, next, hook);
            }

            state = next;
        }

        /// <summary>清空状态（销毁/重建时用）。</summary>
        internal void ResetState()
        {
            state = UIWindowState.None;
        }

        private static bool IsTransitionAllowed(UIWindowState from, UIWindowState to)
        {
            switch (to)
            {
                case UIWindowState.Ready:
                    // OnInit：首次；OnSpawned：从 Closed 复用
                    return from == UIWindowState.None || from == UIWindowState.Closed;
                case UIWindowState.Open:
                    // Ready 正常打开；Open 重复打开要告警；Closed 后未 OnInit 直接打开也告警
                    return from == UIWindowState.Ready;
                case UIWindowState.Closed:
                    return from == UIWindowState.Open;
                default:
                    return true;
            }
        }
    }
}
