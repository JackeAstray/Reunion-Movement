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
        #region 通用摇杆轮询与事件派发

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
            // 文本输入聚焦时，Cancel 属于"退出编辑/清空输入框"的语义，不能冒泡成
            // UIRouter.Back() 把整个窗口关掉。改造前只有导航做了这个检查，Cancel 仍照发，
            // 导致输入框里按 ESC 会误关窗口（与 Docs/03 的承诺相反）。
            if (IsTextInputFocused())
            {
                Log.Debug("[UIInputSystem] 文本输入聚焦中，Cancel 由输入框消费（不触发窗口返回）");
                return;
            }

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
    }
}
