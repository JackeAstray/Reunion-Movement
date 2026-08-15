using ReunionMovement.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReunionMovement.UI.ButtonClick
{
    //双击按钮
    public class DoubleClickButton : Button
    {
        [SerializeField]
        private ButtonClickEvent doubleClick = new ButtonClickEvent();

        public ButtonClickEvent onDoubleClick
        {
            get { return doubleClick; }
            set { doubleClick = value; }
        }

        // 首次有效点击时间（Time.unscaledTime，-1 表示未记录；不用墙钟，避免系统时间被校准/手动调整时跳变误判，且暂停不累积）
        private float firstClickTime = -1f;
        // 本次按下的时间（抬起时校验按住时长，长按不构成有效点击）
        private float currentDownTime = -1f;
        // 双击判定窗口（秒）：两次有效点击（按下→抬起）的最大间隔
        private const float DoubleClickWindow = 0.4f;

        // 新增：是否启用输入与键表（默认与原实现一致）
        [SerializeField]
        private bool enableInput = true;
        [SerializeField]
        private bool enableKeyboard = true;
        [SerializeField]
        private bool enableGamepad = true;

        [SerializeField]
        private UnityEngine.InputSystem.Key[] keyboardTriggerKeys = new UnityEngine.InputSystem.Key[] { UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.Enter };

        [SerializeField]
        private ButtonInputHelper.GamepadButtonType[] gamepadTriggerButtons = new ButtonInputHelper.GamepadButtonType[] { ButtonInputHelper.GamepadButtonType.South };

        /// <summary>
        /// 双击
        /// </summary>
        private void Press()
        {
            Log.Debug("[DoubleClickButton] 双击事件触发。");
            if (onDoubleClick != null)
            {
                onDoubleClick.Invoke();
            }
            resetTime();
        }

        /// <summary>
        /// 按下（鼠标）
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            // 仅记录本次按下时间：双击判定移到抬起时，基于两次完整点击（按下→抬起）的间隔。
            // 旧实现基于两次按下间隔，第二次按住超过窗口再抬起仍会误触发双击。
            currentDownTime = Time.unscaledTime;
        }

        /// <summary>
        /// 抬起（鼠标）
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            TryHandleClickInterval();
        }

        /// <summary>
        /// 离开（鼠标）
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            resetTime();
        }

        private bool KeyboardPressedThisFrame()
        {
            return ButtonInputHelper.KeyboardPressedThisFrame(enableInput, enableKeyboard, keyboardTriggerKeys);
        }

        private bool KeyboardReleasedThisFrame()
        {
            return ButtonInputHelper.KeyboardReleasedThisFrame(enableInput, enableKeyboard, keyboardTriggerKeys);
        }

        private bool GamepadPressedThisFrame()
        {
            return ButtonInputHelper.GamepadPressedThisFrame(enableInput, enableGamepad, gamepadTriggerButtons);
        }

        private bool GamepadReleasedThisFrame()
        {
            return ButtonInputHelper.GamepadReleasedThisFrame(enableInput, enableGamepad, gamepadTriggerButtons);
        }

        /// <summary>
        /// 每帧轮询：当按钮为当前选中项时，监听 Space/Enter/手柄主键 (Gamepad.buttonSouth) 以支持键盘与手柄的双击。
        /// 按下时记录时间（与鼠标 OnPointerDown 保持一致），抬起时检测间隔并触发双击逻辑（与鼠标 OnPointerUp 保持一致）。
        /// </summary>
        private void Update()
        {
            if (EventSystem.current == null) return;

            bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;

            if (!isSelected)
            {
                // 若失去选中，重置计时
                resetTime();
                return;
            }

            bool pressedThisFrame = false;
            bool releasedThisFrame = false;

            if (KeyboardPressedThisFrame()) pressedThisFrame = true;
            if (KeyboardReleasedThisFrame()) releasedThisFrame = true;

            if (!pressedThisFrame && GamepadPressedThisFrame()) pressedThisFrame = true;
            if (!releasedThisFrame && GamepadReleasedThisFrame()) releasedThisFrame = true;

            // 在按下帧仅记录按下时间（与鼠标 OnPointerDown 相同逻辑，双击判定在抬起帧执行）
            if (pressedThisFrame)
            {
                currentDownTime = Time.unscaledTime;
            }

            // 在抬起帧做检查（与鼠标 OnPointerUp 相同逻辑）
            if (releasedThisFrame)
            {
                TryHandleClickInterval();
            }
        }

        /// <summary>
        /// 检查两次点击间隔并处理双击。
        /// 判定基于两次完整点击（按下→抬起）的抬起时刻间隔：
        /// - 按住超过窗口的“长按”不构成有效点击（旧实现基于按下间隔，第二次长按仍误判双击）；
        /// - 两次有效点击间隔 ≤ 窗口时触发双击，超时则将本次点击作为新的第一次。
        /// </summary>
        private void TryHandleClickInterval()
        {
            // 从未按下（无有效按下记录）时直接丢弃
            if (currentDownTime < 0f) return;

            float now = Time.unscaledTime;
            // 长按（按下到抬起超过窗口）不构成有效点击：直接丢弃本次按压
            if (now - currentDownTime > DoubleClickWindow) return;

            if (firstClickTime < 0f)
            {
                // 第一次有效点击
                firstClickTime = now;
                return;
            }

            var intervalMs = (now - firstClickTime) * 1000f;
            Log.Debug($"[DoubleClickButton] 两次点击间隔：{intervalMs:F0} 毫秒");
            if (intervalMs <= DoubleClickWindow * 1000f)
            {
                // 两次有效点击在窗口内：触发双击（Press 内部会 resetTime）
                Press();
            }
            else
            {
                // 超过窗口：本次点击成为新的第一次
                firstClickTime = now;
            }
        }

        /// <summary>
        /// 重置时间
        /// </summary>
        private void resetTime()
        {
            firstClickTime = -1f;
            currentDownTime = -1f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            resetTime();
        }
    }
}