namespace Gear.Framework.Runtime.UI
{
    /// <summary>
    /// 窗口参数的标记接口。用 <c>readonly struct</c> / <c>record</c> 实现，调用方与窗口之间就有了编译期契约。
    ///
    /// <code>
    /// public readonly record struct ShopArgs(string Category, int Page) : IWindowArgs;
    /// </code>
    /// </summary>
    public interface IWindowArgs
    {
    }

    /// <summary>无参数窗口的占位类型（省得为"没有参数"再造一个空结构体）。</summary>
    public readonly struct NoWindowArgs : IWindowArgs
    {
        /// <summary>唯一实例。</summary>
        public static readonly NoWindowArgs Default = default;
    }

    /// <summary>
    /// 窗口接收强类型参数。窗口实现它即可被 <c>UISystem.OpenWindowAsync&lt;TWindow, TArgs&gt;</c> 直接喂参数，
    /// 不再有按位置取值的 <c>args[N]</c> 写法。
    ///
    /// <para>
    /// 与 <see cref="UIController.OnOpen()"/> 的关系：<c>OnOpen()</c> 不再携带参数，可在其中做与参数无关的界面初始化；
    /// 需要参数就实现本接口。不实现不会报错 —— 参数被忽略，派发处记一条 Debug 日志。
    /// </para>
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public interface IWindowArgsReceiver<in T> where T : IWindowArgs
    {
        /// <summary>收到强类型参数。</summary>
        void OnWindowArgs(T args);
    }
}
