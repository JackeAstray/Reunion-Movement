using UnityEngine;
using Gear.Framework.Runtime.Scene;

namespace Gear.Framework.Runtime.UI
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
        /// 界面尺寸档位（预留字段）。
        /// 注意：当前没有任何运行时消费者，不会自动关闭 Main Camera 或切换渲染；
        /// 需要全屏档请在业务层自行处理。保留字段是为了不破坏既有预制体序列化。
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