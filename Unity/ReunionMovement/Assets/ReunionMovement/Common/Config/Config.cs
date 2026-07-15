using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement
{
    /// <summary>
    /// 配置类 用于存放文件路径、常量等
    /// </summary>
    public static class Config
    {
        // 本地路径
        public const string JsonPath = "AutoDatabase/";
        public const string UIPath = "Prefabs/UIs/";
        // UI Toolkit 资源路径（相对于 Resources 文件夹）
        public const string UIToolkitUxmlPath = "UI/UIToolkit/";
        public const string UIToolkitUssPath = "UI/UIToolkit/Styles/";

        // ============================================================
        //  日志等级开关（Debug/Info 在 Release 构建中默认关闭以减少开销）
        // ============================================================
        public static bool Enable_LOG = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool Enable_Debug_LOG = true;
        public static bool Enable_Info_LOG = true;
#else
        public static bool Enable_Debug_LOG = false;
        public static bool Enable_Info_LOG = false;
#endif
        public static bool Enable_Warning_LOG = true;
        public static bool Enable_Error_LOG = true;
        public static bool Enable_Fatal_LOG = true;

        // ============================================================
        //  日志频道开关（可按子系统过滤日志）
        // ============================================================
        public static bool Enable_Channel_General  = true;
        public static bool Enable_Channel_Network  = true;
        public static bool Enable_Channel_UI       = true;
        public static bool Enable_Channel_AI       = true;
        public static bool Enable_Channel_Audio    = true;
        public static bool Enable_Channel_Input    = true;
        public static bool Enable_Channel_Scene    = true;
        public static bool Enable_Channel_Resource = true;
        public static bool Enable_Channel_Custom1  = true;
        public static bool Enable_Channel_Custom2  = true;
        public static bool Enable_Channel_Custom3  = true;

        /// <summary>
        /// 检查指定频道是否开启。
        /// </summary>
        public static bool IsChannelEnabled(LogChannel channel)
        {
            switch (channel)
            {
                case LogChannel.General:  return Enable_Channel_General;
                case LogChannel.Network:  return Enable_Channel_Network;
                case LogChannel.UI:       return Enable_Channel_UI;
                case LogChannel.AI:       return Enable_Channel_AI;
                case LogChannel.Audio:    return Enable_Channel_Audio;
                case LogChannel.Input:    return Enable_Channel_Input;
                case LogChannel.Scene:    return Enable_Channel_Scene;
                case LogChannel.Resource: return Enable_Channel_Resource;
                case LogChannel.Custom1:  return Enable_Channel_Custom1;
                case LogChannel.Custom2:  return Enable_Channel_Custom2;
                case LogChannel.Custom3:  return Enable_Channel_Custom3;
                default:                  return true;
            }
        }
    }
}