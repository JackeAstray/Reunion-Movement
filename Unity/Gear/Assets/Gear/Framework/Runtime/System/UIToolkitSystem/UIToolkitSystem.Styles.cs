using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gear.Framework.Runtime.UIToolkit
{
    public partial class UIToolkitSystem
    {
        #region 样式加载

        /// <summary>
        /// 同步加载面板专属 USS 样式表（可选，不存在时不报错）
        /// </summary>
        private void LoadPanelStyle(string panelName, VisualElement root)
        {
            // 使用 Resources.Load 直接加载（不存在时返回 null，不打 Error）
            var style = UnityEngine.Resources.Load<StyleSheet>($"{USS_PATH_PREFIX}{panelName}");
            if (style != null)
            {
                root.styleSheets.Add(style);
                panelStyleCache[panelName] = style;
            }
        }

        /// <summary>
        /// 异步加载面板专属 USS 样式表（可选，不存在时不报错）
        /// </summary>
        private async UniTask LoadPanelStyleAsync(string panelName, VisualElement root)
        {
            // 异步加载，不缓存到 ResourcesSystem（避免缺失时打 Error）。
            // 直接 await ResourceRequest（ToUniTask），避免 WaitUntil 每帧轮询开销
            var request = UnityEngine.Resources.LoadAsync<StyleSheet>($"{USS_PATH_PREFIX}{panelName}");
            await request.ToUniTask();
            var style = request.asset as StyleSheet;
            if (style != null)
            {
                root.styleSheets.Add(style);
                panelStyleCache[panelName] = style;
            }
        }
        #endregion
    }
}
