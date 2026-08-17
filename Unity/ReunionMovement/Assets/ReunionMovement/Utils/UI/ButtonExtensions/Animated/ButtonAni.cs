using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ReunionMovement.UI.ButtonClick;

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
        [Tooltip("目标缩放（相对初始缩放）")]
        public Vector3 scale = Vector3.one;

        [Tooltip("目标文本；留空保持当前文本")]
        public string text;

        public Color textColor = Color.white;

        [Tooltip("目标图片；留空保持当前图片")]
        public Sprite image;

        public Color imageColor = Color.white;

        [Tooltip("该状态专属动画时长；<=0 时使用全局 transitionDuration")]
        public float durationOverride = -1f;

        [Tooltip("切换到该状态时播放的音效（可选）")]
        public AudioClip audioClip;
    }

    /// <summary>状态切换事件（参数为切换后的状态）</summary>
    [System.Serializable]
    public class ButtonAniStateEvent : UnityEvent<ButtonAniState> { }

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

        [Header("动画")]
        public float transitionDuration = 0.2f;

        [Tooltip("动画缓动曲线；曲线长度不足 2 时退化为线性插值")]
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("弹性缩放（Punch）")]
        [Tooltip("启用后缩放过渡带 overshoot 回弹")]
        public bool usePunch = false;

        [Range(0.1f, 3f)]
        [Tooltip("回弹强度；1 = 标准约 10% overshoot")]
        public float punchStrength = 1f;

        [Header("音效")]
        [Tooltip("播放音效的 AudioSource；留空时自动取本物体上的组件")]
        public AudioSource audioSource;

        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Header("事件")]
        public ButtonAniStateEvent onStateChanged;

        private Image targetImage;
        private TextMeshProUGUI tmpText;

        // 记录初始缩放：动画目标 = baseScale * setting.scale，避免覆盖布局所需的基础缩放
        private Vector3 baseScale = Vector3.one;

        // 当前状态（用于去重与事件/音效派发）
        private ButtonAniState currentState = ButtonAniState.Normal;

        // 动画取消令牌（UniTask 零 GC，替代协程）
        private CancellationTokenSource animCts;
        private CancellationTokenSource submitCts;

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

        [SerializeField]
        private ButtonInputHelper.GamepadButtonType[] gamepadTriggerButtons = new ButtonInputHelper.GamepadButtonType[] { ButtonInputHelper.GamepadButtonType.South };

        // 支持键盘与手柄触发（可配置）
        private bool submitPressed = false;

        protected override void Awake()
        {
            base.Awake();
            EnsureCaches();
            baseScale = transform.localScale;
        }

        /// <summary>懒加载缓存（DoStateTransition 可能在 Awake 前被 interactable setter 触发）</summary>
        private void EnsureCaches()
        {
            if (targetImage == null) targetImage = GetComponent<Image>();
            if (tmpText == null) tmpText = GetComponentInChildren<TextMeshProUGUI>();
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
            // 编辑器非播放模式下异步动画永不恢复，ApplyState 内部会自动走同步路径（修复 Inspector 无即时预览）
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
        /// 重写基类状态过渡：统一收口所有视觉状态切换。
        /// 基类在 OnPointerEnter/Down/Up/Exit/Select/Deselect 以及 interactable 变化时都会调用本方法，
        /// 因此无需再在各事件处理器中重复应用状态（修复运行时 interactable 变化不刷新视觉的问题）。
        /// </summary>
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            ButtonAniState target;
            switch (state)
            {
                case SelectionState.Highlighted: target = ButtonAniState.Highlighted; break;
                case SelectionState.Pressed: target = ButtonAniState.Pressed; break;
                case SelectionState.Selected: target = ButtonAniState.Selected; break;
                case SelectionState.Disabled: target = ButtonAniState.Disabled; break;
                default: target = ButtonAniState.Normal; break;
            }

            // interactable=false 时任何状态都呈现 Disabled 样式
            if (!interactable && target != ButtonAniState.Disabled)
            {
                target = ButtonAniState.Disabled;
                instant = true;
            }

            ApplyState(target, instant);
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
        /// 按交互状态应用视觉：不可交互时强制切 Disabled（立即生效）
        /// </summary>
        private void ApplyInteractableState(ButtonAniState state)
        {
            ApplyState(interactable ? state : ButtonAniState.Disabled, !interactable);
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
                        // 先表现按下状态
                        ApplyInteractableState(ButtonAniState.Pressed);
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
                    ApplyInteractableState(ButtonAniState.Normal);
                }
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

                // 不要停止 animCts（会导致 Pressed 动画立即中止）。
                // 使用单独的 submit 任务等待动画时长后切回 Normal。
                submitCts?.Cancel();
                submitCts?.Dispose();
                submitCts = new CancellationTokenSource();
                SubmitAnimationAsync(submitCts.Token).Forget();
            }
            else
            {
                ApplyState(ButtonAniState.Disabled, true);
            }
        }

        /// <summary>
        /// 提交动画（UniTask 零 GC，替代协程）
        /// </summary>
        private async UniTaskVoid SubmitAnimationAsync(CancellationToken ct)
        {
            // 等待完整的动画时长（忽略 timeScale），按 normal 状态时长覆盖
            float waitTime = GetDuration(GetSetting(ButtonAniState.Normal));
            if (waitTime <= 0f) waitTime = 0.05f;
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(waitTime), ignoreTimeScale: true, delayTiming: PlayerLoopTiming.Update, cancellationToken: ct).SuppressCancellationThrow();
            if (canceled) return;

            ApplyInteractableState(ButtonAniState.Normal);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // 停止未完成的提交任务与动画，避免失活后继续执行
            submitCts?.Cancel();
            submitCts?.Dispose();
            submitCts = null;

            animCts?.Cancel();
            animCts?.Dispose();
            animCts = null;

            // 清理按键/手柄状态
            submitPressed = false;

            // 直接同步设置属性，避免失活时启动动画
            ApplySettingImmediate(GetSetting(ButtonAniState.Disabled));
            currentState = ButtonAniState.Disabled;
        }

        /// <summary>
        /// 获取状态对应的设置
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private ButtonAniSetting GetSetting(ButtonAniState state)
        {
            // Inspector 未配置某状态时回退到 normal；
            // normal 也未配置时使用默认值兜底，避免 OnEnable/OnValidate 立即 NRE
            ButtonAniSetting setting = state switch
            {
                ButtonAniState.Highlighted => highlighted,
                ButtonAniState.Pressed => pressed,
                ButtonAniState.Selected => selected,
                ButtonAniState.Disabled => disabled,
                _ => normal
            };
            return setting ?? normal ?? defaultSetting;
        }

        /// <summary>默认设置兜底（所有字段使用类默认值），当 Inspector 未配置任何状态时使用</summary>
        private static readonly ButtonAniSetting defaultSetting = new ButtonAniSetting();

        /// <summary>获取某状态的动画时长（状态级覆盖优先，否则用全局 transitionDuration）</summary>
        private float GetDuration(ButtonAniSetting setting)
        {
            return setting != null && setting.durationOverride > 0f ? setting.durationOverride : transitionDuration;
        }

        /// <summary>
        /// 应用状态设置
        /// </summary>
        /// <param name="state">目标状态</param>
        /// <param name="instant">true 时跳过动画直接落位</param>
        /// <param name="force">true 时即使状态未变化也重新应用（编辑器预览用）</param>
        public void ApplyState(ButtonAniState state, bool instant = false, bool force = false)
        {
            EnsureCaches();

            var setting = GetSetting(state);

            // 停止进行中的动画
            animCts?.Cancel();
            animCts?.Dispose();
            animCts = null;

            bool stateChanged = force || currentState != state;
            currentState = state;

            // 编辑器非播放/未激活时协程与 UniTask 循环均不会恢复，必须同步落位（修复 Inspector 无即时预览）
            if (instant || !Application.isPlaying || !isActiveAndEnabled)
            {
                ApplySettingImmediate(setting);
            }
            else
            {
                animCts = new CancellationTokenSource();
                AnimateToAsync(setting, animCts.Token).Forget();
            }

            if (stateChanged)
            {
                onStateChanged?.Invoke(state);
                PlayAudio(setting);
            }
        }

        /// <summary>同步应用设置（无动画），用于 instant / 编辑器 / 失活路径</summary>
        private void ApplySettingImmediate(ButtonAniSetting setting)
        {
            transform.localScale = Vector3.Scale(baseScale, setting.scale);
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
        /// 执行动画（UniTask 循环，零 GC，替代协程）
        /// </summary>
        private async UniTaskVoid AnimateToAsync(ButtonAniSetting setting, CancellationToken ct)
        {
            float duration = GetDuration(setting);
            if (duration <= 0f)
            {
                ApplySettingImmediate(setting);
                return;
            }

            Vector3 startScale = transform.localScale;
            Vector3 endScale = Vector3.Scale(baseScale, setting.scale);
            Color startImgColor = targetImage ? targetImage.color : Color.white;
            Color endImgColor = setting.imageColor;
            Color startTextColor = tmpText ? tmpText.color : Color.white;
            Color endTextColor = setting.textColor;

            // sprite/text 无插值语义：首帧即切换
            if (targetImage && setting.image != null) targetImage.sprite = setting.image;
            if (tmpText && !string.IsNullOrEmpty(setting.text)) tmpText.text = setting.text;

            float t = 0f;
            while (t < duration)
            {
                // 每帧 yield，取消时静默退出
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
                if (canceled) return;

                t += Time.unscaledDeltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                float eased = EvaluateCurve(lerp);
                // Punch：缩放走 easeOutBack（overshoot 回弹），其余属性走缓动曲线
                float scaleLerp = usePunch ? EaseOutBack(lerp, punchStrength) : eased;

                transform.localScale = Vector3.Lerp(startScale, endScale, scaleLerp);
                if (targetImage) targetImage.color = Color.Lerp(startImgColor, endImgColor, eased);
                if (tmpText) tmpText.color = Color.Lerp(startTextColor, endTextColor, eased);
            }

            // 兜底精确落位（避免浮点误差残留）
            transform.localScale = endScale;
            if (targetImage) targetImage.color = endImgColor;
            if (tmpText) tmpText.color = endTextColor;
        }

        /// <summary>缓动曲线求值；曲线非法时退化为线性</summary>
        private float EvaluateCurve(float lerp)
        {
            if (easeCurve != null && easeCurve.length > 1)
            {
                float v = easeCurve.Evaluate(lerp);
                if (!float.IsNaN(v)) return v;
            }
            return lerp;
        }

        /// <summary>
        /// easeOutBack：t∈[0,1] 末端带 overshoot 回弹，强度由 punchStrength 控制
        /// （1 = 标准约 10% overshoot）
        /// </summary>
        private static float EaseOutBack(float t, float punchStrength)
        {
            float c1 = 1.70158f * Mathf.Max(0.1f, punchStrength);
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>播放状态音效（可选）</summary>
        private void PlayAudio(ButtonAniSetting setting)
        {
            if (setting == null || setting.audioClip == null) return;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) return;
            audioSource.PlayOneShot(setting.audioClip, audioVolume);
        }
    }
}