using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement
{
    /// <summary>
    /// 配置类 —— 优先从 GameConfig ScriptableObject 加载，缺失时使用静态默认值。
    /// 资源路径使用 const（编译时常量），日志开关支持 ScriptableObject 运行时覆盖。
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
        //  ScriptableObject 配置（懒加载，回退到静态默认值）
        // ============================================================
        private static GameConfig cachedConfig;
        private static bool configLoaded;

        private static GameConfig LoadConfig()
        {
            if (!configLoaded)
            {
                configLoaded = true;
                cachedConfig = Resources.Load<GameConfig>("ScriptableObjects/GameConfig");
            }
            return cachedConfig;
        }

        // ============================================================
        //  日志等级开关
        // ============================================================
        public static bool Enable_LOG
        {
            get => LoadConfig()?.enableLog ?? true;
            set { if (LoadConfig() != null) LoadConfig().enableLog = value; }
        }

        public static bool Enable_Debug_LOG
        {
            get
            {
                var cfg = LoadConfig();
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
                var cfg = LoadConfig();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return cfg?.EnableInfoLog ?? true;
#else
                return cfg?.EnableInfoLog ?? false;
#endif
            }
        }

        public static bool Enable_Warning_LOG
        {
            get => LoadConfig()?.enableWarningLog ?? true;
            set { if (LoadConfig() != null) LoadConfig().enableWarningLog = value; }
        }

        public static bool Enable_Error_LOG
        {
            get => LoadConfig()?.enableErrorLog ?? true;
            set { if (LoadConfig() != null) LoadConfig().enableErrorLog = value; }
        }

        public static bool Enable_Fatal_LOG
        {
            get => LoadConfig()?.enableFatalLog ?? true;
            set { if (LoadConfig() != null) LoadConfig().enableFatalLog = value; }
        }

        // ============================================================
        //  日志频道开关
        // ============================================================
        private static bool Channel(LogChannel channel, bool defaultValue)
        {
            var cfg = LoadConfig();
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
            set { var c = LoadConfig(); if (c != null) c.channelGeneral = value; }
        }
        public static bool Enable_Channel_Network
        {
            get => Channel(LogChannel.Network, true);
            set { var c = LoadConfig(); if (c != null) c.channelNetwork = value; }
        }
        public static bool Enable_Channel_UI
        {
            get => Channel(LogChannel.UI, true);
            set { var c = LoadConfig(); if (c != null) c.channelUI = value; }
        }
        public static bool Enable_Channel_AI
        {
            get => Channel(LogChannel.AI, true);
            set { var c = LoadConfig(); if (c != null) c.channelAI = value; }
        }
        public static bool Enable_Channel_Audio
        {
            get => Channel(LogChannel.Audio, true);
            set { var c = LoadConfig(); if (c != null) c.channelAudio = value; }
        }
        public static bool Enable_Channel_Input
        {
            get => Channel(LogChannel.Input, true);
            set { var c = LoadConfig(); if (c != null) c.channelInput = value; }
        }
        public static bool Enable_Channel_Scene
        {
            get => Channel(LogChannel.Scene, true);
            set { var c = LoadConfig(); if (c != null) c.channelScene = value; }
        }
        public static bool Enable_Channel_Resource
        {
            get => Channel(LogChannel.Resource, true);
            set { var c = LoadConfig(); if (c != null) c.channelResource = value; }
        }
        public static bool Enable_Channel_Custom1
        {
            get => Channel(LogChannel.Custom1, true);
            set { var c = LoadConfig(); if (c != null) c.channelCustom1 = value; }
        }
        public static bool Enable_Channel_Custom2
        {
            get => Channel(LogChannel.Custom2, true);
            set { var c = LoadConfig(); if (c != null) c.channelCustom2 = value; }
        }
        public static bool Enable_Channel_Custom3
        {
            get => Channel(LogChannel.Custom3, true);
            set { var c = LoadConfig(); if (c != null) c.channelCustom3 = value; }
        }

        /// <summary>检查指定频道是否开启。</summary>
        public static bool IsChannelEnabled(LogChannel channel)
        {
            return Channel(channel, true);
        }
    }
}
