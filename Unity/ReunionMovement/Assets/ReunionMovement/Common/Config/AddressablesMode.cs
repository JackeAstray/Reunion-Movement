namespace ReunionMovement
{
    /// <summary>
    /// Addressables 运行模式 —— 由 <c>GameConfig.enableHotUpdate</c> 派生，不是独立配置项。
    ///
    /// 设计意图：Addressables 只在需要热更新时启用。不开热更新时一律走 Resources ——
    /// 那条路径更短（无 catalog 初始化、无 location 查找、无 bundle 装载），性能更优。
    /// 定义在 Common 层：Config/GameConfig/AddressableSystem/SceneSystem 等多层均需引用，
    /// 放在根命名空间避免各层互相反向依赖。
    /// </summary>
    public enum AddressablesMode
    {
        /// <summary>完全关闭 Addressables（纯 Resources 模式）</summary>
        Off = 0,

        // 值 1 曾为 LocalOnly（"仅本地 Bundle、不联网"），已移除：
        // 该模式在玩家包内不成立（AddressableAssetSettings.m_BuildAddressablesWithPlayerBuild = 0，
        // 包内无 bundle），真实用途只是 Editor 内验证链路，且相比 Off 没有任何性能收益。
        // 此处保留空位，避免历史序列化数据/外部代码把 1 误读成其他含义。

        /// <summary>本地 + 远程（从 CDN 加载并检查更新，即热更新）</summary>
        Remote = 2,
    }
}
