using System;
using UnityEngine;
using UnityEngine.Events;

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 效果动画器：使用 AnimationCurve 驱动 ImageEx 效果参数的渐变过渡。
    /// 支持循环/往返/方向控制，可通过 CullingMask 选择性动画指定效果组。
    /// </summary>
    [AddComponentMenu("UI/ReunionMovement/ImageEx Tweener")]
    [RequireComponent(typeof(ImageEx))]
    [ExecuteAlways]
    public class ImageExTweener : MonoBehaviour
    {
        // ---- 可动画的效果组 ----
        [Flags]
        public enum CullingMask
        {
            Tone        = 1 << 0,  // 色调滤镜强度
            Color       = 1 << 1,  // 颜色滤镜强度
            Blur        = 1 << 2,  // 模糊强度
            Sampling    = 1 << 3,  // 采样强度
            Transition  = 1 << 4,  // 过渡进度
            Edge        = 1 << 5,  // 边缘宽度
            Detail      = 1 << 6,  // 细节强度
            Gradient    = 1 << 7,  // 渐变偏移
        }

        public enum WrapMode
        {
            Once,           // 播放一次后停止
            Loop,           // 循环播放
            PingPong,       // 往返一次后停止
            PingPongLoop    // 往返循环
        }

        public enum UpdateMode
        {
            Normal,         // Time.deltaTime
            Unscaled,       // Time.unscaledDeltaTime
            Manual          // 手动调用 Step()
        }

        public enum Direction
        {
            Forward,
            Reverse
        }

        public enum PlayOnEnable
        {
            None,
            Forward,
            Reverse,
            KeepDirection
        }

        // ---- 序列化字段 ----
        [SerializeField] private CullingMask m_CullingMask = (CullingMask)(-1);
        [SerializeField] private AnimationCurve m_Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private WrapMode m_WrapMode = WrapMode.Once;
        [SerializeField] private UpdateMode m_UpdateMode = UpdateMode.Normal;
        [SerializeField] private Direction m_Direction = Direction.Forward;
        [SerializeField] private PlayOnEnable m_PlayOnEnable = PlayOnEnable.Forward;
        [SerializeField] private float m_Duration = 1f;

        [Header("反向曲线")]
        [SerializeField] private bool m_SeparateReverseCurve = false;
        [SerializeField] private AnimationCurve m_ReverseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("事件")]
        public UnityEvent OnCompleted;

        // ---- 运行时状态 ----
        private ImageEx m_Target;
        private float m_Time;
        private bool m_Playing;
        private bool m_Completed;
        private Direction m_LastDirection; // 用于 KeepDirection

        // 缓存的基础值
        private float m_BaseToneIntensity;
        private float m_BaseColorIntensity;
        private float m_BaseBlurIntensity;
        private float m_BaseSamplingIntensity;
        private float m_BaseTransitionRate;
        private float m_BaseEdgeWidth;
        private float m_BaseDetailIntensity;
        private float m_BaseGradientOffset;

        // ---- 公共属性 ----
        public CullingMask Mask { get => m_CullingMask; set => m_CullingMask = value; }
        public AnimationCurve Curve { get => m_Curve; set => m_Curve = value; }
        public WrapMode Wrap { get => m_WrapMode; set => m_WrapMode = value; }
        public Direction Dir { get => m_Direction; set => m_Direction = value; }
        public float Duration { get => m_Duration; set => m_Duration = Mathf.Max(0.01f, value); }
        public bool IsPlaying => m_Playing;

        private void Awake()
        {
            m_Target = GetComponent<ImageEx>();
        }

        private void OnEnable()
        {
            if (m_Target == null) m_Target = GetComponent<ImageEx>();

            switch (m_PlayOnEnable)
            {
                case PlayOnEnable.None:
                    break;
                case PlayOnEnable.Forward:
                    Play(Direction.Forward);
                    break;
                case PlayOnEnable.Reverse:
                    Play(Direction.Reverse);
                    break;
                case PlayOnEnable.KeepDirection:
                    if (m_Playing)
                        Resume();
                    else
                        Play(m_LastDirection);
                    break;
            }
        }

        private void OnDisable()
        {
            m_Playing = false;
        }

        private void Update()
        {
            if (!m_Playing) return;
            if (m_UpdateMode == UpdateMode.Manual) return;

            float dt = m_UpdateMode == UpdateMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            Step(dt);
        }

        /// <summary>
        /// 以指定方向开始播放。
        /// </summary>
        public void Play(Direction direction)
        {
            m_Direction = direction;
            m_LastDirection = direction;
            Play();
        }

        /// <summary>
        /// 以当前方向开始/重新播放。
        /// </summary>
        [ContextMenu("Play")]
        public void Play()
        {
            m_Time = 0;
            m_Playing = true;
            m_Completed = false;
            m_LastDirection = m_Direction;
            CaptureBaseValues();
        }

        [ContextMenu("Pause")]
        public void Pause() { m_Playing = false; }

        [ContextMenu("Resume")]
        public void Resume() { m_Playing = true; }

        [ContextMenu("Stop")]
        public void Stop()
        {
            m_Playing = false;
            m_Time = 0;
            m_Completed = false;
        }

        /// <summary>
        /// 手动推进动画。
        /// </summary>
        public void Step(float deltaTime)
        {
            if (!m_Playing || m_Completed) return;
            if (m_Target == null) return;

            m_Time += deltaTime;
            float duration = Mathf.Max(0.01f, m_Duration);
            float rawT = Mathf.Clamp01(m_Time / duration);

            // WrapMode
            switch (m_WrapMode)
            {
                case WrapMode.Once:
                    if (rawT >= 1f) { rawT = 1f; m_Playing = false; m_Completed = true; OnCompleted?.Invoke(); }
                    break;
                case WrapMode.Loop:
                    rawT = rawT % 1f;
                    break;
                case WrapMode.PingPong:
                    if (rawT >= 1f) { rawT = 1f; m_Playing = false; m_Completed = true; OnCompleted?.Invoke(); }
                    break;
                case WrapMode.PingPongLoop:
                {
                    float loop = rawT % 2f;
                    rawT = loop > 1f ? 2f - loop : loop;
                    break;
                }
            }

            // Direction + reverse curve
            bool isReversed = m_Direction == Direction.Reverse;
            if (m_WrapMode == WrapMode.PingPong && rawT >= 1f) isReversed = !isReversed;

            if (isReversed)
            {
                rawT = 1f - rawT;
            }

            AnimationCurve activeCurve = m_Curve;
            if (isReversed && m_SeparateReverseCurve)
            {
                activeCurve = m_ReverseCurve;
                // 反向时 t 重新映射：0 = reverse 开始
                rawT = 1f - rawT; // 改回来用正向 t
            }

            float factor = Mathf.Clamp01(activeCurve.Evaluate(rawT));
            ApplyFactor(factor);
        }

        private void CaptureBaseValues()
        {
            if (m_Target == null) return;
            m_BaseToneIntensity = m_Target.ToneIntensity;
            m_BaseColorIntensity = m_Target.ColorIntensity;
            m_BaseBlurIntensity = m_Target.BlurIntensity;
            m_BaseSamplingIntensity = m_Target.SamplingIntensity;
            m_BaseTransitionRate = m_Target.TransitionRate;
            m_BaseEdgeWidth = m_Target.EdgeWidth;
            m_BaseDetailIntensity = m_Target.DetailIntensity;
            m_BaseGradientOffset = m_Target.GradientOffset;
        }

        private void ApplyFactor(float factor)
        {
            if (m_Target == null) return;

            if ((m_CullingMask & CullingMask.Tone) != 0)
                m_Target.ToneIntensity = Mathf.Lerp(0, m_BaseToneIntensity, factor);

            if ((m_CullingMask & CullingMask.Color) != 0)
                m_Target.ColorIntensity = Mathf.Lerp(0, m_BaseColorIntensity, factor);

            if ((m_CullingMask & CullingMask.Blur) != 0)
                m_Target.BlurIntensity = Mathf.Lerp(0, m_BaseBlurIntensity, factor);

            if ((m_CullingMask & CullingMask.Sampling) != 0)
                m_Target.SamplingIntensity = Mathf.Lerp(0, m_BaseSamplingIntensity, factor);

            if ((m_CullingMask & CullingMask.Transition) != 0)
                m_Target.TransitionRate = Mathf.Lerp(0, m_BaseTransitionRate, factor);

            if ((m_CullingMask & CullingMask.Edge) != 0)
                m_Target.EdgeWidth = Mathf.Lerp(0, m_BaseEdgeWidth, factor);

            if ((m_CullingMask & CullingMask.Detail) != 0)
                m_Target.DetailIntensity = Mathf.Lerp(0, m_BaseDetailIntensity, factor);

            if ((m_CullingMask & CullingMask.Gradient) != 0)
                m_Target.GradientOffset = Mathf.Lerp(0, m_BaseGradientOffset, factor);
        }
    }
}
