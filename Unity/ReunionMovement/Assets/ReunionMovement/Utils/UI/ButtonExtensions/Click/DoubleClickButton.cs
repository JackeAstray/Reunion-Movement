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

        public enum GamepadButtonType
        {
            South,
            North,
            West,
            East,
            LeftShoulder,
            RightShoulder,
            LeftTrigger,
            RightTrigger,
            Start,
            Select
        }

        [SerializeField]
        private GamepadButtonType[] gamepadTriggerButtons = new GamepadButtonType[] { GamepadButtonType.South };

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

        private bool KeyboardPressedThisFrame()
        {
            if (!enableInput || !enableKeyboard) return false;
            if (UnityEngine.InputSystem.Keyboard.current == null) return false;
            foreach (var k in keyboardTriggerKeys)
            {
                var kc = UnityEngine.InputSystem.Keyboard.current[k];
                if (kc != null && kc.wasPressedThisFrame) return true;
            }
            return false;
        }

        private bool KeyboardReleasedThisFrame()
        {
            if (!enableInput || !enableKeyboard) return false;
            if (UnityEngine.InputSystem.Keyboard.current == null) return false;
            foreach (var k in keyboardTriggerKeys)
            {
                var kc = UnityEngine.InputSystem.Keyboard.current[k];
                if (kc != null && kc.wasReleasedThisFrame) return true;
            }
            return false;
        }

        private bool IsGamepadButtonPressed(GamepadButtonType btn)
        {
            if (UnityEngine.InputSystem.Gamepad.current == null) return false;
            var g = UnityEngine.InputSystem.Gamepad.current;
            switch (btn)
            {
                case GamepadButtonType.South: return g.buttonSouth.wasPressedThisFrame;
                case GamepadButtonType.North: return g.buttonNorth.wasPressedThisFrame;
                case GamepadButtonType.West: return g.buttonWest.wasPressedThisFrame;
                case GamepadButtonType.East: return g.buttonEast.wasPressedThisFrame;
                case GamepadButtonType.LeftShoulder: return g.leftShoulder.wasPressedThisFrame;
                case GamepadButtonType.RightShoulder: return g.rightShoulder.wasPressedThisFrame;
                case GamepadButtonType.LeftTrigger: return g.leftTrigger.wasPressedThisFrame;
                case GamepadButtonType.RightTrigger: return g.rightTrigger.wasPressedThisFrame;
                case GamepadButtonType.Start: return g.startButton != null && g.startButton.wasPressedThisFrame;
                case GamepadButtonType.Select: return g.selectButton != null && g.selectButton.wasPressedThisFrame;
                default: return false;
            }
        }

        private bool IsGamepadButtonReleased(GamepadButtonType btn)
        {
            if (UnityEngine.InputSystem.Gamepad.current == null) return false;
            var g = UnityEngine.InputSystem.Gamepad.current;
            switch (btn)
            {
                case GamepadButtonType.South: return g.buttonSouth.wasReleasedThisFrame;
                case GamepadButtonType.North: return g.buttonNorth.wasReleasedThisFrame;
                case GamepadButtonType.West: return g.buttonWest.wasReleasedThisFrame;
                case GamepadButtonType.East: return g.buttonEast.wasReleasedThisFrame;
                case GamepadButtonType.LeftShoulder: return g.leftShoulder.wasReleasedThisFrame;
                case GamepadButtonType.RightShoulder: return g.rightShoulder.wasReleasedThisFrame;
                case GamepadButtonType.LeftTrigger: return g.leftTrigger.wasReleasedThisFrame;
                case GamepadButtonType.RightTrigger: return g.rightTrigger.wasReleasedThisFrame;
                case GamepadButtonType.Start: return g.startButton != null && g.startButton.wasReleasedThisFrame;
                case GamepadButtonType.Select: return g.selectButton != null && g.selectButton.wasReleasedThisFrame;
                default: return false;
            }
        }

        private bool GamepadPressedThisFrame()
        {
            if (!enableInput || !enableGamepad) return false;
            if (UnityEngine.InputSystem.Gamepad.current == null) return false;
            foreach (var b in gamepadTriggerButtons)
            {
                if (IsGamepadButtonPressed(b)) return true;
            }
            return false;
        }

        private bool GamepadReleasedThisFrame()
        {
            if (!enableInput || !enableGamepad) return false;
            if (UnityEngine.InputSystem.Gamepad.current == null) return false;
            foreach (var b in gamepadTriggerButtons)
            {
                if (IsGamepadButtonReleased(b)) return true;
            }
            return false;
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