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
        #region UI 控制模式切换（Gameplay ↔ UIControl）

        /// <summary>
        /// 启用 UI 控制模式
        /// 关闭 Player 输入，启用本系统的键盘/手柄/方向盘/飞行摇杆 UI 导航，并自动聚焦当前打开的 UI 窗口
        /// </summary>
        public void EnableUIControl()
        {
            if (currentMode == UIControlMode.UIControl) return;

            EnsureInputModuleReferences();

            // 记录 Player map 状态（InputActions 加载失败时仍允许使用本系统的设备轮询）
            var playerMap = inputActions != null ? inputActions.FindActionMap("Player") : null;
            if (playerMap != null)
            {
                playerMapWasEnabled = playerMap.enabled;
                playerMap.Disable();
            }

            // 先确保 InputModule 处于启用状态：组件 OnEnable 会重新启用它引用的所有动作，
            // 因此必须在其后关闭 move/submit/cancel，避免重复响应。
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            SetUIActionsEnabled(enablePointerActions: true);

            // 清理上一轮 UI 会话的重复/转向状态，避免按住键进入 UI 时立即多跳一格
            repeatStates.Clear();
            steeringStates.Clear();

            currentMode = UIControlMode.UIControl;

            // 尝试聚焦当前打开的 UI 窗口
            TryFocusCurrentUI();

            Log.Debug("UIInputSystem: 启用 UI 控制模式");
            try { UIControlModeChangedSubject.OnNext(UIControlMode.UIControl); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] UIControlModeChangedSubject 订阅者异常（已隔离）: {0}", ex.Message); }
        }

        /// <summary>
        /// 禁用 UI 控制模式
        /// 恢复 Player 输入，仅保留鼠标/触摸对 UI 的点击能力，并清除 UI 焦点
        /// </summary>
        public void DisableUIControl()
        {
            if (currentMode == UIControlMode.Gameplay) return;

            EnsureInputModuleReferences();

            // 清除 UI 焦点
            ClearFocus();

            // 退出 UI 控制时清空重复状态，避免下次进入时沿用旧的按住计时
            repeatStates.Clear();
            steeringStates.Clear();

            // 保持 InputSystemUIInputModule 激活，使鼠标/触摸仍可驱动 UI。
            // 先启用组件（可能重新启用其动作），再关闭导航/提交/取消。
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            // 保留鼠标/触摸指针事件，避免 Gameplay 模式下 UI 完全不可点击。
            // 仅关闭导航/提交/取消，让键盘/手柄/方向盘不再驱动 UI 焦点。
            SetUIActionsEnabled(enablePointerActions: true);

            // 恢复 Player map
            var playerMap = inputActions != null ? inputActions.FindActionMap("Player") : null;
            if (playerMap != null && playerMapWasEnabled)
            {
                playerMap.Enable();
            }

            currentMode = UIControlMode.Gameplay;

            Log.Debug("UIInputSystem: 禁用 UI 控制模式");
            try { UIControlModeChangedSubject.OnNext(UIControlMode.Gameplay); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] UIControlModeChangedSubject 订阅者异常（已隔离）: {0}", ex.Message); }
        }

        /// <summary>
        /// 在 Gameplay 和 UIControl 模式之间切换
        /// </summary>
        public void ToggleUIControl()
        {
            if (currentMode == UIControlMode.Gameplay)
            {
                EnableUIControl();
            }
            else
            {
                DisableUIControl();
            }
        }

        /// <summary>
        /// 尝试聚焦当前打开的 UI 窗口中的首个可选元素
        /// </summary>
        private void TryFocusCurrentUI()
        {
            if (eventSystem == null) return;

            // 遍历所有已打开的 UI 窗口，按优先级找第一个可聚焦的
            var openWindows = UISystem.Instance?.GetAllOpenWindows();
            if (openWindows != null)
            {
                foreach (var window in openWindows)
                {
                    if (window == null || !window.gameObject.activeInHierarchy) continue;

                    var first = window.firstSelected;
                    if (first == null)
                    {
                        firstSelectedRegistry.TryGetValue(window.uiName, out first);
                    }
                    if (first == null)
                    {
                        first = FindFirstSelectable(window.gameObject);
                    }

                    if (first != null)
                    {
                        eventSystem.SetSelectedGameObject(first);
                        CurrentSelected = first;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 每帧轮询切换键，处理 Gameplay ↔ UI 模式切换
        /// </summary>
        private void PollToggleKeys()
        {
            // 重绑定期间不响应模式切换键
            if (isRebinding || Time.unscaledTime < inputSuppressedUntil) return;

            bool enterUI = currentMode == UIControlMode.Gameplay;

            // 键盘切换键（默认 Tab，进出对称）
            if (Keyboard.current != null)
            {
                var toggleKey = GetCachedKey(ref cachedToggleToUIStr, ref cachedToggleToUIKey, CurrentBinding.toggleToUI);
                if (toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
                {
                    ToggleFromInput(enterUI);
                    return;
                }
            }

            // 所有手柄的 Start（不再只取 Gamepad.current，支持多手柄/多方向盘同时接入）
            var gamepads = Gamepad.all;
            var gamepadButton = GetCachedGamepadButton(ref cachedToggleToUIGamepadStr, ref cachedToggleToUIGamepadButton, CurrentBinding.toggleToUIGamepad);
            for (int i = 0; i < gamepads.Count; i++)
            {
                if (gamepadButton.HasValue && gamepads[i][gamepadButton.Value].wasPressedThisFrame)
                {
                    ToggleFromInput(enterUI);
                    return;
                }

                // 菜单键被重绑为 dpad/trigger 等无法映射到 GamepadButton 的控件时，按路径查找
                if (!gamepadButton.HasValue && !string.IsNullOrEmpty(CurrentBinding.toggleToUIGamepad))
                {
                    var control = InputControlPath.TryFindControl<ButtonControl>(
                        gamepads[i], CurrentBinding.toggleToUIGamepad);
                    if (control != null && control.wasPressedThisFrame)
                    {
                        ToggleFromInput(enterUI);
                        return;
                    }
                }
            }

            // 方向盘/飞行摇杆的 Start（设备有 start 键时生效）
            if (devicesDirty)
            {
                RefreshRoutedDevices();
            }
            for (int i = 0; i < routedDevices.Count; i++)
            {
                if (routedDevices[i] is Joystick joystick)
                {
                    var toggle = GetJoystickToggle(joystick);
                    if (toggle != null && toggle.wasPressedThisFrame)
                    {
                        ToggleFromInput(enterUI);
                        return;
                    }
                }
            }

            if (enterUI) return;

            // 退出键（默认 Escape）：与 Cancel 共键时优先交给窗口/轮播处理，
            // 只有在没有可取消目标时才退出 UI 控制模式。
            if (Keyboard.current != null)
            {
                var exitKey = GetCachedKey(ref cachedToggleToGameplayStr, ref cachedToggleToGameplayKey, CurrentBinding.toggleToGameplay);
                if (exitKey != Key.None && Keyboard.current[exitKey].wasPressedThisFrame)
                {
                    var cancelKey = GetKeyFromName(CurrentBinding.cancel);
                    if (exitKey == cancelKey && (HasOpenUI() || HasActiveNavigationTarget(KeyboardMousePlayerIndex)))
                    {
                        return;
                    }
                    DisableUIControl();
                }
            }
        }

        private void ToggleFromInput(bool enterUI)
        {
            // 菜单键与确认/取消键可能是同一个物理键：切换模式后抑制本帧剩余输入，
            // 避免“按菜单键打开 UI”的同一次按下又确认/关闭了某个选项。
            // 只抑制一帧左右，避免打开 UI 后立刻按方向键被吞掉。
            inputSuppressedUntil = Time.unscaledTime + 0.05f;

            if (enterUI)
            {
                EnableUIControl();
            }
            else
            {
                DisableUIControl();
            }
        }

        private ButtonControl GetJoystickToggle(Joystick joystick)
        {
            if (joystick == null) return null;
            if (UIInputDeviceRegistry.TryGetToggle(joystick, out var toggle)) return toggle;

            // 设备没有 start 时，允许通过全局绑定指定任意按钮
            if (!string.IsNullOrEmpty(CurrentBinding.toggleToUIJoystick))
            {
                return InputControlPath.TryFindControl<ButtonControl>(joystick, CurrentBinding.toggleToUIJoystick);
            }
            return null;
        }

        /// <summary>
        /// 按键名 → Key 的免分配缓存：仅当字符串值变化时才重新解析，
        /// 避免每帧 ToLower().Trim() 产生字符串分配。
        /// </summary>
        private Key GetCachedKey(ref string cachedStr, ref Key cachedKey, string current)
        {
            if (cachedStr != current)
            {
                cachedStr = current;
                cachedKey = GetKeyFromName(current);
            }
            return cachedKey;
        }

        /// <summary>
        /// 将按键名称字符串转换为 Key 枚举值
        /// </summary>
        private Key GetKeyFromName(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return Key.None;

            // 兼容旧存档中的完整路径：<Keyboard>/enter → enter
            if (keyName.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                keyName = keyName.Substring(keyName.LastIndexOf('/') + 1);
            }

            // 常用别名映射
            var normalized = keyName.ToLower().Trim();
            if (normalized == "enter" || normalized == "return") return Key.Enter;
            if (normalized == "escape" || normalized == "esc") return Key.Escape;
            if (normalized == "tab") return Key.Tab;
            if (normalized == "space") return Key.Space;

            // 尝试直接解析
            if (Enum.TryParse<Key>(keyName, true, out var result))
            {
                return result;
            }

            return Key.None;
        }

        /// <summary>
        /// 游戏手柄按键路径（如 "&lt;Gamepad&gt;/start"）→ GamepadButton 的免分配缓存：
        /// 仅当字符串值变化时才重新解析，避免每帧字符串分配。
        /// </summary>
        private GamepadButton? GetCachedGamepadButton(ref string cachedStr, ref GamepadButton? cachedButton, string current)
        {
            if (cachedStr != current)
            {
                cachedStr = current;
                cachedButton = ParseGamepadButton(current);
            }
            return cachedButton;
        }

        /// <summary>
        /// 解析游戏手柄按键完整路径（如 "&lt;Gamepad&gt;/start"）为 GamepadButton；
        /// 支持 "&lt;Gamepad&gt;/xxx" 与裸名 "xxx" 两种写法，无法解析时返回 null。
        /// </summary>
        private GamepadButton? ParseGamepadButton(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.LastIndexOf('/');
            var name = slash >= 0 ? path.Substring(slash + 1) : path;
            if (name.StartsWith("button", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("button".Length);
            }
            if (Enum.TryParse<GamepadButton>(name, true, out var result))
            {
                return result;
            }
            return null;
        }

        #endregion
    }
}
