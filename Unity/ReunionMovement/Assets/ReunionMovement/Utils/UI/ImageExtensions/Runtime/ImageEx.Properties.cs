using System;
using ReunionMovement.Common;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part: Shape/Effect Properties (same class, no behavior/serialization change)
    /// </summary>
    public partial class ImageEx
    {
        /// <summary>
        /// 三角形
        /// </summary>
        public TriangleImg Triangle
        {
            get => triangle;
            set
            {
                triangle = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 矩形
        /// </summary>
        public RectangleImg Rectangle
        {
            get => rectangle;
            set
            {
                rectangle = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 圆形
        /// </summary>
        public CircleImg Circle
        {
            get => circle;
            set
            {
                circle = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 五边形
        /// </summary>
        public PentagonImg Pentagon
        {
            get => pentagon;
            set
            {
                pentagon = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 六边形
        /// </summary>
        public HexagonImg Hexagon
        {
            get => hexagon;
            set
            {
                hexagon = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 圆角矩形
        /// </summary>
        public ChamferBoxImg ChamferBox
        {
            get => chamferBox;
            set
            {
                chamferBox = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 四边形（梯形 / 平行四边形 / 风筝形等）
        /// </summary>
        public QuadrilateralImg Quadrilateral
        {
            get => quadrilateral;
            set
            {
                quadrilateral = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// N星形多边形
        /// </summary>
        public NStarPolygonImg NStarPolygon
        {
            get => nStarPolygon;
            set
            {
                nStarPolygon = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 爱心形状
        /// </summary>
        public HeartImg Heart
        {
            get => heart;
            set
            {
                heart = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 圆润十字形
        /// </summary>
        public BlobbyCrossImg BlobbyCross
        {
            get => blobbyCross;
            set
            {
                blobbyCross = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 圆角四边形
        /// </summary>
        public SquircleImg Squircle
        {
            get => squircle;
            set
            {
                squircle = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 圆角三角形
        /// </summary>
        public NTriangleRoundedImg NTriangleRounded
        {
            get => nTriangleRounded;
            set
            {
                nTriangleRounded = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 渐变效果
        /// </summary>
        public GradientEffect GradientEffect
        {
            get => gradientEffect;
            set
            {
                gradientEffect = value;
                SetMaterialDirty();
            }
        }

        // ============================================================
        // 色调滤镜（TONE FILTER）
        // ============================================================

        /// <summary>
        /// 色调滤镜类型：None / Grayscale / Sepia / Negative / Retro / Posterize
        /// </summary>
        public ToneFilter Tone
        {
            get => m_ToneFilter;
            set
            {
                m_ToneFilter = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 色调滤镜强度 0~1
        /// </summary>
        public float ToneIntensity
        {
            get => m_ToneIntensity;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_ToneIntensity, clamped)) return;
                m_ToneIntensity = clamped;
                SetMaterialDirty();
            }
        }

        // ============================================================
        // 独立颜色滤镜（COLOR FILTER）
        // ============================================================

        /// <summary>
        /// 颜色滤镜模式：None / Multiply / Additive / Subtractive / Replace / MultiplyLuminance / MultiplyAdditive / HsvModifier / Contrast
        /// </summary>
        public ColorMode ColorFilterMode
        {
            get => m_ColorFilterMode;
            set
            {
                m_ColorFilterMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 颜色滤镜的值（含义取决于模式）
        /// </summary>
        public Color ColorValue
        {
            get => m_ColorValue;
            set
            {
                m_ColorValue = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// HSV 色相偏移 (-0.5~0.5)，对应 ColorValue.r（HsvModifier 模式）
        /// </summary>
        public float ColorHueShift
        {
            get => m_ColorValue.r;
            set { var c = m_ColorValue; c.r = Mathf.Clamp(value, -0.5f, 0.5f); ColorValue = c; }
        }

        /// <summary>
        /// HSV 饱和度偏移 (-1~1)，对应 ColorValue.g（HsvModifier 模式）
        /// </summary>
        public float ColorSaturationShift
        {
            get => m_ColorValue.g;
            set { var c = m_ColorValue; c.g = Mathf.Clamp(value, -1f, 1f); ColorValue = c; }
        }

        /// <summary>
        /// HSV 明度偏移 (-1~1)，对应 ColorValue.b（HsvModifier 模式）
        /// </summary>
        public float ColorValueShift
        {
            get => m_ColorValue.b;
            set { var c = m_ColorValue; c.b = Mathf.Clamp(value, -1f, 1f); ColorValue = c; }
        }

        /// <summary>
        /// 对比度偏移 (-1~1)，对应 ColorValue.r（Contrast 模式）
        /// </summary>
        public float ColorContrastShift
        {
            get => m_ColorValue.r;
            set { var c = m_ColorValue; c.r = Mathf.Clamp(value, -1f, 1f); ColorValue = c; }
        }

        /// <summary>
        /// 亮度偏移 (-1~1)，对应 ColorValue.g（Contrast 模式）
        /// </summary>
        public float ColorBrightnessShift
        {
            get => m_ColorValue.g;
            set { var c = m_ColorValue; c.g = Mathf.Clamp(value, -1f, 1f); ColorValue = c; }
        }

        /// <summary>
        /// 颜色滤镜强度 0~1
        /// </summary>
        public float ColorIntensity
        {
            get => m_ColorIntensity;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_ColorIntensity, clamped)) return;
                m_ColorIntensity = clamped;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 颜色滤镜发光效果
        /// </summary>
        public bool ColorGlow
        {
            get => m_ColorGlow;
            set
            {
                m_ColorGlow = value;
                SetMaterialDirty();
            }
        }

        // ============================================================
        // 边缘效果（EDGE MODE）
        // ============================================================

        /// <summary>
        /// 边缘效果模式：None / Plain / Shiny
        /// </summary>
        public EdgeMode Edge
        {
            get => m_EdgeMode;
            set
            {
                m_EdgeMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘宽度 0~1
        /// </summary>
        public float EdgeWidth
        {
            get => m_EdgeWidth;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_EdgeWidth, clamped)) return;
                m_EdgeWidth = clamped;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘颜色滤镜模式
        /// </summary>
        public ColorMode EdgeColorFilterMode
        {
            get => m_EdgeColorFilterMode;
            set
            {
                m_EdgeColorFilterMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘颜色（支持 HDR）
        /// </summary>
        public Color EdgeColor
        {
            get => m_EdgeColor;
            set
            {
                m_EdgeColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘颜色发光
        /// </summary>
        public bool EdgeColorGlow
        {
            get => m_EdgeColorGlow;
            set
            {
                m_EdgeColorGlow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘高光位置 0~1（Shiny 模式）
        /// </summary>
        public float EdgeShinyRate
        {
            get => m_EdgeShinyRate;
            set
            {
                m_EdgeShinyRate = Mathf.Clamp01(value);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘高光宽度 0~1（Shiny 模式）
        /// </summary>
        public float EdgeShinyWidth
        {
            get => m_EdgeShinyWidth;
            set
            {
                m_EdgeShinyWidth = Mathf.Clamp01(value);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 边缘高光自动播放速度 -5~5（Shiny 模式）
        /// </summary>
        public float EdgeShinyAutoPlaySpeed
        {
            get => m_EdgeShinyAutoPlaySpeed;
            set
            {
                m_EdgeShinyAutoPlaySpeed = Mathf.Clamp(value, -5f, 5f);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 采样模式：None / Pixelation / RgbShift / EdgeLuminance / EdgeAlpha
        /// </summary>
        public SamplingFilter Sampling
        {
            get => m_SamplingMode;
            set
            {
                m_SamplingMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 采样强度 0~1（像素化/色散/边缘检测强度）
        /// </summary>
        public float SamplingIntensity
        {
            get => m_SamplingIntensity;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_SamplingIntensity, clamped)) return;
                m_SamplingIntensity = clamped;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 目标模式：None / Hue / Luminance
        /// </summary>
        public TargetMode Target
        {
            get => m_TargetMode;
            set
            {
                m_TargetMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 目标颜色
        /// </summary>
        public Color TargetColor
        {
            get => m_TargetColor;
            set
            {
                m_TargetColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 目标范围 0~1
        /// </summary>
        public float TargetRange
        {
            get => m_TargetRange;
            set
            {
                m_TargetRange = Mathf.Clamp01(value);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 目标柔和度 0~1
        /// </summary>
        public float TargetSoftness
        {
            get => m_TargetSoftness;
            set
            {
                m_TargetSoftness = Mathf.Clamp01(value);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 图案区域：All / Inner / Edge
        /// </summary>
        public PatternArea TransitionPatternArea
        {
            get => m_PatternArea;
            set
            {
                m_PatternArea = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 细节滤镜模式：None / Masking / Multiply / Additive / Subtractive / Replace / MultiplyAdditive
        /// </summary>
        public DetailFilter Detail
        {
            get => m_DetailMode;
            set
            {
                m_DetailMode = value;
                SetMaterialDirty();
            }
        }

        public Texture DetailTex
        {
            get => m_DetailTex;
            set { m_DetailTex = value; SetMaterialDirty(); }
        }

        public Vector2 DetailTexScale
        {
            get => m_DetailTexScale;
            set { m_DetailTexScale = value; SetMaterialDirty(); }
        }

        public Vector2 DetailTexOffset
        {
            get => m_DetailTexOffset;
            set { m_DetailTexOffset = value; SetMaterialDirty(); }
        }

        public Vector2 DetailTexSpeed
        {
            get => m_DetailTexSpeed;
            set { m_DetailTexSpeed = value; SetMaterialDirty(); }
        }

        public float DetailIntensity
        {
            get => m_DetailIntensity;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_DetailIntensity, clamped)) return;
                m_DetailIntensity = clamped;
                SetMaterialDirty();
            }
        }

        public Vector2 DetailThreshold
        {
            get => m_DetailThreshold;
            set { m_DetailThreshold = value; SetMaterialDirty(); }
        }

        public Color DetailColor
        {
            get => m_DetailColor;
            set { m_DetailColor = value; SetMaterialDirty(); }
        }

        /// <summary>
        /// 渐变纹理模式
        /// </summary>
        public bool EnableGradientTex
        {
            get => m_EnableGradientTex;
            set { m_EnableGradientTex = value; SetMaterialDirty(); }
        }

        public Texture GradientTex
        {
            get => m_GradientTex;
            set { m_GradientTex = value; SetMaterialDirty(); }
        }

        public float GradientOffset
        {
            get => m_GradientOffset;
            set
            {
                float clamped = Mathf.Clamp(value, -1f, 1f);
                if (Mathf.Approximately(m_GradientOffset, clamped)) return;
                m_GradientOffset = clamped;
                SetMaterialDirty();
            }
        }

        public float GradientScale
        {
            get => m_GradientScale;
            set { m_GradientScale = Mathf.Clamp(value, 0.1f, 5f); SetMaterialDirty(); }
        }

        /// <summary>
        /// 混合模式：AlphaBlend / Multiply / Additive / SoftAdditive / MultiplyAdditive
        /// </summary>
        public BlendType Blend
        {
            get => m_BlendType;
            set
            {
                m_BlendType = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 模糊类型
        /// </summary>
        public BlurType Blur
        {
            get => blurType;
            set
            {
                blurType = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 模糊强度
        /// </summary>
        public float BlurIntensity
        {
            get => blurIntensity;
            set
            {
                if (Mathf.Approximately(blurIntensity, value)) return;
                blurIntensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡模式
        /// </summary>
        public TransitionMode Transition
        {
            get => transitionMode;
            set
            {
                transitionMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理
        /// </summary>
        public Texture TransitionTexture
        {
            get => transitionTexture;
            set
            {
                transitionTexture = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的缩放
        /// </summary>
        public Vector2 TransitionTexScale
        {
            get => transitionTexScale;
            set
            {
                transitionTexScale = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的缩放
        /// </summary>
        public Vector2 TransitionTexOffset
        {
            get => transitionTexOffset;
            set
            {
                transitionTexOffset = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的旋转角度
        /// </summary>
        public float TransitionTexRotation
        {
            get => transitionTexRotation;
            set
            {
                transitionTexRotation = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否保持过渡纹理的宽高比
        /// </summary>
        public bool TransitionKeepAspectRatio
        {
            get => transitionKeepAspectRatio;
            set
            {
                transitionKeepAspectRatio = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡的进度
        /// </summary>
        public float TransitionRate
        {
            get => transitionRate;
            set
            {
                if (Mathf.Approximately(transitionRate, value)) return;
                transitionRate = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡颜色
        /// </summary>
        public Color TransitionColor
        {
            get => transitionColor;
            set
            {
                transitionColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡 HSV 色相偏移 (-0.5~0.5)，对应 TransitionColor.r（HsvModifier 模式）
        /// </summary>
        public float TransitionColorHueShift
        {
            get => transitionColor.r;
            set { var c = transitionColor; c.r = Mathf.Clamp(value, -0.5f, 0.5f); TransitionColor = c; }
        }

        public float TransitionColorSaturationShift
        {
            get => transitionColor.g;
            set { var c = transitionColor; c.g = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        public float TransitionColorValueShift
        {
            get => transitionColor.b;
            set { var c = transitionColor; c.b = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        public float TransitionColorContrastShift
        {
            get => transitionColor.r;
            set { var c = transitionColor; c.r = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        public float TransitionColorBrightnessShift
        {
            get => transitionColor.g;
            set { var c = transitionColor; c.g = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        /// <summary>
        /// 过渡的宽度
        /// </summary>
        public float TransitionWidth
        {
            get => transitionWidth;
            set
            {
                transitionWidth = value;
                SetMaterialDirty();
            }
        }

        public float TransitionSoftness
        {
            get => transitionSoftness;
            set
            {
                transitionSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否反转过渡效果
        /// </summary>
        public bool TransitionReverse
        {
            get => transitionReverse;
            set
            {
                transitionReverse = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的移动速度
        /// </summary>
        public Vector2 TransitionSpeed
        {
            get => transitionSpeed;
            set
            {
                transitionSpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否反转过渡纹理的图案
        /// </summary>
        public bool TransitionPatternReverse
        {
            get => transitionPatternReverse;
            set
            {
                transitionPatternReverse = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡自动播放速度
        /// </summary>
        public float TransitionAutoPlaySpeed
        {
            get => transitionAutoPlaySpeed;
            set
            {
                transitionAutoPlaySpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡颜色滤镜模式
        /// </summary>
        public ColorMode TransitionColorFilter
        {
            get => transitionColorFilter;
            set
            {
                transitionColorFilter = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否启用过渡颜色发光效果
        /// </summary>
        public bool TransitionColorGlow
        {
            get => transitionColorGlow;
            set
            {
                transitionColorGlow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡渐变纹理
        /// </summary>
        public Texture TransitionGradient
        {
            get => transitionGradient;
            set
            {
                transitionGradient = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡渐变的值
        /// </summary>
        public Gradient TransitionGradientValue
        {
            get => transitionGradientValue;
            set
            {
                transitionGradientValue = value;
                RefreshTransitionGradient();
            }
        }

        /// <summary>
        /// 过渡渐变的范围
        /// </summary>
        public Vector2 TransitionRange
        {
            get => transitionRange;
            set
            {
                transitionRange = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否在过渡过程中夹紧纹理坐标
        /// </summary>
        public bool TransitionClamp
        {
            get => transitionClamp;
            set
            {
                transitionClamp = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理夹紧的填充量
        /// </summary>
        public float TransitionTexClampPadding
        {
            get => transitionTexClampPadding;
            set
            {
                transitionTexClampPadding = Mathf.Clamp(value, 0, 4);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否使用UV0坐标进行过渡效果
        /// </summary>
        public bool TransitionUseUv0
        {
            get => transitionUseUv0;
            set
            {
                transitionUseUv0 = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影颜色
        /// </summary>
        public Color ShadowColor
        {
            get => shadowColor;
            set
            {
                shadowColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影模糊强度
        /// </summary>
        public float ShadowBlurIntensity
        {
            get => shadowBlurIntensity;
            set
            {
                shadowBlurIntensity = Mathf.Clamp(value, 0f, 8f);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影采样宽度
        /// </summary>
        public float SamplingWidth
        {
            get => samplingWidth;
            set
            {
                samplingWidth = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影采样缩放比例
        /// </summary>
        public float SamplingScale
        {
            get => samplingScale;
            set
            {
                samplingScale = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影缩放比例。1 为原始大小，>1 放大，<1 缩小。
        /// </summary>
        public float ShadowScale
        {
            get => shadowScale;
            set
            {
                shadowScale = Mathf.Clamp(value, 0.1f, 4f);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否允许阴影超出边界绘制
        /// </summary>
        public bool AllowOutOfBoundsShadow
        {
            get => allowOutOfBoundsShadow;
            set
            {
                allowOutOfBoundsShadow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影模式
        /// </summary>
        public ShadowMode Shadow
        {
            get => shadowMode;
            set
            {
                shadowMode = value;
                SetMaterialDirty();
            }
        }
    }
}
