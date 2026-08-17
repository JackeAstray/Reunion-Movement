using UnityEngine.InputSystem;

namespace ReunionMovement.UI.ButtonClick
{
    /// <summary>
    /// 按钮类共用的键盘/手柄输入检测工具。
    /// LongClickButton / DoubleClickButton / ButtonAni 三处重复的输入封装统一收敛于此，
    /// 后续调整键位/增加设备只需改一处。
    /// </summary>
    public static class ButtonInputHelper
    {
        /// <summary>可序列化的手柄按键类型（三按钮类原各自嵌套枚举，现统一收敛）</summary>
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

        /// <summary>指定键中是否有任一在本帧被按下</summary>
        public static bool KeyboardPressedThisFrame(bool enableInput, bool enableKeyboard, Key[] triggerKeys)
        {
            if (!enableInput || !enableKeyboard || Keyboard.current == null) return false;
            foreach (var k in triggerKeys)
            {
                var kc = Keyboard.current[k];
                if (kc != null && kc.wasPressedThisFrame) return true;
            }
            return false;
        }

        /// <summary>指定键中是否有任一在本帧被抬起</summary>
        public static bool KeyboardReleasedThisFrame(bool enableInput, bool enableKeyboard, Key[] triggerKeys)
        {
            if (!enableInput || !enableKeyboard || Keyboard.current == null) return false;
            foreach (var k in triggerKeys)
            {
                var kc = Keyboard.current[k];
                if (kc != null && kc.wasReleasedThisFrame) return true;
            }
            return false;
        }

        /// <summary>指定手柄按键中是否有任一在本帧被按下</summary>
        public static bool GamepadPressedThisFrame(bool enableInput, bool enableGamepad, GamepadButtonType[] triggerButtons)
        {
            if (!enableInput || !enableGamepad || Gamepad.current == null) return false;
            foreach (var b in triggerButtons)
            {
                if (IsGamepadButtonPressed(b)) return true;
            }
            return false;
        }

        /// <summary>指定手柄按键中是否有任一在本帧被抬起</summary>
        public static bool GamepadReleasedThisFrame(bool enableInput, bool enableGamepad, GamepadButtonType[] triggerButtons)
        {
            if (!enableInput || !enableGamepad || Gamepad.current == null) return false;
            foreach (var b in triggerButtons)
            {
                if (IsGamepadButtonReleased(b)) return true;
            }
            return false;
        }

        private static bool IsGamepadButtonPressed(GamepadButtonType btn)
        {
            if (Gamepad.current == null) return false;
            var g = Gamepad.current;
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

        private static bool IsGamepadButtonReleased(GamepadButtonType btn)
        {
            if (Gamepad.current == null) return false;
            var g = Gamepad.current;
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
    }
}
