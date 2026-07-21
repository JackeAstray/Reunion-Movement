using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement
{
    /// <summary>
    /// 游戏配置 ScriptableObject —— 可在 Inspector 中配置日志开关、频道过滤、路径等。
    /// 放置于 Resources/ScriptableObjects/ 目录下，运行时由 Config 自动加载。
    /// 创建方式：Assets → Create → ScriptableObjects → GameConfig
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/GameConfig", order = 0)]
    public class GameConfig : ScriptableObject
    {
        [Header("资源路径")]
        public string jsonPath = "AutoDatabase/";
        public string uiPath = "Prefabs/UIs/";
        public string uiToolkitUxmlPath = "UI/UIToolkit/";
        public string uiToolkitUssPath = "UI/UIToolkit/Styles/";

        [Header("日志等级")]
        public bool enableLog = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private bool enableDebugLog = true;
        [SerializeField] private bool enableInfoLog = true;
#else
        [SerializeField] private bool enableDebugLog = false;
        [SerializeField] private bool enableInfoLog = false;
#endif

        public bool enableWarningLog = true;
        public bool enableErrorLog = true;
        public bool enableFatalLog = true;

        [Header("日志频道")]
        public bool channelGeneral  = true;
        public bool channelNetwork  = true;
        public bool channelUI       = true;
        public bool channelAI       = true;
        public bool channelAudio    = true;
        public bool channelInput    = true;
        public bool channelScene    = true;
        public bool channelResource = true;
        public bool channelCustom1  = true;
        public bool channelCustom2  = true;
        public bool channelCustom3  = true;

        // 公开属性以便 Config 读取
        public bool EnableDebugLog => enableDebugLog;
        public bool EnableInfoLog => enableInfoLog;
    }
}
