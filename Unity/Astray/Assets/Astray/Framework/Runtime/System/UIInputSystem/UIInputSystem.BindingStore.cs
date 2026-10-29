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
        #region 按键绑定持久化

        /// <summary>
        /// 从 PlayerPrefs 加载按键绑定
        /// </summary>
        public void LoadBindings()
        {
            // PlayerPrefs 在 WebGL 上同样可用：Unity 的 WebGL 播放器启动时会以 runDependency 阻塞，
            // 等 IndexedDB 载入完成（FS.syncfs(populate:true)）后才运行 C#，故不再按平台跳过。
            CurrentBinding = new UIInputBinding
            {
                navigateUp = PlayerPrefs.GetString("ui_bind_nav_up", "w"),
                navigateDown = PlayerPrefs.GetString("ui_bind_nav_down", "s"),
                navigateLeft = PlayerPrefs.GetString("ui_bind_nav_left", "a"),
                navigateRight = PlayerPrefs.GetString("ui_bind_nav_right", "d"),
                submit = PlayerPrefs.GetString("ui_bind_submit", "enter"),
                submitDisplayName = PlayerPrefs.GetString("ui_bind_submit_display", "Enter"),
                cancel = PlayerPrefs.GetString("ui_bind_cancel", "escape"),
                cancelDisplayName = PlayerPrefs.GetString("ui_bind_cancel_display", "Escape"),
                gamepadSubmit = PlayerPrefs.GetString("ui_bind_gamepad_submit", "<Gamepad>/buttonSouth"),
                gamepadSubmitDisplayName = PlayerPrefs.GetString("ui_bind_gamepad_submit_display", "A / ✕"),
                gamepadCancel = PlayerPrefs.GetString("ui_bind_gamepad_cancel", "<Gamepad>/buttonEast"),
                gamepadCancelDisplayName = PlayerPrefs.GetString("ui_bind_gamepad_cancel_display", "B / ○"),
                toggleToUI = PlayerPrefs.GetString("ui_bind_toggle_ui", "tab"),
                toggleToUIDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_display", "Tab"),
                toggleToUIGamepad = PlayerPrefs.GetString("ui_bind_toggle_ui_gamepad", "<Gamepad>/start"),
                toggleToUIGamepadDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_gamepad_display", "Start"),
                toggleToUIJoystick = PlayerPrefs.GetString("ui_bind_toggle_ui_joystick", "<Joystick>/start"),
                toggleToUIJoystickDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_joystick_display", "Start"),
                toggleToGameplay = PlayerPrefs.GetString("ui_bind_toggle_gameplay", "escape"),
                navigateDeadzone = PlayerPrefs.GetFloat("ui_bind_nav_deadzone", 0.5f),
                navigateHoldDelay = PlayerPrefs.GetFloat("ui_bind_nav_hold_delay", 0.4f),
                navigateRepeatRate = PlayerPrefs.GetFloat("ui_bind_nav_repeat_rate", 0.1f),
                mouseRightClickAsCancel = PlayerPrefs.GetInt("ui_bind_mouse_right_cancel", 0) != 0,
                wheelSteeringNavigation = PlayerPrefs.GetInt("ui_bind_wheel_steering_nav", 1) != 0,
                wheelSteeringInvert = PlayerPrefs.GetInt("ui_bind_wheel_steering_invert", 0) != 0,
                wheelSteeringDeadzone = PlayerPrefs.GetFloat("ui_bind_wheel_steering_deadzone", 0.25f),
                wheelSteeringReleaseThreshold = PlayerPrefs.GetFloat("ui_bind_wheel_steering_release", 0.12f),
                wheelSteeringHoldDelay = PlayerPrefs.GetFloat("ui_bind_wheel_steering_hold", 0.6f),
                wheelSteeringRepeatRate = PlayerPrefs.GetFloat("ui_bind_wheel_steering_repeat", 0.2f),
            };

            // 旧版本把键盘和手柄的确认/取消混在同一个字段里：检测到旧存档是手柄路径时迁移到手柄字段
            MigrateLegacyGamepadBinding(
                ref CurrentBinding.submit, ref CurrentBinding.submitDisplayName,
                ref CurrentBinding.gamepadSubmit, ref CurrentBinding.gamepadSubmitDisplayName,
                "enter", "Enter");
            MigrateLegacyGamepadBinding(
                ref CurrentBinding.cancel, ref CurrentBinding.cancelDisplayName,
                ref CurrentBinding.gamepadCancel, ref CurrentBinding.gamepadCancelDisplayName,
                "escape", "Escape");
        }

        private static void MigrateLegacyGamepadBinding(
            ref string keyboardValue, ref string keyboardDisplay,
            ref string gamepadValue, ref string gamepadDisplay,
            string defaultKey, string defaultDisplay)
        {
            if (!LooksLikeGamepadBinding(keyboardValue)) return;

            gamepadValue = keyboardValue;
            gamepadDisplay = keyboardDisplay;
            keyboardValue = defaultKey;
            keyboardDisplay = defaultDisplay;
        }

        private static bool LooksLikeGamepadBinding(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase)) return true;

            string name = value;
            if (name.StartsWith("button", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("button".Length);
            }
            return Enum.TryParse<GamepadButton>(name, true, out _);
        }

        /// <summary>
        /// 保存按键绑定到 PlayerPrefs（简化字段 + 官方 JSON 快照双写）。
        /// 官方 JSON 快照（InputActionAsset.ToJson）完整保留手柄/composite override，
        /// 供 LoadInputActionsAsync 恢复；简化字段保持向后兼容。
        /// </summary>
        public void SaveBindings()
        {
            PlayerPrefs.SetString("ui_bind_nav_up", CurrentBinding.navigateUp);
            PlayerPrefs.SetString("ui_bind_nav_down", CurrentBinding.navigateDown);
            PlayerPrefs.SetString("ui_bind_nav_left", CurrentBinding.navigateLeft);
            PlayerPrefs.SetString("ui_bind_nav_right", CurrentBinding.navigateRight);
            PlayerPrefs.SetString("ui_bind_submit", CurrentBinding.submit);
            PlayerPrefs.SetString("ui_bind_submit_display", CurrentBinding.submitDisplayName);
            PlayerPrefs.SetString("ui_bind_cancel", CurrentBinding.cancel);
            PlayerPrefs.SetString("ui_bind_cancel_display", CurrentBinding.cancelDisplayName);
            PlayerPrefs.SetString("ui_bind_gamepad_submit", CurrentBinding.gamepadSubmit);
            PlayerPrefs.SetString("ui_bind_gamepad_submit_display", CurrentBinding.gamepadSubmitDisplayName);
            PlayerPrefs.SetString("ui_bind_gamepad_cancel", CurrentBinding.gamepadCancel);
            PlayerPrefs.SetString("ui_bind_gamepad_cancel_display", CurrentBinding.gamepadCancelDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_ui", CurrentBinding.toggleToUI);
            PlayerPrefs.SetString("ui_bind_toggle_ui_display", CurrentBinding.toggleToUIDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_ui_gamepad", CurrentBinding.toggleToUIGamepad);
            PlayerPrefs.SetString("ui_bind_toggle_ui_gamepad_display", CurrentBinding.toggleToUIGamepadDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_ui_joystick", CurrentBinding.toggleToUIJoystick);
            PlayerPrefs.SetString("ui_bind_toggle_ui_joystick_display", CurrentBinding.toggleToUIJoystickDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_gameplay", CurrentBinding.toggleToGameplay);
            PlayerPrefs.SetFloat("ui_bind_nav_deadzone", CurrentBinding.navigateDeadzone);
            PlayerPrefs.SetFloat("ui_bind_nav_hold_delay", CurrentBinding.navigateHoldDelay);
            PlayerPrefs.SetFloat("ui_bind_nav_repeat_rate", CurrentBinding.navigateRepeatRate);
            PlayerPrefs.SetInt("ui_bind_mouse_right_cancel", CurrentBinding.mouseRightClickAsCancel ? 1 : 0);
            PlayerPrefs.SetInt("ui_bind_wheel_steering_nav", CurrentBinding.wheelSteeringNavigation ? 1 : 0);
            PlayerPrefs.SetInt("ui_bind_wheel_steering_invert", CurrentBinding.wheelSteeringInvert ? 1 : 0);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_deadzone", CurrentBinding.wheelSteeringDeadzone);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_release", CurrentBinding.wheelSteeringReleaseThreshold);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_hold", CurrentBinding.wheelSteeringHoldDelay);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_repeat", CurrentBinding.wheelSteeringRepeatRate);

            // 官方 JSON 快照：重置时删除（ResetBindings 后不应残留旧覆盖）
            if (inputActions != null)
            {
                try
                {
                    PlayerPrefs.SetString("ui_bind_asset_json", inputActions.ToJson());
                }
                catch (Exception ex)
                {
                    Log.Warning("UIInputSystem: 绑定 JSON 快照保存失败: {0}", ex.Message);
                }
            }
            else
            {
                PlayerPrefs.DeleteKey("ui_bind_asset_json");
            }

            PlayerPrefs.Save();
        }

        #endregion
    }
}
