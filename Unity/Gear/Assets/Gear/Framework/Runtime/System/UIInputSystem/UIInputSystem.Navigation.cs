using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.Resources;
using Gear.Framework.Runtime.UI;
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

namespace Gear.Framework.Runtime.UIInput
{
    public partial class UIInputSystem
    {
        #region 输入事件回调与设备轮询

        private void RegisterInputCallbacks()
        {
            // 导航/确认/取消不再走 InputAction 回调：改为每帧直接轮询设备。
            // 这样键盘、手柄、赛车方向盘、飞行摇杆可以统一处理，并按玩家槽位路由；
            // InputModule 只保留鼠标/触摸指针动作，避免 EventSystem 与轮播双重响应。
            if (inputActions != null)
            {
                var playerMap = inputActions.FindActionMap("Player");
                playerMap?.Enable();
            }

            SetUIActionsEnabled(enablePointerActions: true);
            currentMode = UIControlMode.Gameplay;
        }

        /// <summary>
        /// UISystem 可能在 UIInputSystem.Init 之后才创建/替换 EventSystem，使用前补一次引用解析。
        /// </summary>
        private void EnsureInputModuleReferences()
        {
            var current = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = current ?? UISystem.Instance?.EventSystem;
            }
            else if (!eventSystem.isActiveAndEnabled && current != null && current != eventSystem)
            {
                eventSystem = current;
            }

            if (eventSystem != null &&
                (inputModule == null || inputModule.gameObject != eventSystem.gameObject))
            {
                inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            }
        }

        private void SetUIActionsEnabled(bool enablePointerActions)
        {
            // 自己的资产副本：保持指针动作与 Player map 可用；导航动作关闭（由轮询器接管）。
            if (inputActions != null)
            {
                SetActionEnabled(inputActions, "UI/Point", enablePointerActions);
                SetActionEnabled(inputActions, "UI/Click", enablePointerActions);
                SetActionEnabled(inputActions, "UI/RightClick", enablePointerActions);
                SetActionEnabled(inputActions, "UI/MiddleClick", enablePointerActions);
                SetActionEnabled(inputActions, "UI/ScrollWheel", enablePointerActions);
                SetActionEnabled(inputActions, "UI/TrackedDevicePosition", enablePointerActions);
                SetActionEnabled(inputActions, "UI/TrackedDeviceOrientation", enablePointerActions);

                // 始终关闭：键盘/手柄/方向盘导航由 PollInputDevices 产生，
                // 若这里再启用 InputAction，会与轮询器重复响应。
                SetActionEnabled(inputActions, "UI/Navigate", false);
                SetActionEnabled(inputActions, "UI/Submit", false);
                SetActionEnabled(inputActions, "UI/Cancel", false);
            }

            // 真正驱动 EventSystem 的是 InputModule 自己的资产，必须单独关闭导航动作。
            if (inputModule != null)
            {
                inputModule.move?.action?.Disable();
                inputModule.submit?.action?.Disable();
                inputModule.cancel?.action?.Disable();

                if (enablePointerActions)
                {
                    inputModule.point?.action?.Enable();
                    inputModule.leftClick?.action?.Enable();
                    inputModule.rightClick?.action?.Enable();
                    inputModule.middleClick?.action?.Enable();
                    inputModule.scrollWheel?.action?.Enable();
                    inputModule.trackedDevicePosition?.action?.Enable();
                    inputModule.trackedDeviceOrientation?.action?.Enable();
                }
            }
        }

        private static void SetActionEnabled(InputActionAsset asset, string actionPath, bool enabled)
        {
            var action = asset?.FindAction(actionPath);
            if (action == null) return;

            if (enabled)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        private void UnregisterInputCallbacks()
        {
            // 轮询模式无需解除 InputAction 回调；只需确保模块导航保持关闭。
            if (inputModule == null) return;
            inputModule.move?.action?.Disable();
            inputModule.submit?.action?.Disable();
            inputModule.cancel?.action?.Disable();
        }

        #region 设备轮询与事件派发

        /// <summary>每帧轮询键盘/鼠标/手柄/方向盘/飞行摇杆，产生带玩家槽位的 UI 事件。</summary>
        private void PollInputDevices()
        {
            // 重绑定期间捕获的按键不应同时触发 UI 动作
            if (isRebinding || Time.unscaledTime < inputSuppressedUntil) return;

            float now = Time.unscaledTime;

            PollKeyboard(now);
            PollMouse(now);

            for (int i = 0; i < routedDevices.Count; i++)
            {
                var device = routedDevices[i];
                if (device == null || !device.added) continue;

                int player = GetPlayerIndex(device);
                if (device is Gamepad gamepad)
                {
                    PollGamepad(gamepad, player, now);
                }
                else if (device is Joystick joystick)
                {
                    PollJoystick(joystick, player, now);
                }
            }
        }

        private void PollKeyboard(float now)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 输入框获得焦点时，方向键/WASD 用于文本编辑，不能被 UI 导航吞掉
            if (IsTextInputFocused())
            {
                ResetRepeat(-1);
            }
            else
            {
                Vector2Int step = Vector2Int.zero;
                var upKey = GetCachedKey(ref cachedNavUpStr, ref cachedNavUpKey, CurrentBinding.navigateUp);
                var downKey = GetCachedKey(ref cachedNavDownStr, ref cachedNavDownKey, CurrentBinding.navigateDown);
                var leftKey = GetCachedKey(ref cachedNavLeftStr, ref cachedNavLeftKey, CurrentBinding.navigateLeft);
                var rightKey = GetCachedKey(ref cachedNavRightStr, ref cachedNavRightKey, CurrentBinding.navigateRight);

                if (IsKeyActive(keyboard, upKey) || IsKeyActive(keyboard, Key.UpArrow)) step.y += 1;
                if (IsKeyActive(keyboard, downKey) || IsKeyActive(keyboard, Key.DownArrow)) step.y -= 1;
                if (IsKeyActive(keyboard, leftKey) || IsKeyActive(keyboard, Key.LeftArrow)) step.x -= 1;
                if (IsKeyActive(keyboard, rightKey) || IsKeyActive(keyboard, Key.RightArrow)) step.x += 1;

                if (step == Vector2Int.zero)
                {
                    ResetRepeat(-1);
                }
                else
                {
                    EmitDigitalStep(null, KeyboardMousePlayerIndex, step, now, -1);
                }
            }

            var submitKey = GetCachedKey(ref cachedSubmitStr, ref cachedSubmitKey, CurrentBinding.submit);
            if (IsSubmitKeyPressed(keyboard, submitKey))
            {
                DispatchSubmit(null, KeyboardMousePlayerIndex);
            }

            var cancelKey = GetCachedKey(ref cachedCancelStr, ref cachedCancelKey, CurrentBinding.cancel);
            if (IsKeyPressedThisFrame(keyboard, cancelKey))
            {
                DispatchCancel(null, KeyboardMousePlayerIndex);
            }
        }

        private void PollMouse(float now)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (CurrentBinding.mouseRightClickAsCancel && mouse.rightButton.wasPressedThisFrame)
            {
                DispatchCancel(null, KeyboardMousePlayerIndex);
            }
        }

        private void PollGamepad(Gamepad gamepad, int player, float now)
        {
            Vector2Int digital = Vector2Int.zero;
            var dpad = gamepad.dpad;
            if (IsButtonActive(dpad.up)) digital.y += 1;
            if (IsButtonActive(dpad.down)) digital.y -= 1;
            if (IsButtonActive(dpad.left)) digital.x -= 1;
            if (IsButtonActive(dpad.right)) digital.x += 1;

            // 方向盘伪装成手柄时，允许用 SetDeviceNavigation 把它的面板按钮/小摇杆按钮绑成方向
            if (UIInputDeviceRegistry.TryGetNavigationButtons(
                gamepad, out var navUp, out var navDown, out var navLeft, out var navRight))
            {
                if (IsButtonActive(navUp)) digital.y += 1;
                if (IsButtonActive(navDown)) digital.y -= 1;
                if (IsButtonActive(navLeft)) digital.x -= 1;
                if (IsButtonActive(navRight)) digital.x += 1;
            }

            // 自定义按钮可能与十字键重合，避免一次按下被算成两步
            digital.x = Mathf.Clamp(digital.x, -1, 1);
            digital.y = Mathf.Clamp(digital.y, -1, 1);

            // 有些方向盘会伪装成 Gamepad：左摇杆是转向轴，不能用于 UI 导航；
            // 这类设备改用右摇杆（通常是小摇杆），没有右摇杆输入时只用十字键。
            bool wheelLike = UIInputDeviceRegistry.LooksLikeRacingWheel(gamepad);
            Vector2 stick;
            if (wheelLike)
            {
                stick = gamepad.rightStick.ReadValue();
            }
            else
            {
                stick = gamepad.leftStick.ReadValue();
                // 左摇杆优先；左摇杆回中时允许右摇杆导航
                if (Mathf.Abs(stick.x) < CurrentBinding.navigateDeadzone &&
                    Mathf.Abs(stick.y) < CurrentBinding.navigateDeadzone)
                {
                    Vector2 right = gamepad.rightStick.ReadValue();
                    if (Mathf.Abs(right.x) >= CurrentBinding.navigateDeadzone ||
                        Mathf.Abs(right.y) >= CurrentBinding.navigateDeadzone)
                    {
                        stick = right;
                    }
                }
            }
            Vector2Int analog = ToCardinal(stick, CurrentBinding.navigateDeadzone);

            if (digital != Vector2Int.zero)
            {
                EmitDigitalStep(gamepad, player, digital, now, gamepad.deviceId);
            }
            else if (analog != Vector2Int.zero)
            {
                EmitAnalogStep(gamepad, player, analog, stick, now, gamepad.deviceId);
            }
            else
            {
                ResetRepeat(gamepad.deviceId);
            }

            var explicitSubmit = GetCachedGamepadButton(
                ref cachedGamepadSubmitStr, ref cachedGamepadSubmitButton, CurrentBinding.gamepadSubmit);
            ButtonControl submit = explicitSubmit.HasValue
                ? null
                : InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.gamepadSubmit);
            submit ??= GetGamepadActionButton(gamepad, explicitSubmit, "Submit", GamepadButton.South, gamepadSubmitCache);
            if (submit != null && submit.wasPressedThisFrame)
            {
                DispatchSubmit(gamepad, player);
            }

            var explicitCancel = GetCachedGamepadButton(
                ref cachedGamepadCancelStr, ref cachedGamepadCancelButton, CurrentBinding.gamepadCancel);
            ButtonControl cancel = explicitCancel.HasValue
                ? null
                : InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.gamepadCancel);
            cancel ??= GetGamepadActionButton(gamepad, explicitCancel, "Cancel", GamepadButton.East, gamepadCancelCache);
            if (cancel != null && cancel.wasPressedThisFrame)
            {
                DispatchCancel(gamepad, player);
            }
        }

        private void PollJoystick(Joystick joystick, int player, float now)
        {
            var profile = UIInputDeviceRegistry.GetProfile(joystick);
            if (profile.Kind == UIInputDeviceKind.RacingWheel)
            {
                PollRacingWheel(joystick, profile, player, now);
            }
            else
            {
                PollGenericJoystick(joystick, profile, player, now);
            }

            if (UIInputDeviceRegistry.TryGetSubmit(joystick, out var submit) && submit.wasPressedThisFrame)
            {
                DispatchSubmit(joystick, player);
            }
            if (UIInputDeviceRegistry.TryGetCancel(joystick, out var cancel) && cancel.wasPressedThisFrame)
            {
                DispatchCancel(joystick, player);
            }
        }

        /// <summary>
        /// 方向盘导航。Auto 优先级：自定义按钮 &gt; 小摇杆 &gt; 十字键/hat &gt; 转向轴。
        /// 只要检测到小摇杆/hat，就不会再使用转向轴，避免轻微打方向导致菜单频繁切换。
        /// </summary>
        private void PollRacingWheel(Joystick joystick, UIInputDeviceProfile profile, int player, float now)
        {
            var hat = UIInputDeviceRegistry.GetHat(joystick);
            bool hasCustom = UIInputDeviceRegistry.TryGetNavigationButtons(
                joystick, out var navUp, out var navDown, out var navLeft, out var navRight);
            bool hasThumb = UIInputDeviceRegistry.TryGetThumbStick(
                joystick, out var thumb, out var thumbX, out var thumbY);
            bool thumbIsAxesOnly = thumb == null && thumbX != null && thumbY != null;

            bool useCustom;
            bool useThumb;
            bool useHat;
            bool useSteering;

            switch (profile.WheelNavigationSource)
            {
                case WheelNavigationSource.Hat:
                    useCustom = false; useThumb = false; useHat = true; useSteering = false;
                    break;
                case WheelNavigationSource.ThumbStick:
                    useCustom = false; useThumb = true; useHat = false; useSteering = false;
                    break;
                case WheelNavigationSource.Steering:
                    useCustom = false; useThumb = false; useHat = false; useSteering = true;
                    break;
                case WheelNavigationSource.CustomButtons:
                    useCustom = true; useThumb = false; useHat = false; useSteering = false;
                    break;
                case WheelNavigationSource.None:
                    useCustom = false; useThumb = false; useHat = false; useSteering = false;
                    break;
                default: // Auto
                    // 优先级：自定义按钮 > 小摇杆 > 十字键 > 转向轴。
                    // 自定义按钮/小摇杆/十字键可以互为兜底；只要存在其一，就不使用转向轴。
                    // rx/ry 轴对只有在没有 hat 时才作为小摇杆自动使用，避免把离合/手刹轴误当摇杆。
                    useCustom = hasCustom;
                    useThumb = hasThumb && (thumb != null || hat == null);
                    useHat = hat != null;
                    useSteering = !hasCustom && !useThumb && hat == null;
                    break;
            }

            // 全局开关仍然可以关闭转向轴导航
            if (useSteering) useSteering = CurrentBinding.wheelSteeringNavigation;

            Vector2Int digital = Vector2Int.zero;
            if (useCustom)
            {
                if (IsButtonActive(navUp)) digital.y += 1;
                if (IsButtonActive(navDown)) digital.y -= 1;
                if (IsButtonActive(navLeft)) digital.x -= 1;
                if (IsButtonActive(navRight)) digital.x += 1;
            }

            Vector2Int thumbStep = Vector2Int.zero;
            Vector2 thumbValue = Vector2.zero;
            bool thumbIsDigital = false;
            if (digital == Vector2Int.zero && useThumb)
            {
                ReadThumbStickStep(thumb, thumbX, thumbY, out thumbStep, out thumbValue, out thumbIsDigital);
            }

            // 小摇杆优先；小摇杆无输入时再用十字键兜底
            if (digital == Vector2Int.zero && thumbStep == Vector2Int.zero && useHat)
            {
                digital = ReadHatStep(hat, 0.5f);
            }

            if (digital != Vector2Int.zero)
            {
                EmitDigitalStep(joystick, player, digital, now, joystick.deviceId);
            }
            else if (thumbStep != Vector2Int.zero)
            {
                if (thumbIsDigital)
                {
                    EmitDigitalStep(joystick, player, thumbStep, now, joystick.deviceId);
                }
                else
                {
                    EmitAnalogStep(joystick, player, thumbStep, thumbValue, now, joystick.deviceId);
                }
            }
            else
            {
                ResetRepeat(joystick.deviceId);
            }

            if (useSteering && digital == Vector2Int.zero && thumbStep == Vector2Int.zero)
            {
                var steering = UIInputDeviceRegistry.GetSteeringAxis(joystick);
                if (steering != null)
                {
                    PollWheelSteering(joystick, steering, player, now);
                }
                else
                {
                    steeringStates.Remove(joystick.deviceId);
                }
            }
            else
            {
                steeringStates.Remove(joystick.deviceId);
            }
        }

        #endregion
        #endregion
    }
}
