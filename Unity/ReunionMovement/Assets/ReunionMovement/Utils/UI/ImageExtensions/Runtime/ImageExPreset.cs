using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 效果预设：保存所有 Phase 1-3 的效果参数为 ScriptableObject，
    /// 可在 ImageEx 组件之间共享，也可用于 ImageExTweener 的起止状态。
    /// </summary>
    [CreateAssetMenu(menuName = "ReunionMovement/ImageEx Preset", fileName = "ImageExPreset")]
    public class ImageExPreset : ScriptableObject
    {
        [Header("渐变")]
        public bool enableGradientTex;
        public Texture gradientTex;
        [Range(-1, 1)] public float gradientOffset;
        [Range(0.1f, 5)] public float gradientScale = 1f;

        [Header("模糊")]
        public ImageEx.BlurType blurType;
        [Range(0, 1)] public float blurIntensity = 1f;

        [Header("采样增强")]
        public ImageEx.SamplingFilter samplingMode;
        [Range(0, 1)] public float samplingIntensity = 0.5f;

        [Header("目标模式")]
        public ImageEx.TargetMode targetMode;
        public Color targetColor = Color.white;
        [Range(0, 1)] public float targetRange = 0.1f;
        [Range(0, 1)] public float targetSoftness = 0.5f;

        [Header("色调滤镜")]
        public ImageEx.ToneFilter toneFilter;
        [Range(0, 1)] public float toneIntensity = 1f;

        [Header("颜色滤镜")]
        public ImageEx.ColorMode colorFilterMode;
        public Color colorValue = Color.white;
        [Range(0, 1)] public float colorIntensity = 1f;
        public bool colorGlow;

        [Header("过渡")]
        public ImageEx.TransitionMode transitionMode;
        public Texture transitionTexture;
        public Vector2 transitionTexScale = Vector2.one;
        public Vector2 transitionTexOffset = Vector2.zero;
        public float transitionTexRotation;
        public bool transitionKeepAspectRatio;
        [Range(0, 1)] public float transitionRate;
        [ColorUsage(true, true)] public Color transitionColor = Color.white;
        [Range(0, 1)] public float transitionWidth = 0.1f;
        [Range(0, 1)] public float transitionSoftness = 0.1f;
        public bool transitionReverse;
        public Vector2 transitionSpeed;
        public bool transitionPatternReverse;
        public float transitionAutoPlaySpeed;
        public ImageEx.ColorMode transitionColorFilter;
        public bool transitionColorGlow;
        public Vector2 transitionRange = new Vector2(0, 1);
        public ImageEx.PatternArea patternArea;

        [Header("边缘效果")]
        public ImageEx.EdgeMode edgeMode;
        [Range(0, 1)] public float edgeWidth = 0.5f;
        public ImageEx.ColorMode edgeColorFilterMode = ImageEx.ColorMode.Replace;
        [ColorUsage(true, true)] public Color edgeColor = Color.white;
        public bool edgeColorGlow;
        [Range(0, 1)] public float edgeShinyRate = 0.5f;
        [Range(0, 1)] public float edgeShinyWidth = 0.5f;
        [Range(-5, 5)] public float edgeShinyAutoPlaySpeed = 1f;

        [Header("细节纹理")]
        public ImageEx.DetailFilter detailMode;
        public Texture detailTex;
        public Vector2 detailTexScale = Vector2.one;
        public Vector2 detailTexOffset = Vector2.zero;
        public Vector2 detailTexSpeed = Vector2.zero;
        [Range(0, 1)] public float detailIntensity = 1f;
        public Vector2 detailThreshold = new Vector2(0, 1);
        [ColorUsage(true, true)] public Color detailColor = Color.white;

        [Header("混合")]
        public ImageEx.BlendType blendType;

        [Header("阴影")]
        public ImageEx.ShadowMode shadowMode;
        public ImageEx.ColorMode shadowColorFilter = ImageEx.ColorMode.Replace;
        public bool shadowColorGlow;

        /// <summary>
        /// 将预设应用到指定的 ImageEx 组件。
        /// </summary>
        public void ApplyTo(ImageEx target)
        {
            if (target == null) return;

            // 渐变
            target.EnableGradientTex = enableGradientTex;
            target.GradientTex = gradientTex;
            target.GradientOffset = gradientOffset;
            target.GradientScale = gradientScale;

            // 模糊
            target.Blur = blurType;
            target.BlurIntensity = blurIntensity;

            // 采样
            target.Sampling = samplingMode;
            target.SamplingIntensity = samplingIntensity;

            // 目标模式
            target.Target = targetMode;
            target.TargetColor = targetColor;
            target.TargetRange = targetRange;
            target.TargetSoftness = targetSoftness;

            // 色调
            target.Tone = toneFilter;
            target.ToneIntensity = toneIntensity;

            // 颜色滤镜
            target.ColorFilterMode = colorFilterMode;
            target.ColorValue = colorValue;
            target.ColorIntensity = colorIntensity;
            target.ColorGlow = colorGlow;

            // 过渡
            target.Transition = transitionMode;
            target.TransitionTexture = transitionTexture;
            target.TransitionTexScale = transitionTexScale;
            target.TransitionTexOffset = transitionTexOffset;
            target.TransitionTexRotation = transitionTexRotation;
            target.TransitionKeepAspectRatio = transitionKeepAspectRatio;
            target.TransitionRate = transitionRate;
            target.TransitionColor = transitionColor;
            target.TransitionWidth = transitionWidth;
            target.TransitionSoftness = transitionSoftness;
            target.TransitionReverse = transitionReverse;
            target.TransitionSpeed = transitionSpeed;
            target.TransitionPatternReverse = transitionPatternReverse;
            target.TransitionAutoPlaySpeed = transitionAutoPlaySpeed;
            target.TransitionColorFilter = transitionColorFilter;
            target.TransitionColorGlow = transitionColorGlow;
            target.TransitionRange = transitionRange;
            target.TransitionPatternArea = patternArea;

            // 边缘
            target.Edge = edgeMode;
            target.EdgeWidth = edgeWidth;
            target.EdgeColorFilterMode = edgeColorFilterMode;
            target.EdgeColor = edgeColor;
            target.EdgeColorGlow = edgeColorGlow;
            target.EdgeShinyRate = edgeShinyRate;
            target.EdgeShinyWidth = edgeShinyWidth;
            target.EdgeShinyAutoPlaySpeed = edgeShinyAutoPlaySpeed;

            // 细节
            target.Detail = detailMode;
            target.DetailTex = detailTex;
            target.DetailTexScale = detailTexScale;
            target.DetailTexOffset = detailTexOffset;
            target.DetailTexSpeed = detailTexSpeed;
            target.DetailIntensity = detailIntensity;
            target.DetailThreshold = detailThreshold;
            target.DetailColor = detailColor;

            // 混合 & 阴影
            target.Blend = blendType;
            target.Shadow = shadowMode;
            target.ShadowColorFilter = shadowColorFilter;
            target.ShadowColorGlow = shadowColorGlow;
        }

        /// <summary>
        /// 从指定的 ImageEx 组件读取当前设置到预设。
        /// </summary>
        public void ReadFrom(ImageEx source)
        {
            if (source == null) return;

            enableGradientTex = source.EnableGradientTex;
            gradientTex = source.GradientTex;
            gradientOffset = source.GradientOffset;
            gradientScale = source.GradientScale;

            blurType = source.Blur;
            blurIntensity = source.BlurIntensity;

            samplingMode = source.Sampling;
            samplingIntensity = source.SamplingIntensity;

            targetMode = source.Target;
            targetColor = source.TargetColor;
            targetRange = source.TargetRange;
            targetSoftness = source.TargetSoftness;

            toneFilter = source.Tone;
            toneIntensity = source.ToneIntensity;

            colorFilterMode = source.ColorFilterMode;
            colorValue = source.ColorValue;
            colorIntensity = source.ColorIntensity;
            colorGlow = source.ColorGlow;

            transitionMode = source.Transition;
            transitionTexture = source.TransitionTexture;
            transitionTexScale = source.TransitionTexScale;
            transitionTexOffset = source.TransitionTexOffset;
            transitionTexRotation = source.TransitionTexRotation;
            transitionKeepAspectRatio = source.TransitionKeepAspectRatio;
            transitionRate = source.TransitionRate;
            transitionColor = source.TransitionColor;
            transitionWidth = source.TransitionWidth;
            transitionSoftness = source.TransitionSoftness;
            transitionReverse = source.TransitionReverse;
            transitionSpeed = source.TransitionSpeed;
            transitionPatternReverse = source.TransitionPatternReverse;
            transitionAutoPlaySpeed = source.TransitionAutoPlaySpeed;
            transitionColorFilter = source.TransitionColorFilter;
            transitionColorGlow = source.TransitionColorGlow;
            transitionRange = source.TransitionRange;
            patternArea = source.TransitionPatternArea;

            edgeMode = source.Edge;
            edgeWidth = source.EdgeWidth;
            edgeColorFilterMode = source.EdgeColorFilterMode;
            edgeColor = source.EdgeColor;
            edgeColorGlow = source.EdgeColorGlow;
            edgeShinyRate = source.EdgeShinyRate;
            edgeShinyWidth = source.EdgeShinyWidth;
            edgeShinyAutoPlaySpeed = source.EdgeShinyAutoPlaySpeed;

            detailMode = source.Detail;
            detailTex = source.DetailTex;
            detailTexScale = source.DetailTexScale;
            detailTexOffset = source.DetailTexOffset;
            detailTexSpeed = source.DetailTexSpeed;
            detailIntensity = source.DetailIntensity;
            detailThreshold = source.DetailThreshold;
            detailColor = source.DetailColor;

            blendType = source.Blend;
            shadowMode = source.Shadow;
            shadowColorFilter = source.ShadowColorFilter;
            shadowColorGlow = source.ShadowColorGlow;
        }
    }
}
