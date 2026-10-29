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
    /// ImageEx partial part：形状与外观属性（形状 / 渐变 / 色调 / 颜色 / 边缘 / 采样 / 目标）。
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

    }
}
