using Astray.Common;
using Astray.Framework.Runtime.Base;
using Astray.Framework.Runtime.Resources;
using Astray.Framework.Runtime.UI;
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

namespace Astray.Framework.Runtime.UIInput
{
    public partial class UIInputSystem
    {
        #region UISystem 生命周期事件处理

        /// <summary>R3 订阅句柄（用于取消订阅）</summary>
        private IDisposable uiOpenSubscription;
        private IDisposable uiCloseSubscription;

        private void RegisterUIEvents()
        {
            // R3 原生订阅方式
            uiOpenSubscription = UISystem.Instance.OnOpenSubject.Subscribe(OnUIOpened);
            uiCloseSubscription = UISystem.Instance.OnCloseSubject.Subscribe(OnUIClosed);
        }

        private void UnregisterUIEvents()
        {
            uiOpenSubscription?.Dispose();
            uiOpenSubscription = null;
            uiCloseSubscription?.Dispose();
            uiCloseSubscription = null;
        }

        /// <summary>
        /// UI 窗口打开时 —— 自动聚焦该窗口注册的首选项
        /// </summary>
        private void OnUIOpened(UIController controller)
        {
            if (controller == null) return;

            // 仅在 UI 模式下才自动聚焦；Gameplay 模式下窗口打开不抢焦点
            if (currentMode != UIControlMode.UIControl) return;

            // 优先从 UIController 获取 firstSelected
            var firstSelected = controller.firstSelected;

            // 如果 UIController 未设置，尝试从注册表获取
            if (firstSelected == null)
            {
                firstSelectedRegistry.TryGetValue(controller.uiName, out firstSelected);
            }

            // 如果仍未找到，自动查找该 UI 下的第一个 Selectable
            if (firstSelected == null)
            {
                firstSelected = FindFirstSelectable(controller.gameObject);
            }

            if (firstSelected != null)
            {
                // PushFocus 内部会压入当前旧焦点并设置新焦点（单次压栈）。
                // 不要在此处再手动 Push，否则每次开窗多压一条，焦点栈无界增长。
                SetSelectedGameObject(firstSelected);
            }
        }

        /// <summary>
        /// UI 窗口关闭时 —— 恢复上一个焦点
        /// </summary>
        private void OnUIClosed(UIController controller)
        {
            // 与 OnUIOpened 对称：仅当窗口是在 UI 模式下打开（压栈过焦点）时才恢复焦点，
            // 避免 Gameplay 模式下关窗误弹栈导致焦点栈错位（Pop 多于 Push）。
            if (currentMode != UIControlMode.UIControl) return;

            // 延迟一帧恢复焦点：当帧窗口尚未完全从 EventSystem 注销，
            // 立即恢复可能被 InputSystemUIInputModule 当帧的导航状态覆写
            if (focusStack.Count > 0)
            {
                RestoreFocusNextFrameAsync().Forget();
            }
        }

        /// <summary>延迟一帧恢复焦点（延迟期间若模式切换/Clear 则放弃恢复）</summary>
        private async UniTaskVoid RestoreFocusNextFrameAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (currentMode != UIControlMode.UIControl) return;
            if (focusStack.Count > 0)
            {
                RestorePreviousFocus();
            }
        }

        /// <summary>
        /// 在 GameObject 及其子对象中查找第一个可交互的 Selectable
        /// </summary>
        private GameObject FindFirstSelectable(GameObject root)
        {
            if (root == null) return null;

            var selectables = root.GetComponentsInChildren<UnityEngine.UI.Selectable>();
            foreach (var s in selectables)
            {
                if (s.interactable && s.gameObject.activeInHierarchy)
                {
                    return s.gameObject;
                }
            }
            return null;
        }

        #endregion
    }
}
