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
        #region 按键重绑定（运行时自定义按键）

        /// <summary>
        /// 开始按键重绑定流程
        /// </summary>
        /// <param name="actionName">Action 名称（如 "Navigate"、"Submit"、"Cancel"）</param>
        /// <param name="onComplete">重绑定完成回调（参数：新按键显示名称）</param>
        /// <param name="onCancel">取消回调</param>
        public void StartRebind(string actionName, Action<string> onComplete, Action onCancel = null)
        {
            StartRebind(actionName, null, onComplete, onCancel);
        }

        /// <summary>
        /// 开始按键重绑定流程，并限定只接受指定设备上的按键（目前用于 Toggle/Menu 菜单键）。
        /// </summary>
        public void StartRebind(string actionName, InputDevice device, Action<string> onComplete, Action onCancel = null)
        {
            if (string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "Menu", StringComparison.OrdinalIgnoreCase))
            {
                StartDeviceToggleRebind(device, onComplete, onCancel);
                return;
            }

            if (device != null)
            {
                Log.Warning("UIInputSystem: StartRebind 的 device 参数目前只支持 Toggle/Menu；{0} 将按全局重绑定处理。", actionName);
            }

            if (inputActions == null || isRebinding)
            {
                onCancel?.Invoke();
                return;
            }

            var action = inputActions.FindAction($"UI/{actionName}");
            if (action == null)
            {
                Log.Warning("UIInputSystem: 找不到 UI/{0} Action", actionName);
                onCancel?.Invoke();
                return;
            }

            // Navigate 是 2D Vector composite（含 up/down/left/right 四个 part），
            // 单次交互式重绑定无法确定捕获的按键属于哪个方向，
            // 若强行使用会只写入 navigateUp 造成方向键设置混乱。
            // 拒绝并引导调用方使用按方向的 SetBinding。
            if (string.Equals(actionName, "Navigate", StringComparison.OrdinalIgnoreCase) &&
                action.expectedControlType == "Vector2")
            {
                Log.Warning("UIInputSystem: Navigate 为 2D Vector 复合动作，不支持交互式重绑定。" +
                            "请改用 SetBinding(\"NavigateUp\"/\"NavigateDown\"/\"NavigateLeft\"/\"NavigateRight\", key)。");
                onCancel?.Invoke();
                return;
            }

            // PerformInteractiveRebinding 要求目标 Action 处于禁用状态
            if (action.enabled)
            {
                action.Disable();
            }

            isRebinding = true;

            // 幂等包装：操作自身 OnCancel 与外部 CancelRebind 都可能触发，保证用户 onCancel 恰好一次
            bool cancelInvoked = false;
            Action guardedCancel = () =>
            {
                if (cancelInvoked) return;
                cancelInvoked = true;
                onCancel?.Invoke();
            };
            activeRebindCancelCallback = guardedCancel;

            // 交互式重绑定：允许键盘、手柄、赛车方向盘、飞行摇杆（方向盘通过 Joystick 设备上报）；
            // 仅排除鼠标/触屏/XR 等不适合做 UI 动作的设备。
            var rebindOperation = action.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>/")
                .WithControlsExcluding("<XRController>/")
                .WithControlsExcluding("<Touchscreen>/")
                .WithControlsExcluding("<Pen>/")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    activeRebindCancelCallback = null;

                    var control = operation.selectedControl;
                    var newKey = control.path;
                    var keyName = InputControlPath.ToHumanReadableString(
                        newKey,
                        InputControlPath.HumanReadableStringOptions.OmitDevice |
                        InputControlPath.HumanReadableStringOptions.UseShortNames);

                    if (control.device is Joystick joystick)
                    {
                        // 方向盘/飞行摇杆按设备 product 保存，避免影响其他控制器
                        if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase))
                        {
                            UIInputDeviceRegistry.SetOverride(joystick, actionName, newKey, keyName);
                        }
                        else
                        {
                            UpdateBindingFromRebind(actionName, newKey, keyName);
                        }
                    }
                    else if (control.device is Gamepad)
                    {
                        // 手柄使用独立字段，不再和键盘共用 submit/cancel
                        if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentBinding.gamepadSubmit = newKey;
                            CurrentBinding.gamepadSubmitDisplayName = keyName;
                        }
                        else if (string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentBinding.gamepadCancel = newKey;
                            CurrentBinding.gamepadCancelDisplayName = keyName;
                        }
                        else
                        {
                            UpdateBindingFromRebind(actionName, newKey, keyName);
                        }
                    }
                    else
                    {
                        UpdateBindingFromRebind(actionName, newKey, keyName);
                    }

                    // 保存绑定
                    SaveBindings();

                    onComplete?.Invoke(keyName);
                    // Clear() 可能已 Dispose 并置 null Subject，判空避免 NRE
                    BindingChangedSubject?.OnNext(CurrentBinding);

                    operation.Dispose();
                })
                .OnCancel(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    // 通过幂等包装调用用户取消回调（先取出并清空，防重入）
                    var cancelCb = activeRebindCancelCallback;
                    activeRebindCancelCallback = null;
                    cancelCb?.Invoke();
                    operation.Dispose();
                });

            activeRebindOperation = rebindOperation;
            rebindOperation.Start();
        }

        /// <summary>
        /// 取消当前重绑定（真正 Dispose 底层 operation，停止监听按键）。
        /// 注意：部分 Unity 版本 Dispose 进行中的 operation 不会触发 OnCancel 回调，
        /// 因此这里在 Dispose 后兜底调用取消回调 —— guardedCancel 幂等守卫保证恰好一次。
        /// </summary>
        public void CancelRebind()
        {
            var operation = activeRebindOperation;
            activeRebindOperation = null;
            var cancelCb = activeRebindCancelCallback;
            activeRebindCancelCallback = null;
            isRebinding = false;
            inputSuppressedUntil = Time.unscaledTime + 0.15f;

            operation?.Dispose();
            cancelCb?.Invoke();
        }

        /// <summary>
        /// 重绑定“打开/关闭菜单”键。device 为 null 时接受任意设备；指定设备时只接受该设备上的按键。
        /// 方向盘/飞行摇杆按设备持久化；手柄/键盘写入全局菜单键。
        /// </summary>
        public void StartDeviceToggleRebind(InputDevice device, Action<string> onComplete, Action onCancel = null)
        {
            if (inputActions == null || isRebinding)
            {
                onCancel?.Invoke();
                return;
            }

            // 借用 UI/Submit 作为“捕获任意按钮”的探针，但不允许它修改 Submit 绑定。
            var captureAction = inputActions.FindAction("UI/Submit");
            if (captureAction == null)
            {
                Log.Warning("UIInputSystem: 找不到用于捕获菜单键的 UI/Submit Action");
                onCancel?.Invoke();
                return;
            }

            // PerformInteractiveRebinding 要求目标 Action 处于禁用状态
            if (captureAction.enabled)
            {
                captureAction.Disable();
            }

            isRebinding = true;
            bool cancelInvoked = false;
            Action guardedCancel = () =>
            {
                if (cancelInvoked) return;
                cancelInvoked = true;
                onCancel?.Invoke();
            };
            activeRebindCancelCallback = guardedCancel;

            var rebindOperation = captureAction.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>/")
                .WithControlsExcluding("<XRController>/")
                .WithControlsExcluding("<Touchscreen>/")
                .WithControlsExcluding("<Pen>/")
                .OnMatchWaitForAnother(0.1f)
                .OnApplyBinding((_, __) => { /* 只捕获，不改 UI/Submit */ })
                .OnComplete(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    activeRebindCancelCallback = null;

                    var control = operation.selectedControl;
                    if (control == null || control.device == null ||
                        (device != null && control.device != device))
                    {
                        guardedCancel();
                        operation.Dispose();
                        return;
                    }

                    string path = control.path;
                    string display = InputControlPath.ToHumanReadableString(
                        path,
                        InputControlPath.HumanReadableStringOptions.OmitDevice |
                        InputControlPath.HumanReadableStringOptions.UseShortNames);

                    if (control.device is Joystick)
                    {
                        UIInputDeviceRegistry.SetOverride(control.device, "Toggle", path, display);
                    }
                    else if (control.device is Gamepad)
                    {
                        CurrentBinding.toggleToUIGamepad = path;
                        CurrentBinding.toggleToUIGamepadDisplayName = display;
                        SaveBindings();
                    }
                    else if (control.device is Keyboard)
                    {
                        string leaf = path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)
                            ? path.Substring(path.LastIndexOf('/') + 1)
                            : path;
                        CurrentBinding.toggleToUI = leaf;
                        CurrentBinding.toggleToUIDisplayName = display;
                        SaveBindings();
                    }
                    else
                    {
                        guardedCancel();
                        operation.Dispose();
                        return;
                    }

                    BindingChangedSubject?.OnNext(CurrentBinding);
                    onComplete?.Invoke(display);
                    operation.Dispose();
                })
                .OnCancel(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    var cancelCb = activeRebindCancelCallback;
                    activeRebindCancelCallback = null;
                    cancelCb?.Invoke();
                    operation.Dispose();
                });

            activeRebindOperation = rebindOperation;
            rebindOperation.Start();
        }

        /// <summary>
        /// 是否正在重绑定
        /// </summary>
        public bool IsRebinding => isRebinding;

        /// <summary>
        /// 根据重绑定结果更新 CurrentBinding
        /// </summary>
        private void UpdateBindingFromRebind(string actionName, string controlPath, string displayName)
        {
            // 键盘按键提取尾段（如 "<Keyboard>/a" → "a"，与 SetBinding/历史存档格式一致）；
            // 非键盘控件（如手柄）保留完整路径（如 "<Gamepad>/buttonSouth"），ApplyBindingOverride 按路径处理
            var keyName = controlPath;
            if (controlPath.Contains("<Keyboard>"))
            {
                var slashIndex = controlPath.LastIndexOf('/');
                if (slashIndex >= 0)
                {
                    keyName = controlPath.Substring(slashIndex + 1);
                }
            }

            switch (actionName)
            {
                case "Submit":
                    CurrentBinding.submit = keyName;
                    CurrentBinding.submitDisplayName = displayName;
                    break;
                case "Cancel":
                    CurrentBinding.cancel = keyName;
                    CurrentBinding.cancelDisplayName = displayName;
                    break;
                case "Navigate":
                    // Navigate 是 2D Vector composite，正常路径已被 StartRebind 拒绝
                    // （交互式重绑定无法确定方向）。此分支仅作防御，保持向后兼容。
                    CurrentBinding.navigateUp = keyName;
                    break;
            }
        }

        /// <summary>
        /// 程序化设置指定操作的自定义按键（不通过交互式重绑定）
        /// </summary>
        public void SetBinding(string actionName, Key key)
        {
            var keyName = key.ToString().ToLower();
            switch (actionName)
            {
                case "Submit":
                    CurrentBinding.submit = keyName;
                    CurrentBinding.submitDisplayName = key.ToString();
                    break;
                case "Cancel":
                    CurrentBinding.cancel = keyName;
                    CurrentBinding.cancelDisplayName = key.ToString();
                    break;
                case "NavigateUp":
                    CurrentBinding.navigateUp = keyName;
                    break;
                case "NavigateDown":
                    CurrentBinding.navigateDown = keyName;
                    break;
                case "NavigateLeft":
                    CurrentBinding.navigateLeft = keyName;
                    break;
                case "NavigateRight":
                    CurrentBinding.navigateRight = keyName;
                    break;
            }

            ApplyBindingsToActions();
            SaveBindings();
            // Clear 后 Subject 为 null，判空避免 NRE（与 StartRebind 回调中的 ?. 一致）
            BindingChangedSubject?.OnNext(CurrentBinding);

            WarnBindingConflicts();
        }

        /// <summary>
        /// 重置所有按键绑定为默认值
        /// </summary>
        public void ResetBindings()
        {
            CurrentBinding = new UIInputBinding();
            UIInputDeviceRegistry.ClearAllOverrides();
            ApplyBindingsToActions();
            SaveBindings();
            BindingChangedSubject?.OnNext(CurrentBinding);
            Log.Debug("UIInputSystem: 按键绑定已重置为默认值（含方向盘/飞行摇杆）");
        }

        /// <summary>
        /// 检测并告警按键冲突：同一按键绑定到多个操作（Submit/Cancel/切换键等）时提示，
        /// 避免玩家按键互相覆盖导致输入行为异常。
        /// </summary>
        private void WarnBindingConflicts()
        {
            // 简易分组：keyName → 绑定该键的操作名列表（低频调用，分配可接受）
            var map = new Dictionary<string, List<string>>();
            void Register(string keyName, string actionName)
            {
                if (string.IsNullOrEmpty(keyName)) return;
                if (!map.TryGetValue(keyName, out var list))
                {
                    list = new List<string>(2);
                    map[keyName] = list;
                }
                list.Add(actionName);
            }

            // 键盘/手柄/摇杆各自独立命名空间，避免 Enter 与 buttonSouth 被误判为冲突
            Register("K:" + CurrentBinding.submit, "Keyboard.Submit");
            Register("K:" + CurrentBinding.cancel, "Keyboard.Cancel");
            Register("K:" + CurrentBinding.toggleToUI, "Keyboard.Toggle");
            Register("K:" + CurrentBinding.toggleToGameplay, "Keyboard.ToggleGameplay");
            Register("G:" + CurrentBinding.gamepadSubmit, "Gamepad.Submit");
            Register("G:" + CurrentBinding.gamepadCancel, "Gamepad.Cancel");
            Register("G:" + CurrentBinding.toggleToUIGamepad, "Gamepad.Toggle");
            Register("J:" + CurrentBinding.toggleToUIJoystick, "Joystick.Toggle");

            foreach (var kv in map)
            {
                if (kv.Value.Count > 1)
                {
                    Log.Warning("UIInputSystem: 按键 [{0}] 同时绑定到 {1}，存在冲突，可能导致输入行为异常",
                        kv.Key, string.Join("/", kv.Value));
                }
            }
        }

        #endregion
    }
}
