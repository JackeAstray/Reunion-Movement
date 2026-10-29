using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime.Base;
using ReunionMovement.Framework.Runtime.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement.Framework.Runtime.UI
{
    public partial class UISystem
    {
        #region 公共方法 工具

        /// <summary>
        /// 给打开的UI添加脚本（脚本从程序集查找）
        /// </summary>
        /// <param name="uiObj"></param>
        /// <param name="uiTemplateName"></param>
        /// <returns></returns>

        // 类型缓存：避免每次打开 UI 都执行字符串拼接 + 反射
        private static readonly Dictionary<string, Type> uiControllerTypeCache = new Dictionary<string, Type>();

        public virtual UIController CreateUIController(GameObject uiObj, string uiTemplateName)
        {
            // 优先使用源码生成器生成的注册表（编译期扫描，零运行时反射）
            if (UIControllerRegistry.TryGet(uiTemplateName, out Type type))
            {
                return uiObj.AddComponent(type) as UIController;
            }

            // 后备：反射查找（覆盖生成器未覆盖的动态/外部类型）
            if (!uiControllerTypeCache.TryGetValue(uiTemplateName, out type))
            {
                // 在所有已加载程序集中查找类型（兼容多程序集项目）
                string fullName = "ReunionMovement.Framework.Runtime.UI." + uiTemplateName;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(fullName);
                    if (type != null) break;
                }
                uiControllerTypeCache[uiTemplateName] = type;
            }
            if (type == null)
            {
                Log.Error("CreateUIController: 未能找到UI脚本组件 ReunionMovement.Framework.Runtime.UI.{0}！", uiTemplateName);
                return null;
            }
            UIController uiBase = uiObj.AddComponent(type) as UIController;
            return uiBase;
        }

        /// <summary>
        /// 记录操作的耗时
        /// </summary>
        private void LogElapsedTime(Action action, string message)
        {
            float startTime = Time.realtimeSinceStartup;
            action();
            float elapsed = Time.realtimeSinceStartup - startTime;

            Log.Debug("{0}, cost {1}", message, elapsed);
        }

        /// <summary>
        /// 关闭指定类型的所有窗口
        /// </summary>
        /// <param name="panelType"></param>
        public void CloseAllWindowsByPanelType(PanelType panelType)
        {
            var toClose = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.panelType == panelType && IsOpen(kv.Key))
                    toClose.Add(kv.Key);
            }
            foreach (var name in toClose)
            {
                CloseWindow(name);
            }
        }

        /// <summary>
        /// 关闭除指定窗口外的所有窗口
        /// </summary>
        public void CloseAllExcept(params string[] exceptNames)
        {
            HashSet<string> exceptSet = new HashSet<string>(exceptNames);
            // 防御性拷贝：CloseWindow 会修改 uiStateCache，先收集键再遍历
            var keysToClose = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key) && !exceptSet.Contains(kv.Key))
                {
                    keysToClose.Add(kv.Key);
                }
            }
            foreach (var key in keysToClose)
            {
                CloseWindow(key);
            }
        }

        /// <summary>
        /// 将指定窗口置于同层级最前
        /// </summary>
        /// <param name="uiName"></param>
        public void BringToFront(string uiName)
        {
            var uiBase = GetUIBase(uiName);
            if (uiBase != null)
            {
                uiBase.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 隐藏所有窗口（可选按类型）
        /// </summary>
        /// <param name="panelType"></param>
        public void HideAllWindows(PanelType? panelType = null)
        {
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && (panelType == null || kv.Value.uiWindow.WindowAsset.panelType == panelType))
                {
                    kv.Value.uiWindow.IsVisiable = false;
                }
            }
        }

        /// <summary>
        /// 显示所有窗口（可选按类型）
        /// </summary>
        /// <param name="panelType"></param>
        public void ShowAllWindows(PanelType? panelType = null)
        {
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && (panelType == null || kv.Value.uiWindow.WindowAsset.panelType == panelType))
                {
                    kv.Value.uiWindow.IsVisiable = true;
                }
            }
        }

        /// <summary>
        /// 关闭指定组的所有窗口
        /// </summary>
        /// <param name="groupName"></param>
        public void CloseGroup(string groupName)
        {
            var keysToClose = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.groupName == groupName)
                    keysToClose.Add(kv.Key);
            }
            foreach (var key in keysToClose)
            {
                CloseWindow(key);
            }
        }

        #endregion
    }
}
