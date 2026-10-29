using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime.Base;
using ReunionMovement.Framework.Runtime.Resources;
using ReunionMovement.Framework.Runtime.UI;
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

namespace ReunionMovement.Framework.Runtime.UIInput
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

        /// <summary>飞行摇杆/普通摇杆：自定义按钮 &gt; hat &gt; 主轴。</summary>
        private void PollGenericJoystick(Joystick joystick, UIInputDeviceProfile profile, int player, float now)
        {
            var hat = UIInputDeviceRegistry.GetHat(joystick);
            Vector2Int digital = Vector2Int.zero;

            if (UIInputDeviceRegistry.TryGetNavigationButtons(
                joystick, out var navUp, out var navDown, out var navLeft, out var navRight))
            {
                if (IsButtonActive(navUp)) digital.y += 1;
                if (IsButtonActive(navDown)) digital.y -= 1;
                if (IsButtonActive(navLeft)) digital.x -= 1;
                if (IsButtonActive(navRight)) digital.x += 1;
            }

            if (digital == Vector2Int.zero)
            {
                digital = ReadHatStep(hat, 0.5f);
            }

            // 飞行摇杆（已识别）用主轴导航；未知 HID 摇杆只有存在 hat 时才用主轴，
            // 避免把未识别方向盘的转向轴当成导航轴，导致菜单持续切换。
            bool useStick = profile.UseStickForNavigation &&
                            (profile.Kind == UIInputDeviceKind.FlightStick || hat != null);

            Vector2 stickValue = Vector2.zero;
            Vector2Int analog = Vector2Int.zero;
            if (useStick)
            {
                var stick = UIInputDeviceRegistry.GetStick(joystick);
                if (stick != null)
                {
                    stickValue = stick.ReadValue();
                    analog = ToCardinal(stickValue, CurrentBinding.navigateDeadzone);
                }
            }

            if (digital != Vector2Int.zero)
            {
                EmitDigitalStep(joystick, player, digital, now, joystick.deviceId);
            }
            else if (analog != Vector2Int.zero)
            {
                EmitAnalogStep(joystick, player, analog, stickValue, now, joystick.deviceId);
            }
            else
            {
                ResetRepeat(joystick.deviceId);
            }
        }

        private void ReadThumbStickStep(
            Vector2Control thumb, AxisControl axisX, AxisControl axisY,
            out Vector2Int step, out Vector2 value, out bool isDigital)
        {
            step = Vector2Int.zero;
            value = Vector2.zero;
            isDigital = false;

            if (thumb is DpadControl dpad)
            {
                step = ReadHatStep(dpad, 0.5f);
                isDigital = true;
                return;
            }

            if (thumb != null)
            {
                value = thumb.ReadValue();
                step = ToCardinal(value, CurrentBinding.navigateDeadzone);
                return;
            }

            if (axisX != null && axisY != null)
            {
                value = new Vector2(axisX.ReadValue(), axisY.ReadValue());
                step = ToCardinal(value, CurrentBinding.navigateDeadzone);
            }
        }

        /// <summary>
        /// 转向轴导航：左右打方向触发一次，回正后重新允许触发。
        /// 适用于 MOZA RS50 等没有十字键的方向盘：向右 = 下一项，向左 = 上一项。
        /// 保持在阈值以上可按配置自动重复。
        /// </summary>
        private void PollWheelSteering(Joystick joystick, AxisControl steering, int player, float now)
        {
            if (steering == null) return;

            float value = steering.ReadValue();
            if (CurrentBinding.wheelSteeringInvert) value = -value;

            float engage = Mathf.Clamp(CurrentBinding.wheelSteeringDeadzone, 0.01f, 1f);
            float release = Mathf.Clamp(CurrentBinding.wheelSteeringReleaseThreshold, 0f, engage);
            float holdDelay = Mathf.Max(0f, CurrentBinding.wheelSteeringHoldDelay);
            float repeatRate = CurrentBinding.wheelSteeringRepeatRate;

            if (!steeringStates.TryGetValue(joystick.deviceId, out var state))
            {
                // UI 打开时方向盘可能本来就没回正：先不武装，等回正后再允许切换，
                // 避免打开菜单的瞬间立刻跳一格。
                bool nearCenter = value > -engage && value < engage;
                state = new SteeringState { PositiveArmed = nearCenter, NegativeArmed = nearCenter };
            }

            int direction = 0;
            bool repeat = false;

            if (value >= engage)
            {
                if (state.PositiveArmed)
                {
                    state.PositiveArmed = false;
                    state.RepeatDirection = 1;
                    state.NextRepeatTime = now + holdDelay;
                    direction = 1;
                }
                else if (state.RepeatDirection == 1 && repeatRate > 0f && now >= state.NextRepeatTime)
                {
                    state.NextRepeatTime = now + repeatRate;
                    direction = 1;
                    repeat = true;
                }
            }
            else if (value <= release)
            {
                state.PositiveArmed = true;
            }

            if (value <= -engage)
            {
                if (state.NegativeArmed)
                {
                    state.NegativeArmed = false;
                    state.RepeatDirection = -1;
                    state.NextRepeatTime = now + holdDelay;
                    direction = -1;
                }
                else if (state.RepeatDirection == -1 && repeatRate > 0f && now >= state.NextRepeatTime)
                {
                    state.NextRepeatTime = now + repeatRate;
                    direction = -1;
                    repeat = true;
                }
            }
            else if (value >= -release)
            {
                state.NegativeArmed = true;
            }

            if (value > -engage && value < engage)
            {
                state.RepeatDirection = 0;
            }

            steeringStates[joystick.deviceId] = state;

            if (direction != 0)
            {
                DispatchNavigate(
                    joystick, player,
                    new Vector2Int(direction, 0),
                    new Vector2(value, 0f),
                    true, repeat, isWheelSteering: true);
            }
        }

        private static Vector2Int ReadHatStep(Vector2Control hat, float threshold)
        {
            if (hat == null) return Vector2Int.zero;

            if (hat is DpadControl dpad)
            {
                Vector2Int digital = Vector2Int.zero;
                if (IsButtonActive(dpad.up)) digital.y += 1;
                if (IsButtonActive(dpad.down)) digital.y -= 1;
                if (IsButtonActive(dpad.left)) digital.x -= 1;
                if (IsButtonActive(dpad.right)) digital.x += 1;
                return digital;
            }

            return ToCardinal(hat.ReadValue(), threshold);
        }

        private static Vector2Int ToCardinal(Vector2 value, float deadzone)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            if (ax < deadzone && ay < deadzone) return Vector2Int.zero;

            if (ax >= ay)
            {
                return new Vector2Int(value.x > 0f ? 1 : -1, 0);
            }
            return new Vector2Int(0, value.y > 0f ? 1 : -1);
        }

        private static ButtonControl GetGamepadActionButton(
            Gamepad gamepad, GamepadButton? explicitButton, string usage, GamepadButton fallback,
            Dictionary<int, ButtonControl> cache)
        {
            if (explicitButton.HasValue)
            {
                return gamepad[explicitButton.Value];
            }

            if (cache.TryGetValue(gamepad.deviceId, out var cached) && cached != null)
            {
                return cached;
            }

            // 优先使用 Input System 的标准 usage（兼容 Switch Pro 等 A/B 位置不同的手柄）
            var byUsage = InputControlPath.TryFindControl<ButtonControl>(gamepad, "*/{" + usage + "}");
            var result = byUsage ?? gamepad[fallback];
            cache[gamepad.deviceId] = result;
            return result;
        }

        /// <summary>按下状态：包含本帧刚按下（快速点击时 isPressed 可能已回落）。</summary>
        private static bool IsKeyActive(Keyboard keyboard, Key key)
        {
            if (key == Key.None) return false;
            var control = keyboard[key];
            return control.isPressed || control.wasPressedThisFrame;
        }

        /// <summary>数字按钮：包含本帧刚按下（快速点击/松开时 isPressed 可能已回落）。</summary>
        private static bool IsButtonActive(ButtonControl button)
        {
            return button != null && (button.isPressed || button.wasPressedThisFrame);
        }

        private static bool IsKeyPressedThisFrame(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        private bool IsTextInputFocused()
        {
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null) return false;

            return selected.GetComponent<UnityEngine.UI.InputField>() != null ||
                   selected.GetComponent<TMPro.TMP_InputField>() != null;
        }

        private static bool IsSubmitKeyPressed(Keyboard keyboard, Key key)
        {
            if (key == Key.None) return false;
            if (keyboard[key].wasPressedThisFrame) return true;

            // 小键盘 Enter 与主 Enter 等价
            return key == Key.Enter && keyboard[Key.NumpadEnter].wasPressedThisFrame;
        }

        /// <summary>数字输入：每次按下立即派发一次；按住后按 HoldDelay/RepeatRate 重复。</summary>
        private void EmitDigitalStep(InputDevice device, int player, Vector2Int step, float now, int stateKey)
        {
            if (!repeatStates.TryGetValue(stateKey, out var state) || state.LastStep != step)
            {
                state.LastStep = step;
                state.NextRepeatTime = now + Mathf.Max(0f, CurrentBinding.navigateHoldDelay);
                repeatStates[stateKey] = state;
                DispatchNavigate(device, player, step, new Vector2(step.x, step.y), false, false);
                return;
            }

            if (now < state.NextRepeatTime) return;

            state.NextRepeatTime = now + Mathf.Max(0.01f, CurrentBinding.navigateRepeatRate);
            repeatStates[stateKey] = state;
            DispatchNavigate(device, player, step, new Vector2(step.x, step.y), false, true);
        }

        /// <summary>模拟轴输入：方向变化立即派发；同方向按 HoldDelay/RepeatRate 重复。</summary>
        private void EmitAnalogStep(InputDevice device, int player, Vector2Int step, Vector2 value, float now, int stateKey)
        {
            if (!repeatStates.TryGetValue(stateKey, out var state) || state.LastStep != step)
            {
                state.LastStep = step;
                state.NextRepeatTime = now + Mathf.Max(0f, CurrentBinding.navigateHoldDelay);
                repeatStates[stateKey] = state;
                DispatchNavigate(device, player, step, value, true, false);
                return;
            }

            if (now < state.NextRepeatTime) return;

            state.NextRepeatTime = now + Mathf.Max(0.01f, CurrentBinding.navigateRepeatRate);
            repeatStates[stateKey] = state;
            DispatchNavigate(device, player, step, value, true, true);
        }

        private void ResetRepeat(int stateKey)
        {
            if (repeatStates.Count > 0)
            {
                repeatStates.Remove(stateKey);
            }
        }

        private void DispatchNavigate(
            InputDevice device, int player, Vector2Int step, Vector2 value,
            bool analog, bool repeat, bool isWheelSteering = false)
        {
            var e = new UINavigateEvent(device, player, step, value, analog, repeat, isWheelSteering);

            try { NavigateEventSubject?.OnNext(e); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] NavigateEventSubject 订阅者异常（已隔离）: {0}", ex.Message); }

            // 旧接口只代表主玩家（键盘/第一个实体控制器），避免第二个方向盘的输入误驱动旧 UI
            if (player == KeyboardMousePlayerIndex)
            {
                try { NavigateSubject?.OnNext(value); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] NavigateSubject 订阅者异常（已隔离）: {0}", ex.Message); }
            }

            if (DispatchNavigateToTargets(e)) return;

            if (player == KeyboardMousePlayerIndex)
            {
                MoveFocus(step);
            }
        }

        private void DispatchSubmit(InputDevice device, int player)
        {
            var e = new UIButtonEvent(device, player);

            // 新事件用于观察/提示；真正消费由导航目标完成
            try { SubmitEventSubject?.OnNext(e); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] SubmitEventSubject 订阅者异常（已隔离）: {0}", ex.Message); }

            if (DispatchSubmitToTargets(e)) return;

            // 没有目标消费时才走旧接口与 EventSystem 回退，避免同一次确认被执行两次
            if (player == KeyboardMousePlayerIndex)
            {
                try { SubmitSubject?.OnNext(Unit.Default); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] SubmitSubject 订阅者异常（已隔离）: {0}", ex.Message); }

                SubmitFocus();
            }
        }

        private void DispatchCancel(InputDevice device, int player)
        {
            var e = new UIButtonEvent(device, player);

            try { CancelEventSubject?.OnNext(e); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] CancelEventSubject 订阅者异常（已隔离）: {0}", ex.Message); }

            if (DispatchCancelToTargets(e)) return;

            if (player == KeyboardMousePlayerIndex)
            {
                try { CancelSubject?.OnNext(Unit.Default); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] CancelSubject 订阅者异常（已隔离）: {0}", ex.Message); }

                CancelFocus();
            }
        }

        private bool DispatchNavigateToTargets(in UINavigateEvent e)
        {
            var scope = GetTopFocusScope();
            if (scope == null) return false;

            try
            {
                return scope.HandleNavigate(e);
            }
            catch (Exception ex)
            {
                Log.Warning("[UIInputSystem] 焦点域处理导航异常（已隔离）: {0}", ex.Message);
                return false;
            }
        }

        private bool DispatchSubmitToTargets(in UIButtonEvent e)
        {
            var scope = GetTopFocusScope();
            if (scope == null) return false;

            try
            {
                return scope.HandleSubmit(e);
            }
            catch (Exception ex)
            {
                Log.Warning("[UIInputSystem] 焦点域处理确认异常（已隔离）: {0}", ex.Message);
                return false;
            }
        }

        private bool DispatchCancelToTargets(in UIButtonEvent e)
        {
            var scope = GetTopFocusScope();
            if (scope == null) return false;

            try
            {
                return scope.HandleCancel(e);
            }
            catch (Exception ex)
            {
                Log.Warning("[UIInputSystem] 焦点域处理取消异常（已隔离）: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// EventSystem 回退前确认当前焦点属于栈顶焦点域，
        /// 避免上层窗口没有可导航目标时把输入落到下层窗口。
        /// </summary>
        private bool IsSelectionInTopScope()
        {
            var scope = GetTopFocusScope();
            if (scope == null) return true;

            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            return selected != null && scope.Contains(selected.transform);
        }

        /// <summary>把导航事件交给 EventSystem 的当前焦点（普通 Button/Toggle 等）。</summary>
        private void MoveFocus(Vector2Int step)
        {
            if (eventSystem == null || step == Vector2Int.zero) return;
            if (!IsSelectionInTopScope()) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;

            MoveDirection moveDir;
            if (Mathf.Abs(step.x) >= Mathf.Abs(step.y))
            {
                moveDir = step.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            }
            else
            {
                moveDir = step.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            }

            var axisEvent = new AxisEventData(eventSystem)
            {
                moveVector = new Vector2(step.x, step.y),
                moveDir = moveDir,
            };
            ExecuteEvents.Execute(selected, axisEvent, ExecuteEvents.moveHandler);
        }

        private void SubmitFocus()
        {
            if (eventSystem == null || !IsSelectionInTopScope()) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        }

        private void CancelFocus()
        {
            if (eventSystem == null || !IsSelectionInTopScope()) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.cancelHandler);
        }

        private bool HasActiveNavigationTarget(int player)
        {
            var scope = GetTopFocusScope();
            return scope != null && scope.HasTargetForPlayer(player);
        }

        #endregion
        #endregion
    }
}
