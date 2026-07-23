using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement
{
    /// <summary>
    /// 配置类 —— 优先从 GameConfig ScriptableObject 加载，缺失时使用静态默认值。
    /// 资源路径使用 const（编译时常量），日志开关支持 ScriptableObject 运行时覆盖。
    ///
    /// 优化：首次调用 EnsureLoaded() 后缓存 GameConfig 引用，后续属性访问直接读字段，
    /// 避免每次 getter 都走 LoadConfig() 的 null 传播路径。
    /// 推荐在 GameEngine.BeforeInit 阶段调用 EnsureLoaded()。
    /// </summary>
    public static class Config
    {
        // ============================================================
        //  资源路径（编译时常量 —— 不可变）
        // ============================================================
        public const string JsonPath = "AutoDatabase/";
        public const string UIPath = "Prefabs/UIs/";
        public const string UIToolkitUxmlPath = "UI/UIToolkit/";
        public const string UIToolkitUssPath = "UI/UIToolkit/Styles/";

        // ============================================================
        //  ScriptableObject 配置
        // ============================================================
        private static GameConfig cachedConfig;
        private static bool configLoaded;

        /// <summary>
        /// 确保配置已加载（推荐在 GameEngine.BeforeInit 阶段调用）。
        /// 幂等操作 —— 多次调用只执行一次 Resources.Load。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (!configLoaded)
            {
                configLoaded = true;
                cachedConfig = Resources.Load<GameConfig>("ScriptableObjects/GameConfig");
            }
        }

        /// <summary>
        /// 强制重新加载配置（运行时修改 ScriptableObject 后调用）。
        /// </summary>
        public static void RefreshConfig()
        {
            configLoaded = false;
            cachedConfig = null;
            EnsureLoaded();
        }

        /// <summary>获取当前缓存的配置引用（可能为 null）。</summary>
        private static GameConfig Cfg
        {
            get
            {
                if (!configLoaded) EnsureLoaded();
                return cachedConfig;
            }
        }

        // ============================================================
        //  日志等级开关
        // ============================================================
        public static bool Enable_LOG
        {
            get => Cfg?.enableLog ?? true;
            set { if (Cfg != null) Cfg.enableLog = value; }
        }

        public static bool Enable_Debug_LOG
        {
            get
            {
                var cfg = Cfg;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return cfg?.EnableDebugLog ?? true;
#else
                return cfg?.EnableDebugLog ?? false;
#endif
            }
        }

        public static bool Enable_Info_LOG
        {
            get
            {
                var cfg = Cfg;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return cfg?.EnableInfoLog ?? true;
#else
                return cfg?.EnableInfoLog ?? false;
#endif
            }
        }

        public static bool Enable_Warning_LOG
        {
            get => Cfg?.enableWarningLog ?? true;
            set { if (Cfg != null) Cfg.enableWarningLog = value; }
        }

        public static bool Enable_Error_LOG
        {
            get => Cfg?.enableErrorLog ?? true;
            set { if (Cfg != null) Cfg.enableErrorLog = value; }
        }

        public static bool Enable_Fatal_LOG
        {
            get => Cfg?.enableFatalLog ?? true;
            set { if (Cfg != null) Cfg.enableFatalLog = value; }
        }

        // ============================================================
        //  日志频道开关
        // ============================================================
        private static bool Channel(LogChannel channel, bool defaultValue)
        {
            var cfg = Cfg;
            if (cfg == null) return defaultValue;
            return channel switch
            {
                LogChannel.General  => cfg.channelGeneral,
                LogChannel.Network  => cfg.channelNetwork,
                LogChannel.UI       => cfg.channelUI,
                LogChannel.AI       => cfg.channelAI,
                LogChannel.Audio    => cfg.channelAudio,
                LogChannel.Input    => cfg.channelInput,
                LogChannel.Scene    => cfg.channelScene,
                LogChannel.Resource => cfg.channelResource,
                LogChannel.Custom1  => cfg.channelCustom1,
                LogChannel.Custom2  => cfg.channelCustom2,
                LogChannel.Custom3  => cfg.channelCustom3,
                _                   => defaultValue
            };
        }

        public static bool Enable_Channel_General
        {
            get => Channel(LogChannel.General, true);
            set { var c = Cfg; if (c != null) c.channelGeneral = value; }
        }
        public static bool Enable_Channel_Network
        {
            get => Channel(LogChannel.Network, true);
            set { var c = Cfg; if (c != null) c.channelNetwork = value; }
        }
        public static bool Enable_Channel_UI
        {
            get => Channel(LogChannel.UI, true);
            set { var c = Cfg; if (c != null) c.channelUI = value; }
        }
        public static bool Enable_Channel_AI
        {
            get => Channel(LogChannel.AI, true);
            set { var c = Cfg; if (c != null) c.channelAI = value; }
        }
        public static bool Enable_Channel_Audio
        {
            get => Channel(LogChannel.Audio, true);
            set { var c = Cfg; if (c != null) c.channelAudio = value; }
        }
        public static bool Enable_Channel_Input
        {
            get => Channel(LogChannel.Input, true);
            set { var c = Cfg; if (c != null) c.channelInput = value; }
        }
        public static bool Enable_Channel_Scene
        {
            get => Channel(LogChannel.Scene, true);
            set { var c = Cfg; if (c != null) c.channelScene = value; }
        }
        public static bool Enable_Channel_Resource
        {
            get => Channel(LogChannel.Resource, true);
            set { var c = Cfg; if (c != null) c.channelResource = value; }
        }
        public static bool Enable_Channel_Custom1
        {
            get => Channel(LogChannel.Custom1, true);
            set { var c = Cfg; if (c != null) c.channelCustom1 = value; }
        }
        public static bool Enable_Channel_Custom2
        {
            get => Channel(LogChannel.Custom2, true);
            set { var c = Cfg; if (c != null) c.channelCustom2 = value; }
        }
        public static bool Enable_Channel_Custom3
        {
            get => Channel(LogChannel.Custom3, true);
            set { var c = Cfg; if (c != null) c.channelCustom3 = value; }
        }

        /// <summary>检查指定频道是否开启。</summary>
        public static bool IsChannelEnabled(LogChannel channel)
        {
            return Channel(channel, true);
        }
    }
}
