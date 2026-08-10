using ReunionMovement.Common;
using System;
using System.Collections;
using System.Collections.Generic;
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

        private DateTime firstTime;
        private DateTime secondTime;

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
            var now = DateTime.Now;
            if (firstTime.Equals(default(DateTime)))
            {
                firstTime = now;
            }
            else if ((now - firstTime).TotalMilliseconds > 400)
            {
                // 距上次单击已超过双击窗口：本次作为新的第一次点击，
                // 避免"单击后长时间再双击"时第一次点击被当作 secondTime 导致双击永不触发
                firstTime = now;
                secondTime = default(DateTime);
            }
            else
            {
                secondTime = now;
            }
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

            // 在按下帧记录时间（与鼠标 OnPointerDown 相同逻辑）
            if (pressedThisFrame)
            {
                var now = DateTime.Now;
                if (firstTime.Equals(default(DateTime)))
                {
                    firstTime = now;
                }
                else if ((now - firstTime).TotalMilliseconds > 400)
                {
                    // 距上次单击已超时：本次作为新的第一次点击
                    firstTime = now;
                    secondTime = default(DateTime);
                }
                else
                {
                    secondTime = now;
                }
            }

            // 在抬起帧做检查（与鼠标 OnPointerUp 相同逻辑）
            if (releasedThisFrame)
            {
                TryHandleClickInterval();
            }
        }

        /// <summary>
        /// 检查两次点击间隔并处理双击
        /// </summary>
        private void TryHandleClickInterval()
        {
            if (!firstTime.Equals(default(DateTime)) && !secondTime.Equals(default(DateTime)))
            {
                var intervalTime = secondTime - firstTime;
                // TotalMilliseconds 包含分/小时等全部跨度，避免 TimeSpan.Seconds 只含 0-59 部分
                // 导致超过 59 秒的间隔被误判为合法双击
                double milliSeconds = intervalTime.TotalMilliseconds;
                Log.Debug($"[DoubleClickButton] 两次点击间隔：{milliSeconds:F0} 毫秒");
                if (milliSeconds < 400)
                {
                    Press();
                }
                else
                {
                    resetTime();
                }
            }
        }

        /// <summary>
        /// 重置时间
        /// </summary>
        private void resetTime()
        {
            firstTime = default(DateTime);
            secondTime = default(DateTime);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            resetTime();
        }
    }
}