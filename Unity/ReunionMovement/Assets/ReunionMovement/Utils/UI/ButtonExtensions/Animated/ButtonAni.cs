using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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
        /// <param name="eventData"></param>
        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);
            if (interactable)
            {
                ApplyState(ButtonAniState.Pressed);
                // 延迟一帧或动画时长后切回Normal
                if (animCoroutine != null)
                {
                    StopCoroutine(animCoroutine);
                }
                animCoroutine = StartCoroutine(SubmitAnimationCoroutine());
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
            yield return new WaitForSecondsRealtime(transitionDuration > 0 ? transitionDuration : 0.05f);
            if (interactable)
            {
                ApplyState(ButtonAniState.Normal);
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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
            // 不再调用 ApplyState 或启动协程
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