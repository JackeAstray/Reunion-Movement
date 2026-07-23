using UnityEngine;
using ReunionMovement.Core.Scene;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UIWindowAsset是每个UI界面的窗口资源类，负责管理UI窗口的资源和属性。
    /// </summary>
    public class UIWindowAsset : MonoBehaviour
    {
        public string stringArgument;
        /// <summary>
        /// 界面类型
        /// </summary>
        public PanelType panelType = PanelType.NormalUI;
        /// <summary>
        /// 是否为全屏界面
        /// </summary>
        public PanelSize panelSize = PanelSize.SmallPanel;
        /// <summary>
        /// 切换场景时是否关闭当前界面
        /// </summary>
        public bool isHiddenWhenLeaveScene = true;

        /// <summary>
        /// 界面分组名称
        /// </summary>
        public string groupName = "";

        private void OnEnable()
        {
            // 向 SceneSystem 注册自身，避免场景切换时使用 FindObjectsByType 全场景遍历
            if (SceneSystem.Instance.isInited)
                SceneSystem.Instance.RegisterUIWindow(this);
        }

        private void OnDisable()
        {
            if (SceneSystem.Instance.isInited)
                SceneSystem.Instance.UnregisterUIWindow(this);
        }
    }
}