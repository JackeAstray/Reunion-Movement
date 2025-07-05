using UnityEngine;

namespace ReunionMovement
{
    /// <summary>
    /// 配置类 用于存放文件路径、常量等
    /// </summary>
    public static class Config
    {
        // 本地路径
        public static string JsonPath = "AutoDatabase/";
        public static string UIPath = "Prefabs/UIs/";

        // 资源路径
        public static string AssetBundlePath = "Assets/AssetBundles/";

        // 日志等级
        public static bool Enable_LOG = true;
        public static bool Enable_Debug_LOG = true;
        public static bool Enable_Info_LOG = true;
        public static bool Enable_Warning_LOG = true;
        public static bool Enable_Error_LOG = true;
        public static bool Enable_Fatal_LOG = true;
    }
}