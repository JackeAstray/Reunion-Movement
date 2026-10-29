namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// 窗口回传的结果。用它而不是裸 <c>T</c>，是为了区分两种"没拿到值"：
    /// <list type="bullet">
    /// <item><c>HasValue == false</c>：窗口被直接关掉 / 加载失败（相当于取消）；</item>
    /// <item><c>HasValue == true</c>：窗口主动 <c>CloseWithResult(...)</c> 回传了值。</item>
    /// </list>
    ///
    /// <code>
    /// var result = await UIRouter.PushForResultAsync&lt;ShopUI, ShopArgs, int&gt;(new ShopArgs("weapon", 1));
    /// if (result.HasValue) { int picked = result.Value; }
    /// </code>
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    public readonly struct WindowResult<T>
    {
        /// <summary>是否拿到了结果。</summary>
        public readonly bool HasValue;

        /// <summary>结果值（<see cref="HasValue"/> 为 false 时是 default）。</summary>
        public readonly T Value;

        /// <param name="value">结果值</param>
        public WindowResult(T value)
        {
            HasValue = true;
            Value = value;
        }

        /// <summary>没有结果（取消 / 加载失败）。</summary>
        public static WindowResult<T> None => default;

        /// <summary>有值时取出来，否则返回 <paramref name="fallback"/>。</summary>
        public T GetValueOrDefault(T fallback = default)
        {
            return HasValue ? Value : fallback;
        }

        public override string ToString()
        {
            return HasValue ? "Some(" + Value + ")" : "None";
        }
    }
}
