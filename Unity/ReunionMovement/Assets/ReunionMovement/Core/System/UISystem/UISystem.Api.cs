using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement.Core.UI
{
    public partial class UISystem
    {
        #region 公共方法 判断
        /// <summary>
        /// 是否被加载了
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsLoad(string name)
        {
            if (uiStateCache.ContainsKey(name))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 是否已打开
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [Obsolete("改用 IsOpen<TWindow>()", false)]
        public bool IsOpen(string name)
        {
            UIController uiBase = GetUIBase(name);
            return uiBase == null ? false : uiBase.gameObject.activeSelf;
        }

        /// <summary>
        /// 是否存在任意已打开的窗口（零分配，供高频输入路径使用）
        /// </summary>
        public bool HasAnyOpenWindow()
        {
            foreach (var kv in uiStateCache)
            {
                var uiBase = kv.Value?.uiWindow;
                if (uiBase != null && uiBase.gameObject.activeSelf)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断指定类型窗口是否已打开
        /// </summary>
        public bool IsOpen<T>() where T : UIController
        {
            string uiName = typeof(T).Name;
            return IsOpen(uiName);
        }

        /// <summary>
        /// 判断窗口是否存在且可见
        /// </summary>
        /// <param name="uiName"></param>
        /// <returns></returns>
        public bool IsWindowVisible(string uiName)
        {
            var uiBase = GetUIBase(uiName);
            return uiBase != null && uiBase.IsVisiable;
        }
        #endregion

        #region 公共方法 Get
        /// <summary>
        /// 获取所有已打开窗口的名称
        /// </summary>
        public List<string> GetAllOpenWindowNames()
        {
            List<string> openNames = new List<string>();
            GetAllOpenWindowNames(openNames);
            return openNames;
        }

        /// <summary>
        /// 获取所有已打开窗口的名称（使用外部 List 避免分配）
        /// </summary>
        public void GetAllOpenWindowNames(List<string> result)
        {
            result.Clear();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key))
                    result.Add(kv.Key);
            }
        }

        /// <summary>
        /// 获取所有已打开窗口的UIController实例
        /// </summary>
        public List<UIController> GetAllOpenWindows()
        {
            List<UIController> openWindows = new List<UIController>();
            GetAllOpenWindows(openWindows);
            return openWindows;
        }

        /// <summary>
        /// 获取所有已打开窗口的UIController实例（使用外部 List 避免分配）
        /// </summary>
        public void GetAllOpenWindows(List<UIController> result)
        {
            result.Clear();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key) && kv.Value.uiWindow != null)
                    result.Add(kv.Value.uiWindow);
            }
        }

        /// <summary>
        /// 根据名称模糊查找窗口
        /// </summary>
        public List<string> FindWindowsByName(string partialName)
        {
            var result = new List<string>();
            FindWindowsByName(partialName, result);
            return result;
        }

        /// <summary>
        /// 根据名称模糊查找窗口（使用外部 List 避免分配）
        /// </summary>
        public void FindWindowsByName(string partialName, List<string> result)
        {
            result.Clear();
            foreach (var key in uiStateCache.Keys)
            {
                if (key.Contains(partialName))
                    result.Add(key);
            }
        }

        /// <summary>
        /// 获取指定类型的所有窗口名称
        /// </summary>
        public List<string> GetWindowNamesByPanelType(PanelType panelType)
        {
            var result = new List<string>();
            GetWindowNamesByPanelType(panelType, result);
            return result;
        }

        /// <summary>
        /// 获取指定类型的所有窗口名称（使用外部 List 避免分配）
        /// </summary>
        public void GetWindowNamesByPanelType(PanelType panelType, List<string> result)
        {
            result.Clear();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.panelType == panelType)
                    result.Add(kv.Key);
            }
        }

        /// <summary>
        /// 获取UI控制器
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>

        private UIController GetUIBase(string name)
        {
            return uiStateCache.TryGetValue(name, out var uiState) ? uiState.uiWindow : null;
        }
        #endregion

        #region 公共方法 Set
        /// <summary>
        /// 切换 - 打开的隐藏，隐藏的打开
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="args"></param>
        public void ToggleWindow<T>(params object[] args)
        {
            string uiName = typeof(T).Name;
            ToggleWindow(uiName, args);
        }

        /// <summary>
        /// 切换 - 打开的隐藏，隐藏的打开
        /// </summary>
        /// <param name="name"></param>
        /// <param name="args"></param>
        public void ToggleWindow(string name, params object[] args)
        {
            if (IsOpen(name))
            {
                CloseWindow(name);
            }
            else
            {
                OpenWindow(name, args);
            }
        }

        /// <summary>
        /// 根据UI名称设置窗口优先级，并重新排序
        /// </summary>
        public void SetWindowPriority(string uiName, int priority)
        {
            var ui = GetUIBase(uiName);
            if (ui != null && ui.transform.parent != null)
            {
                ui.priority = priority;
                // 获取同级所有 UIController 并按优先级排序（避免 LINQ 分配；复用成员缓冲数组，零分配）
                var parent = ui.transform.parent;
                int childCount = parent.childCount;
                if (prioritySortBuffer == null || prioritySortBuffer.Length < childCount)
                {
                    prioritySortBuffer = new UIController[childCount];
                }
                var controllers = prioritySortBuffer;
                int count = 0;
                for (int i = 0; i < childCount; i++)
                {
                    var ctrl = parent.GetChild(i).GetComponent<UIController>();
                    if (ctrl != null)
                    {
                        controllers[count++] = ctrl;
                    }
                }
                // Array.Sort：子节点数量少时开销可忽略，比手写冒泡更清晰且免分配
                Array.Sort(controllers, 0, count, UIControllerPriorityComparer.Instance);
                for (int i = 0; i < count; i++)
                {
                    controllers[i].transform.SetSiblingIndex(i);
                }
                // 清理末尾引用，避免缓冲数组长期持有已销毁窗口
                for (int i = count; i < controllers.Length; i++)
                {
                    controllers[i] = null;
                }
            }
        }

        /// <summary>SetWindowPriority 排序比较器（静态单例，免每调用分配闭包/对象）</summary>
        private sealed class UIControllerPriorityComparer : IComparer<UIController>
        {
            public static readonly UIControllerPriorityComparer Instance = new UIControllerPriorityComparer();
            public int Compare(UIController x, UIController y)
            {
                if (x == null) return y == null ? 0 : 1;
                if (y == null) return -1;
                return x.priority.CompareTo(y.priority);
            }
        }

        #endregion
    }
}
