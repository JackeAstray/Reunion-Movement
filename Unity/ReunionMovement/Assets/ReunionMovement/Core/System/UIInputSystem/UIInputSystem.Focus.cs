using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.UI;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UIInput
{
    public partial class UIInputSystem
    {
        #region 焦点管理

        /// <summary>
        /// 注册 UI 窗口的默认首选项
        /// </summary>
        /// <param name="uiName">UI 窗口名称</param>
        /// <param name="firstSelected">默认选中的 GameObject</param>
        public void RegisterFirstSelected(string uiName, GameObject firstSelected)
        {
            if (firstSelected == null) return;
            firstSelectedRegistry[uiName] = firstSelected;
        }

        /// <summary>
        /// 取消注册 UI 窗口的默认首选项
        /// </summary>
        public void UnregisterFirstSelected(string uiName)
        {
            firstSelectedRegistry.Remove(uiName);
        }

        /// <summary>
        /// 将焦点压入栈并设置为当前选中（包装方法，包含边界检查）。
        /// 注意：压入的是【当前选中】的旧焦点，开窗→设新焦点只入栈一次，
        /// PopFocus 弹出并恢复该旧焦点，保证 Push/Pop 严格配对。
        /// </summary>
        public void PushFocus(GameObject go)
        {
            if (eventSystem == null) return;

            // 检查目标是否可选
            if (go != null)
            {
                var selectable = go.GetComponent<UnityEngine.UI.Selectable>();
                if (selectable == null || !selectable.interactable) return;
            }

            // 幂等：目标与当前一致时不重复压栈
            if (go == CurrentSelected) return;

            // 先压入旧焦点，再设置新焦点
            if (CurrentSelected != null)
            {
                focusStack.Push(CurrentSelected);
            }

            eventSystem.SetSelectedGameObject(go);
            CurrentSelected = go;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (focusStack.Count > 32)
            {
                Log.Warning("UIInputSystem: 焦点栈深度异常 ({0})，可能存在 Push/Pop 不对称调用", focusStack.Count);
            }
#endif
        }

        /// <summary>
        /// 弹出当前焦点并恢复到上一个（包装方法，包含边界检查）
        /// </summary>
        public void PopFocus()
        {
            if (focusStack.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Warning("UIInputSystem: PopFocus 调用时焦点栈为空，可能存在不对称的 Push/Pop");
#endif
                return;
            }

            // 弹出的是开窗时压入的旧焦点
            GameObject restoreTarget = focusStack.Pop();

            // 旧焦点可能已销毁/失活，向下找最近的有效焦点
            while (restoreTarget != null && !restoreTarget.activeInHierarchy && focusStack.Count > 0)
            {
                restoreTarget = focusStack.Pop();
            }

            if (restoreTarget != null && restoreTarget.activeInHierarchy)
            {
                SetEventSystemSelection(restoreTarget);
                CurrentSelected = restoreTarget;
            }
            else if (LastSelected != null && LastSelected.activeInHierarchy)
            {
                SetEventSystemSelection(LastSelected);
                CurrentSelected = LastSelected;
            }
            else
            {
                SetEventSystemSelection(null);
                CurrentSelected = null;
            }
        }

        /// <summary>设置 EventSystem 焦点；使用 Unity 的 == 判空以识别已销毁对象。</summary>
        private void SetEventSystemSelection(GameObject go)
        {
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(go);
            }
        }

        /// <summary>
        /// 手动设置当前选中的 GameObject（压入当前旧焦点后设置新焦点）
        /// </summary>
        public void SetSelectedGameObject(GameObject go)
        {
            PushFocus(go);
        }

        /// <summary>
        /// 恢复上一个焦点（弹窗关闭时调用）
        /// </summary>
        public void RestorePreviousFocus()
        {
            PopFocus();
        }

        /// <summary>
        /// 清除当前焦点（取消所有选中）
        /// </summary>
        public void ClearFocus()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (focusStack.Count > 0)
            {
                Log.Debug("UIInputSystem: ClearFocus 清除了 {0} 个焦点记录", focusStack.Count);
            }
#endif
            focusStack.Clear();
            SetEventSystemSelection(null);
            CurrentSelected = null;
        }

        /// <summary>
        /// 获取当前选中 GameObject 上的 Selectable
        /// </summary>
        public UnityEngine.UI.Selectable GetCurrentSelectable()
        {
            return CurrentSelected?.GetComponent<UnityEngine.UI.Selectable>();
        }

        #endregion
    }
}
