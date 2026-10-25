namespace ReunionMovement.Core.UI
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
    /// 不需要再写 <c>GetFromArgs&lt;T&gt;(args, 0)</c> 这种按位置取值。
    ///
    /// <para>
    /// 与老路径的关系：<see cref="UIController.OnOpen"/> 仍会照常收到 <c>object[]{ args }</c>，
    /// 所以同一个窗口可以只实现其中一个；两个都实现时，后者（强类型）在 <c>OnOpen</c> 之后调用。
    /// </para>
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public interface IWindowArgsReceiver<in T> where T : IWindowArgs
    {
        /// <summary>收到强类型参数。</summary>
        void OnWindowArgs(T args);
    }
}
