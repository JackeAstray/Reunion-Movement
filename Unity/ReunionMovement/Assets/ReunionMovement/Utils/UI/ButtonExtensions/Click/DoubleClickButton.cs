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

        /// <summary>
        /// 双击
        /// </summary>
        private void Press()
        {
            Log.Debug($"[DoubleClickButton] 双击事件触发。");
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
            if (firstTime.Equals(default(DateTime)))
            {
                firstTime = DateTime.Now;
            }
            else
            {
                secondTime = DateTime.Now;
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

            if (Keyboard.current != null)
            {
                var space = Keyboard.current.spaceKey;
                var enter = Keyboard.current.enterKey;
                if (space.wasPressedThisFrame || enter.wasPressedThisFrame) pressedThisFrame = true;
                if (space.wasReleasedThisFrame || enter.wasReleasedThisFrame) releasedThisFrame = true;
            }

            if (!pressedThisFrame && Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame) pressedThisFrame = true;
            }

            if (!releasedThisFrame && Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasReleasedThisFrame) releasedThisFrame = true;
            }

            // 在按下帧记录时间（与鼠标 OnPointerDown 相同逻辑）
            if (pressedThisFrame)
            {
                if (firstTime.Equals(default(DateTime)))
                {
                    firstTime = DateTime.Now;
                }
                else
                {
                    secondTime = DateTime.Now;
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
                float milliSeconds = intervalTime.Seconds * 1000 + intervalTime.Milliseconds;
                Log.Debug($"[DoubleClickButton] 两次点击间隔：{milliSeconds} 毫秒");
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