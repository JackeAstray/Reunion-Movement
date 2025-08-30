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

        private Image image;
        private TextMeshProUGUI tmpText;
        private Coroutine animCoroutine;
        private Coroutine submitCoroutine;

        // 支持键盘与手柄触发（Space/Enter / Gamepad South 按钮）
        private bool submitPressed = false;

        protected override void Awake()
        {
            base.Awake();
            image = GetComponent<Image>();
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

        /// <summary>
        /// 使用 Input System 支持键盘与手柄触发（Space / Enter / Gamepad South）
        /// 当按钮为当前选中项时，监听按下与抬起以触发动画和提交。
        /// </summary>
        private void Update()
        {
            if (EventSystem.current == null) return;

            bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;

            if (isSelected)
            {
                // 检查按下（键盘或手柄）
                if (!submitPressed)
                {
                    bool pressedThisFrame = false;

                    if (Keyboard.current != null)
                    {
                        var space = Keyboard.current.spaceKey;
                        var enter = Keyboard.current.enterKey;
                        if (space.wasPressedThisFrame || enter.wasPressedThisFrame) pressedThisFrame = true;
                    }

                    if (!pressedThisFrame && Gamepad.current != null)
                    {
                        // Gamepad 的主按键通常为 buttonSouth（A 键）
                        if (Gamepad.current.buttonSouth.wasPressedThisFrame) pressedThisFrame = true;
                    }

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
                    bool releasedThisFrame = false;

                    if (Keyboard.current != null)
                    {
                        var space = Keyboard.current.spaceKey;
                        var enter = Keyboard.current.enterKey;
                        if (space.wasReleasedThisFrame || enter.wasReleasedThisFrame) releasedThisFrame = true;
                    }

                    if (!releasedThisFrame && Gamepad.current != null)
                    {
                        if (Gamepad.current.buttonSouth.wasReleasedThisFrame) releasedThisFrame = true;
                    }

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
            if (image)
            {
                image.color = setting.imageColor;
                if (setting.image != null) image.sprite = setting.image;
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
            Color startImgColor = image ? image.color : Color.white;
            Color endImgColor = setting.imageColor;
            Color startTextColor = tmpText ? tmpText.color : Color.white;
            Color endTextColor = setting.textColor;
            Sprite startSprite = image ? image.sprite : null;
            Sprite endSprite = setting.image;
            string startText = tmpText ? tmpText.text : null;
            string endText = setting.text;

            while (t < duration)
            {
                float lerp = duration > 0 ? t / duration : 1f;
                transform.localScale = Vector3.Lerp(startScale, endScale, lerp);
                if (image)
                {
                    image.color = Color.Lerp(startImgColor, endImgColor, lerp);
                    if (endSprite != null) image.sprite = endSprite;
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
            if (image)
            {
                image.color = endImgColor;
                if (endSprite != null) image.sprite = endSprite;
            }
            if (tmpText)
            {
                tmpText.color = endTextColor;
                if (!string.IsNullOrEmpty(endText)) tmpText.text = endText;
            }
        }
    }
}