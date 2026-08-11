namespace ReunionMovement.Core.Resources
{
    /// <summary>
    /// Addressables 地址常量 —— 消除魔法字符串，统一资源寻址入口。
    /// 路径前缀对应 Assets/AddressableAssets/ 下的物理目录（见仓库 Docs/Addressables/Addressables集成设计方案.md §4.1）。
    /// 运行时请统一通过 AddressableSystem 加载；Label 用于批量预载 / 清理。
    /// </summary>
    public static class AddressableKeys
    {
        // ========== 资源根前缀（与 AddressableAssets/ 物理目录对齐） ==========
        /// <summary>内置（BuiltIn）资源根</summary>
        public const string BuiltInRoot = "BuiltIn/";
        /// <summary>远程（Remote）资源根</summary>
        public const string RemoteRoot = "Remote/";

        // ========== UI（对应 AddressableAssets/BuiltIn/UI/） ==========
        /// <summary>UI 资源根</summary>
        public const string UIRoot = BuiltInRoot + "UI/";
        /// <summary>启动界面（与 UINames.StartGame 一致）</summary>
        public const string UIStartGame = UIRoot + "StartGameUIPlane";

        // ========== Prefabs（对应 AddressableAssets/BuiltIn/Prefabs/） ==========
        /// <summary>Prefab 资源根</summary>
        public const string PrefabRoot = BuiltInRoot + "Prefabs/";
        /// <summary>敌人 Prefab（对象池示例）</summary>
        public const string PrefabEnemy = PrefabRoot + "Enemy";
        /// <summary>特效 Prefab（对象池示例）</summary>
        public const string PrefabEffect = PrefabRoot + "Effect";

        // ========== 场景（对应 AddressableAssets/Remote/Scenes/） ==========
        /// <summary>场景资源根</summary>
        public const string SceneRoot = RemoteRoot + "Scenes/";
        /// <summary>游戏主场景（示例）</summary>
        public const string SceneGame = SceneRoot + "GameScene";

        // ========== 音频（对应 AddressableAssets/Remote/Sounds/） ==========
        /// <summary>音频资源根（迁移自 Resources/Sounds）</summary>
        public const string SoundRoot = RemoteRoot + "Sounds/";

        // ========== 纹理/图片（对应 AddressableAssets/Remote/Textures/） ==========
        /// <summary>纹理资源根（迁移自 Resources/UI/Sprites）</summary>
        public const string TextureRoot = RemoteRoot + "Textures/";

        // ========== Label ==========
        /// <summary>内置资源 Label</summary>
        public const string LabelBuiltIn = "builtin";
        /// <summary>远程资源 Label</summary>
        public const string LabelRemote = "remote";
        /// <summary>UI 功能 Label</summary>
        public const string LabelUI = "ui";
        /// <summary>音频功能 Label</summary>
        public const string LabelSound = "sound";
        /// <summary>纹理/图集功能 Label</summary>
        public const string LabelTexture = "texture";
        /// <summary>场景功能 Label</summary>
        public const string LabelScene = "scene";
        /// <summary>数据（JSON 等）功能 Label</summary>
        public const string LabelData = "data";
    }
}
