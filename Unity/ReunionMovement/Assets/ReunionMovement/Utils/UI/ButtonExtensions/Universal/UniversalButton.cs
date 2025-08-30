using ReunionMovement.Common;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReunionMovement.UI.ButtonClick
{
    /// <summary>
    /// 支持鼠标、键盘、手柄输入的 Button 扩展，并可在点击后切换 UI（激活/禁用指定 GameObject 并设置选中项）。
    /// 使用 Unity Input System（Gamepad / Keyboard）。
    /// </summary>
    public class UniversalButton : Button
    {
        [Header("输入支持")]
        [Tooltip("是否通过 Update 手动监听键盘/手柄的按键（用于在没有 InputModule 或需要额外支持时）")]
        [SerializeField]
        private bool manualInputSupport = true;

        [Tooltip("当通过手柄/键盘/鼠标触发时，用于防止与 EventSystem 重复触发的短时冷却（秒）")]
        [SerializeField]
        private float invokeCooldown = 0.12f;

        [Header("点击后切换 UI（可选）")]
        [Tooltip("点击后需要激活的 GameObject（例如目标面板）")]
        [SerializeField]
        private GameObject[] activateOnClick = new GameObject[0];

        [Tooltip("点击后需要禁用的 GameObject（例如当前面板）")]
        [SerializeField]
        private GameObject[] deactivateOnClick = new GameObject[0];

        [Tooltip("点击后希望被选中的 UI 元素（通过 EventSystem.SetSelectedGameObject）")]
        [SerializeField]
        private GameObject selectAfterSwitch;

        [Tooltip("是否在点击后自动执行 UI 切换逻辑")]
        [SerializeField]
        private bool switchUiOnClick = false;

        private float lastInvokeTime = -10f;

        private void Update()
        {
            if (!manualInputSupport) return;
            if (EventSystem.current == null) return;

            // 只有当当前按钮为选中项时才响应键盘/手柄提交（与导航一致）
            bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;
            if (!isSelected) return;

            // 键盘支持
            if (Keyboard.current != null)
            {
                var space = Keyboard.current.spaceKey;
                var enter = Keyboard.current.enterKey;

                if (space.wasReleasedThisFrame || enter.wasReleasedThisFrame)
                {
                    TryInvokeFromManualInput();
                }
            }

            // 手柄支持（常用的 A / Cross 按钮）
            if (Gamepad.current != null)
            {
                var a = Gamepad.current.buttonSouth; // A / Cross
                if (a.wasReleasedThisFrame)
                {
                    TryInvokeFromManualInput();
                }
            }
        }

        /// <summary>
        /// 避免与 EventSystem 的 OnSubmit 重复触发：当手动触发时记录时间，OnSubmit 会检测冷却。
        /// </summary>
        private void TryInvokeFromManualInput()
        {
            if (!IsActive() || !IsInteractable()) return;

            float now = Time.unscaledTime;
            if (now - lastInvokeTime < invokeCooldown) return;

            // 记录时间防止重复
            lastInvokeTime = now;

            Log.Debug($"[UniversalButton] 手动输入触发点击：{name}");
            // 直接触发 Button 的 onClick 事件
            onClick?.Invoke();

            if (switchUiOnClick)
            {
                DoSwitchUI();
            }
        }

        /// <summary>
        /// 当 EventSystem 通过标准提交路径触发时，避免与手动触发重复。
        /// </summary>
        public override void OnSubmit(BaseEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - lastInvokeTime < invokeCooldown)
            {
                // 已经被手动触发过，忽略此次系统提交
                eventData.Use();
                return;
            }

            base.OnSubmit(eventData); // 这会调用 onClick
            lastInvokeTime = now;

            if (switchUiOnClick)
            {
                DoSwitchUI();
            }
        }

        /// <summary>
        /// 鼠标点击（PointerClick）由 Button 自身处理，覆盖以支持 UI 切换逻辑。
        /// </summary>
        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData); // 会触发 onClick
            lastInvokeTime = Time.unscaledTime;

            if (switchUiOnClick)
            {
                DoSwitchUI();
            }
        }

        /// <summary>
        /// 执行激活/禁用面板并设置选中项
        /// </summary>
        private void DoSwitchUI()
        {
            try
            {
                // 激活目标
                if (activateOnClick != null)
                {
                    foreach (var go in activateOnClick)
                    {
                        if (go == null) continue;
                        go.SetActive(true);
                        Log.Debug($"[UniversalButton] Activate: {go.name}");
                    }
                }

                // 禁用源或其他
                if (deactivateOnClick != null)
                {
                    foreach (var go in deactivateOnClick)
                    {
                        if (go == null) continue;
                        go.SetActive(false);
                        Log.Debug($"[UniversalButton] Deactivate: {go.name}");
                    }
                }

                // 设置选中项（延迟一帧更可靠）
                if (selectAfterSwitch != null && EventSystem.current != null)
                {
                    // 直接设置，若需要更稳妥可使用协程延迟一帧
                    EventSystem.current.SetSelectedGameObject(selectAfterSwitch);
                    Log.Debug($"[UniversalButton] SetSelected: {selectAfterSwitch.name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // 重置防重复时间，避免被禁用后立刻再次触发影响逻辑
            lastInvokeTime = -10f;
        }
    }
}