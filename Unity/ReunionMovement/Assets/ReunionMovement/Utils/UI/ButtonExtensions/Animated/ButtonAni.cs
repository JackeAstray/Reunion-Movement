using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReunionMovement.UI.ButtonAnimated
{
    public enum ButtonAniState
    {
        Normal,
        Highlighted,
        Pressed,
        Selected,
        Disabled
    }

    [System.Serializable]
    public class ButtonAniSetting
    {
        public Vector3 scale = Vector3.one;
        public string text;
        public Color textColor = Color.white;
        public Sprite image;
        public Color imageColor = Color.white;
    }

    public class ButtonAni : Button
    {
        [SerializeField]
        public ButtonAniSetting normal;
        [SerializeField]
        public ButtonAniSetting highlighted;
        [SerializeField]
        public ButtonAniSetting pressed;
        [SerializeField]
        public ButtonAniSetting selected;
        [SerializeField]
        public ButtonAniSetting disabled;

        public float transitionDuration = 0.2f;

        private Image targetImage;
        private TextMeshProUGUI tmpText;
        private Coroutine animCoroutine;
        private Coroutine submitCoroutine;

        // 新增：是否启用输入系统（总开关）与键盘/手柄开关
        [SerializeField]
        private bool enableInput = true;
        [SerializeField]
        private bool enableKeyboard = true;
        [SerializeField]
        private bool enableGamepad = true;

        // 新增：可在 Inspector 指定触发的键（默认与原实现一致）
        [SerializeField]
        private UnityEngine.InputSystem.Key[] keyboardTriggerKeys = new UnityEngine.InputSystem.Key[] { UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.Enter };

        // 嵌套枚举：可序列化的手柄按键类型
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

        // 支持键盘与手柄触发（可配置）
        private bool submitPressed = false;

        protected override void Awake()
        {
            base.Awake();
            targetImage = GetComponent<Image>();
            tmpText = GetComponentInChildren<TextMeshProUGUI>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!interactable)
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
            else
            {
                ApplyState(ButtonAniState.Normal, true);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (!interactable)
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
            else
            {
                ApplyState(ButtonAniState.Normal, true);
            }
        }
#endif

        private bool KeyboardPressedThisFrame()
        {
            if (!enableInput || !enableKeyboard) return false;
            if (UnityEngine.InputSystem.Keyboard.current == null) return false;
            foreach (var k in keyboardTriggerKeys)
            {
                var keyControl = UnityEngine.InputSystem.Keyboard.current[k];
                if (keyControl != null && keyControl.wasPressedThisFrame) return true;
            }
            return false;
        }

        private bool KeyboardReleasedThisFrame()
        {
            if (!enableInput || !enableKeyboard) return false;
            if (UnityEngine.InputSystem.Keyboard.current == null) return false;
            foreach (var k in keyboardTriggerKeys)
            {
                var keyControl = UnityEngine.InputSystem.Keyboard.current[k];
                if (keyControl != null && keyControl.wasReleasedThisFrame) return true;
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
        /// 使用 Input System 支持键盘与手柄触发（可配置）
        /// 当按钮为当前选中项时，监听按下与抬起以触发动画和提交。
        /// </summary>
        private void Update()
        {
            if (EventSystem.current == null) return;

            bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;

            if (isSelected)
            {
                // 检查按下（键盘或手柄），使用可配置键表
                if (!submitPressed)
                {
                    bool pressedThisFrame = KeyboardPressedThisFrame() || GamepadPressedThisFrame();

                    if (pressedThisFrame)
                    {
                        submitPressed = true;
                        if (interactable)
                        {
                            // 先表现按下状态
                            ApplyState(ButtonAniState.Pressed);
                        }
                        else
                        {
                            ApplyState(ButtonAniState.Disabled, true);
                        }
                    }
                }

                // 检查抬起（键盘或手柄）
                if (submitPressed)
                {
                    bool releasedThisFrame = KeyboardReleasedThisFrame() || GamepadReleasedThisFrame();

                    if (releasedThisFrame)
                    {
                        submitPressed = false;
                        OnPress();
                    }
                }
            }
            else
            {
                // 失去选中时清理状态
                if (submitPressed)
                {
                    submitPressed = false;
                    if (interactable)
                    {
                        ApplyState(ButtonAniState.Normal);
                    }
                    else
                    {
                        ApplyState(ButtonAniState.Disabled, true);
                    }
                }
            }
        }

        /// <summary>
        /// 鼠标移入时调用
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Highlighted);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 鼠标移出时调用
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Normal);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 鼠标按下时调用
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Pressed);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 鼠标抬起时调用
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Normal);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 选择时调用
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Selected);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 取消选择时调用
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Normal);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 提交按钮时调用
        /// </summary>
        public void OnPress()
        {
            if (interactable)
            {
                // 先切到 Pressed 动画（AnimateTo 会使用 transitionDuration）
                ApplyState(ButtonAniState.Pressed);

                // 不要停止 animCoroutine（会导致 Pressed 动画立即中止）。
                // 使用单独的 submitCoroutine 来等待 transitionDuration 后切回 Normal。
                if (submitCoroutine != null)
                {
                    StopCoroutine(submitCoroutine);
                    submitCoroutine = null;
                }
                submitCoroutine = StartCoroutine(SubmitAnimationCoroutine());
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 提交动画协程
        /// </summary>
        /// <returns></returns>
        private IEnumerator SubmitAnimationCoroutine()
        {
            // 等待完整的 transitionDuration（使用实时时间以防 timeScale 被改变）
            yield return new WaitForSecondsRealtime(transitionDuration > 0 ? transitionDuration : 0.05f);

            if (interactable)
            {
                ApplyState(ButtonAniState.Normal);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }

            submitCoroutine = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // 停止未完成的提交协程，避免失活时协程继续执行
            if (submitCoroutine != null)
            {
                StopCoroutine(submitCoroutine);
                submitCoroutine = null;
            }

            // 清理按键/手柄状态
            submitPressed = false;

            // 直接同步设置属性，避免失活时启动协程
            var setting = GetSetting(ButtonAniState.Disabled);
            transform.localScale = setting.scale;
            if (targetImage)
            {
                targetImage.color = setting.imageColor;
                if (setting.image != null) targetImage.sprite = setting.image;
            }
            if (tmpText)
            {
                tmpText.color = setting.textColor;
                if (!string.IsNullOrEmpty(setting.text)) tmpText.text = setting.text;
            }
        }

        /// <summary>
        /// 获取状态对应的设置
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private ButtonAniSetting GetSetting(ButtonAniState state)
        {
            switch (state)
            {
                case ButtonAniState.Normal: return normal;
                case ButtonAniState.Highlighted: return highlighted;
                case ButtonAniState.Pressed: return pressed;
                case ButtonAniState.Selected: return selected;
                case ButtonAniState.Disabled: return disabled;
                default: return normal;
            }
        }

        /// <summary>
        /// 应用状态设置
        /// </summary>
        /// <param name="state"></param>
        /// <param name="instant"></param>
        public void ApplyState(ButtonAniState state, bool instant = false)
        {
            var setting = GetSetting(state);
            if (animCoroutine != null)
            {
                StopCoroutine(animCoroutine);
            }
            animCoroutine = StartCoroutine(AnimateTo(setting, instant));
        }

        /// <summary>
        /// 执行动画协程
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="instant"></param>
        /// <returns></returns>
        private IEnumerator AnimateTo(ButtonAniSetting setting, bool instant)
        {
            float t = 0f;
            float duration = instant ? 0f : transitionDuration;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = setting.scale;
            Color startImgColor = targetImage ? targetImage.color : Color.white;
            Color endImgColor = setting.imageColor;
            Color startTextColor = tmpText ? tmpText.color : Color.white;
            Color endTextColor = setting.textColor;
            Sprite startSprite = targetImage ? targetImage.sprite : null;
            Sprite endSprite = setting.image;
            string startText = tmpText ? tmpText.text : null;
            string endText = setting.text;

            while (t < duration)
            {
                float lerp = duration > 0 ? t / duration : 1f;
                transform.localScale = Vector3.Lerp(startScale, endScale, lerp);
                if (targetImage)
                {
                    targetImage.color = Color.Lerp(startImgColor, endImgColor, lerp);
                    if (endSprite != null) targetImage.sprite = endSprite;
                }
                if (tmpText)
                {
                    tmpText.color = Color.Lerp(startTextColor, endTextColor, lerp);
                    if (!string.IsNullOrEmpty(endText)) tmpText.text = endText;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            transform.localScale = endScale;
            if (targetImage)
            {
                targetImage.color = endImgColor;
                if (endSprite != null) targetImage.sprite = endSprite;
            }
            if (tmpText)
            {
                tmpText.color = endTextColor;
                if (!string.IsNullOrEmpty(endText)) tmpText.text = endText;
            }
        }
    }
}