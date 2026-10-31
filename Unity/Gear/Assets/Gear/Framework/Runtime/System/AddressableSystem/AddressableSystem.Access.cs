namespace Gear.Framework.Runtime.Resources
{
    public partial class AddressableSystem
    {
        /// <summary>
        /// 引擎已启动时取注册实例；否则回退到单例（编辑器 / 预初始化 / 独立使用）。
        /// 调用方统一用本方法，避免直接散落 <c>Xxx.Instance</c>；注册点仍必须用 <c>Instance</c>。
        /// </summary>
        public static AddressableSystem GetOrInstance()
        {
            return Gear.Framework.Runtime.Modules.TryGet(out AddressableSystem system) ? system : Instance;
        }
    }
}
