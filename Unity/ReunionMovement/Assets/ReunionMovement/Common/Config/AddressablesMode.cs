namespace ReunionMovement
{
    /// <summary>
    /// Addressables 运行模式（由 GameConfig 配置）。
    /// 定义在 Common 层：Config/GameConfig/AddressableSystem/SceneSystem 等多层均需引用，
    /// 放在根命名空间避免各层互相反向依赖。
    /// </summary>
    public enum AddressablesMode
    {
        /// <summary>完全关闭 Addressables（纯 Resources 降级模式）</summary>
        Off = 0,
        /// <summary>仅本地 Bundle（无远程更新）</summary>
        LocalOnly = 1,
        /// <summary>本地 + 远程（支持 CDN 热更）</summary>
        Remote = 2,
    }
}
