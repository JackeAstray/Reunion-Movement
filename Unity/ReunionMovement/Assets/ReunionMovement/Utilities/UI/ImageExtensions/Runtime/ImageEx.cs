using System;
using ReunionMovement.Common;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReunionMovement.UI.ImageExtensions
{
    [AddComponentMenu("UI/ReunionMovement/ImageEx")]
    public partial class ImageEx : Image
    {
        [SerializeField] private bool appendShadow = false;
        [SerializeField] private Vector2 shadowOffsetLocal = new Vector2(8, -8);
        [SerializeField][ColorUsage(true, true)] private Color shadowColor = new Color(0, 0, 0, 0.5f);
        [SerializeField][Range(0, 100)] private float shadowBlurIntensity = 1f;
        [SerializeField] private float samplingWidth = 1f;
        [SerializeField] private float samplingScale = 1f;
        [SerializeField][Range(0.1f, 4f)] private float shadowScale = 1f;
        [SerializeField] private bool allowOutOfBoundsShadow = true;
        [SerializeField] private ShadowMode shadowMode = ShadowMode.Shadow;

        public bool AppendShadow
        {
            get => appendShadow;
            set
            {
                appendShadow = value;
                base.SetMaterialDirty();
                base.SetVerticesDirty();
            }
        }

        public Vector2 ShadowOffsetLocal
        {
            get => shadowOffsetLocal;
            set
            {
                shadowOffsetLocal = value;
                base.SetMaterialDirty();
                base.SetVerticesDirty();
            }
        }

        public enum BlurType
        {
            None = 0,
            Fast = 1,
            Medium = 2,
            Detail = 3
        }

        public enum ShadowMode
        {
            Shadow = 1,
            Shadow3 = 2,
            Mirror = 3,
            Outline8 = 8
        }

        public enum ShadowDirection
        {
            Vertical = 0,
            Horizontal = 1
        }

        public enum TransitionMode
        {
            None = 0,
            Fade = 1,
            Cutoff = 2,
            Dissolve = 3,
            Shiny = 4,
            Mask = 5,
            Melt = 6,
            Burn = 7,
            Pattern = 8,
            Blaze = 9
        }

        public enum ColorMode
        {
            None = 0,
            Multiply = 1,
            Additive = 2,
            Subtractive = 3,
            Replace = 4,
            MultiplyLuminance = 5,
            MultiplyAdditive = 6,
            HsvModifier = 7,
            Contrast = 8,
        }

        public enum ToneFilter
        {
            None = 0,
            Grayscale = 1,
            Sepia = 2,
            Negative = 3,
            Retro = 4,
            Posterize = 5
        }

        public enum EdgeMode
        {
            None = 0,
            Plain = 1,
            Shiny = 2
        }

        public enum SamplingFilter
        {
            None = 0,
            Pixelation = 4,
            RgbShift = 5,
            EdgeLuminance = 6,
            EdgeAlpha = 7
        }

        public enum TargetMode
        {
            None = 0,
            Hue = 1,
            Luminance = 2
        }

        public enum PatternArea
        {
            All = 0,
            Inner = 1,
            Edge = 2
        }

        public enum DetailFilter
        {
            None = 0,
            Masking = 1,
            Multiply = 2,
            Additive = 3,
            Subtractive = 6,
            Replace = 4,
            MultiplyAdditive = 5
        }

        public enum BlendType
        {
            AlphaBlend,
            Multiply,
            Additive,
            SoftAdditive,
            MultiplyAdditive
        }

        #region 常量
        public const string shaderName = "ReunionMovement/UI/ImageEx";
        #endregion

        #region 序列化字段

        [SerializeField] private DrawShape drawShape = DrawShape.None;
        [SerializeField] private Type imageType = Type.Simple;
        [SerializeField] private MaterialMode materialMode;

        [SerializeField] private BlurType blurType = BlurType.None;
        [SerializeField][Range(0, 1)] private float blurIntensity = 1f;

        [SerializeField] private TransitionMode transitionMode = TransitionMode.None;
        [SerializeField] private Texture transitionTexture;
        [SerializeField] private Vector2 transitionTexScale = Vector2.one;
        [SerializeField] private Vector2 transitionTexOffset = Vector2.zero;
        [SerializeField] private float transitionTexRotation = 0;
        [SerializeField] private bool transitionKeepAspectRatio;
        [SerializeField][Range(0, 1)] private float transitionRate = 0f;
        [SerializeField][ColorUsage(true, true)] private Color transitionColor = Color.white;
        [SerializeField][Range(0, 1)] private float transitionWidth = 0.1f;
        [SerializeField][Range(0, 1)] private float transitionSoftness = 0.1f;
        [SerializeField] private bool transitionReverse;
        [SerializeField] private Vector2 transitionSpeed;
        [SerializeField] private bool transitionPatternReverse;
        [SerializeField] private float transitionAutoPlaySpeed;
        [SerializeField] private ColorMode transitionColorFilter;
        [SerializeField] private bool transitionColorGlow;
        [SerializeField] private Texture transitionGradient;
        [SerializeField][GradientUsage(true)] private Gradient transitionGradientValue;

        /// <summary>过渡渐变纹理是否为运行时生成（替换时仅销毁运行时纹理，避免误删用户资产）</summary>
        private bool transitionGradientIsRuntime;
        [SerializeField] private Vector2 transitionRange;
        [SerializeField] private bool transitionClamp = true;
        [SerializeField][Range(0, 4)] private float transitionTexClampPadding = 1f;
        [SerializeField] private bool transitionUseUv0 = true;

        [SerializeField] private float strokeWidth;

        [SerializeField] private float outlineWidth;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField] private ShadowDirection shadowMirrorDirection = ShadowDirection.Vertical;
        [SerializeField][Range(0f, 2f)] private float shadowMirrorScale = 1f;
        [SerializeField] private Vector2 shadowMirrorOffset = Vector2.zero;
        [SerializeField] private bool shadowMirrorShowSource = false;
        [SerializeField][Range(0f, 1f)] private float shadowMirrorTintMix = 0.5f;
        [SerializeField] private ColorMode shadowColorFilter = ColorMode.Replace;
        [SerializeField] private bool shadowColorGlow = false;
        [SerializeField] private float customTime;
        [SerializeField] private int enableDashedOutline;

        [SerializeField] private float falloffDistance = 0.5f;
        [SerializeField] private bool constrainRotation = true;
        [SerializeField] private float shapeRotation;
        [SerializeField] private bool flipHorizontal;
        [SerializeField] private bool flipVertical;
        [SerializeField] private float alphaThreshold = 0f;

        [SerializeField] private TriangleImg triangle = new TriangleImg();
        [SerializeField] private RectangleImg rectangle = new RectangleImg();
        [SerializeField] private CircleImg circle = new CircleImg();
        [SerializeField] private PentagonImg pentagon = new PentagonImg();
        [SerializeField] private HexagonImg hexagon = new HexagonImg();
        [SerializeField] private ChamferBoxImg chamferBox = new ChamferBoxImg();
        [SerializeField] private QuadrilateralImg quadrilateral = new QuadrilateralImg();
        [SerializeField] private NStarPolygonImg nStarPolygon = new NStarPolygonImg();
        [SerializeField] private HeartImg heart = new HeartImg();
        [SerializeField] private BlobbyCrossImg blobbyCross = new BlobbyCrossImg();
        [SerializeField] private SquircleImg squircle = new SquircleImg();
        [SerializeField] private NTriangleRoundedImg nTriangleRounded = new NTriangleRoundedImg();

        [SerializeField] private GradientEffect gradientEffect = new GradientEffect();

        // -------------------- 色调滤镜（TONE） --------------------
        [SerializeField] private ToneFilter m_ToneFilter = ToneFilter.None;
        [SerializeField][Range(0, 1)] private float m_ToneIntensity = 1f;

        // -------------------- 独立颜色滤镜（COLOR FILTER） --------------------
        [SerializeField] private ColorMode m_ColorFilterMode = ColorMode.None;
        [SerializeField] private Color m_ColorValue = Color.white;
        [SerializeField][Range(0, 1)] private float m_ColorIntensity = 1f;
        [SerializeField] private bool m_ColorGlow = false;

        // -------------------- 边缘效果（EDGE） --------------------
        [SerializeField] private EdgeMode m_EdgeMode = EdgeMode.None;
        [SerializeField][Range(0, 1)] private float m_EdgeWidth = 0.5f;
        [SerializeField] private ColorMode m_EdgeColorFilterMode = ColorMode.Replace;
        [SerializeField][ColorUsage(true, true)] private Color m_EdgeColor = Color.white;
        [SerializeField] private bool m_EdgeColorGlow = false;
        [SerializeField][Range(0, 1)] private float m_EdgeShinyRate = 0.5f;
        [SerializeField][Range(0, 1)] private float m_EdgeShinyWidth = 0.5f;
        [SerializeField][Range(-5, 5)] private float m_EdgeShinyAutoPlaySpeed = 1f;

        // -------------------- 采样增强（SAMPLING） --------------------
        [SerializeField] private SamplingFilter m_SamplingMode = SamplingFilter.None;
        [SerializeField][Range(0, 1)] private float m_SamplingIntensity = 0.5f;

        // -------------------- 目标模式（TARGET） --------------------
        [SerializeField] private TargetMode m_TargetMode = TargetMode.None;
        [SerializeField] private Color m_TargetColor = Color.white;
        [SerializeField][Range(0, 1)] private float m_TargetRange = 0.1f;
        [SerializeField][Range(0, 1)] private float m_TargetSoftness = 0.5f;

        // -------------------- 图案区域（PATTERN AREA） --------------------
        [SerializeField] private PatternArea m_PatternArea = PatternArea.All;

        // -------------------- 细节纹理（DETAIL FILTER） --------------------
        [SerializeField] private DetailFilter m_DetailMode = DetailFilter.None;
        [SerializeField] private Texture m_DetailTex;
        [SerializeField] private Vector2 m_DetailTexScale = Vector2.one;
        [SerializeField] private Vector2 m_DetailTexOffset = Vector2.zero;
        [SerializeField] private Vector2 m_DetailTexSpeed = Vector2.zero;
        [SerializeField][Range(0, 1)] private float m_DetailIntensity = 1f;
        [SerializeField] private Vector2 m_DetailThreshold = new Vector2(0, 1);
        [SerializeField][ColorUsage(true, true)] private Color m_DetailColor = Color.white;

        // -------------------- 渐变纹理（GRADIENT TEXTURE） --------------------
        [SerializeField] private bool m_EnableGradientTex = false;
        [SerializeField] private Texture m_GradientTex;
        [SerializeField][Range(-1, 1)] private float m_GradientOffset = 0f;
        [SerializeField][Range(0.1f, 5)] private float m_GradientScale = 1f;

        // -------------------- 混合模式（BLEND TYPE） --------------------
        [SerializeField] private BlendType m_BlendType = BlendType.AlphaBlend;

        // -------------------- 相机画面（CAMERA FEED） --------------------
        [SerializeField] private Texture cameraTexture;
        #endregion

        #region Material PropertyIds

        private static readonly int mainTex_Sp = Shader.PropertyToID("_MainTex");
        private static readonly int pixelWorldScale_Sp = Shader.PropertyToID("_PixelWorldScale");
        private static readonly int drawShape_Sp = Shader.PropertyToID("_DrawShape");
        private static readonly int strokeWidth_Sp = Shader.PropertyToID("_StrokeWidth");

        private static readonly int blurType_Sp = Shader.PropertyToID("_BlurType");
        private static readonly int blurIntensity_Sp = Shader.PropertyToID("_BlurIntensity");

        private static readonly int transitionMode_Sp = Shader.PropertyToID("_TransitionMode");
        private static readonly int transitionTex_Sp = Shader.PropertyToID("_TransitionTex");
        private static readonly int transitionTex_ST_Sp = Shader.PropertyToID("_TransitionTex_ST");
        private static readonly int transitionTexRotation_Sp = Shader.PropertyToID("_TransitionTexRotation");
        private static readonly int transitionRate_Sp = Shader.PropertyToID("_TransitionRate");
        private static readonly int transitionColor_Sp = Shader.PropertyToID("_TransitionColor");
        private static readonly int transitionWidth_Sp = Shader.PropertyToID("_TransitionWidth");
        private static readonly int transitionSoftness_Sp = Shader.PropertyToID("_TransitionSoftness");
        private static readonly int transitionReverse_Sp = Shader.PropertyToID("_TransitionReverse");
        private static readonly int transitionTexSpeed_Sp = Shader.PropertyToID("_TransitionTex_Speed");
        private static readonly int transitionPatternReverse_Sp = Shader.PropertyToID("_TransitionPatternReverse");
        private static readonly int transitionAutoPlaySpeed_Sp = Shader.PropertyToID("_TransitionAutoPlaySpeed");
        private static readonly int transitionColorFilter_Sp = Shader.PropertyToID("_TransitionColorFilter");
        private static readonly int transitionColorGlow_Sp = Shader.PropertyToID("_TransitionColorGlow");
        private static readonly int transitionGradientTex_Sp = Shader.PropertyToID("_TransitionGradientTex");
        private static readonly int transitionRange_Sp = Shader.PropertyToID("_TransitionRange");
        private static readonly int transitionClamp_Sp = Shader.PropertyToID("_TransitionClamp");
        private static readonly int transitionTexClampPadding_Sp = Shader.PropertyToID("_TransitionTexClampPadding");
        private static readonly int transitionUseUv0_Sp = Shader.PropertyToID("_TransitionUseUv0");

        private static readonly int shadowColor_Sp = Shader.PropertyToID("_ShadowColor");
        private static readonly int shadowBlurIntensity_Sp = Shader.PropertyToID("_ShadowBlurIntensity");
        private static readonly int samplingWidth_Sp = Shader.PropertyToID("_SamplingWidth");
        private static readonly int samplingScale_Sp = Shader.PropertyToID("_SamplingScale");
        private static readonly int shadowScale_Sp = Shader.PropertyToID("_ShadowScale");
        private static readonly int allowOutOfBoundsShadow_Sp = Shader.PropertyToID("_AllowOutOfBoundsShadow");
        private static readonly int shadowMode_Sp = Shader.PropertyToID("_ShadowMode");
        private static readonly int shadowMirrorDirection_Sp = Shader.PropertyToID("_ShadowMirrorDirection");
        private static readonly int shadowMirrorScale_Sp = Shader.PropertyToID("_ShadowMirrorScale");
        private static readonly int shadowMirrorOffset_Sp = Shader.PropertyToID("_ShadowMirrorOffset");
        private static readonly int shadowMirrorShowSource_Sp = Shader.PropertyToID("_ShadowMirrorShowSource");
        private static readonly int shadowMirrorTintMix_Sp = Shader.PropertyToID("_ShadowMirrorTintMix");
        private static readonly int shadowColorFilter_Sp = Shader.PropertyToID("_ShadowColorFilter");
        private static readonly int shadowColorGlow_Sp = Shader.PropertyToID("_ShadowColorGlow");

        private static readonly int outlineWidth_Sp = Shader.PropertyToID("_OutlineWidth");
        private static readonly int outlineColor_Sp = Shader.PropertyToID("_OutlineColor");
        private static readonly int enableDashedOutline_Sp = Shader.PropertyToID("_EnableDashedOutline");
        private static readonly int customTime_Sp = Shader.PropertyToID("_CustomTime");

        private static readonly int falloffDistance_Sp = Shader.PropertyToID("_FalloffDistance");
        private static readonly int shapeRotation_Sp = Shader.PropertyToID("_ShapeRotation");
        private static readonly int constrainedRotation_Sp = Shader.PropertyToID("_ConstrainRotation");
        private static readonly int flipHorizontal_Sp = Shader.PropertyToID("_FlipHorizontal");
        private static readonly int flipVertical_Sp = Shader.PropertyToID("_FlipVertical");

        // 色调滤镜
        private static readonly int toneIntensity_Sp = Shader.PropertyToID("_ToneIntensity");

        // Shared 模式回读补齐：枚举/开关属性 ID（GetModifiedMaterial 写入，InitValuesFromSharedMaterial 回读）
        private static readonly int toneFilter_Sp = Shader.PropertyToID("_ToneFilter");
        private static readonly int edgeMode_Sp = Shader.PropertyToID("_EdgeMode");
        private static readonly int samplingMode_Sp = Shader.PropertyToID("_SamplingMode");
        private static readonly int targetMode_Sp = Shader.PropertyToID("_TargetMode");
        private static readonly int detailMode_Sp = Shader.PropertyToID("_DetailMode");
        // 渐变开关必须与 shader 中的 [_Toggle] _EnableGradientTex 对齐；
        // 旧名 "_GradientTexEnabled" 在 shader 中不存在，Shared 模式回读恒为 0 会清空共享材质的渐变纹理。
        private static readonly int gradientTexEnabled_Sp = Shader.PropertyToID("_EnableGradientTex");

        // 独立颜色滤镜
        private static readonly int colorFilter_Sp = Shader.PropertyToID("_ColorFilter");
        private static readonly int colorValue_Sp = Shader.PropertyToID("_ColorValue");
        private static readonly int colorIntensity_Sp = Shader.PropertyToID("_ColorIntensity");
        private static readonly int colorGlow_Sp = Shader.PropertyToID("_ColorGlow");

        // 边缘效果
        private static readonly int edgeWidth_Sp = Shader.PropertyToID("_EdgeWidth");
        private static readonly int edgeColorFilter_Sp = Shader.PropertyToID("_EdgeColorFilter");
        private static readonly int edgeColor_Sp = Shader.PropertyToID("_EdgeColor");
        private static readonly int edgeColorGlow_Sp = Shader.PropertyToID("_EdgeColorGlow");
        private static readonly int edgeShinyRate_Sp = Shader.PropertyToID("_EdgeShinyRate");
        private static readonly int edgeShinyWidth_Sp = Shader.PropertyToID("_EdgeShinyWidth");
        private static readonly int edgeShinyAutoPlaySpeed_Sp = Shader.PropertyToID("_EdgeShinyAutoPlaySpeed");

        // 采样增强
        private static readonly int samplingIntensity_Sp = Shader.PropertyToID("_SamplingIntensity");

        // 目标模式
        private static readonly int targetColor_Sp = Shader.PropertyToID("_TargetColor");
        private static readonly int targetRange_Sp = Shader.PropertyToID("_TargetRange");
        private static readonly int targetSoftness_Sp = Shader.PropertyToID("_TargetSoftness");

        // 图案区域
        private static readonly int patternArea_Sp = Shader.PropertyToID("_PatternArea");

        // 细节纹理
        private static readonly int detailTex_Sp = Shader.PropertyToID("_DetailTex");
        private static readonly int detailTex_ST_Sp = Shader.PropertyToID("_DetailTex_ST");
        private static readonly int detailTexSpeed_Sp = Shader.PropertyToID("_DetailTex_Speed");
        private static readonly int detailIntensity_Sp = Shader.PropertyToID("_DetailIntensity");
        private static readonly int detailThreshold_Sp = Shader.PropertyToID("_DetailThreshold");
        private static readonly int detailColor_Sp = Shader.PropertyToID("_DetailColor");

        // 渐变纹理
        private static readonly int gradientTex_Sp = Shader.PropertyToID("_GradientTex");
        private static readonly int gradientOffset_Sp = Shader.PropertyToID("_GradientOffset");
        private static readonly int gradientScale_Sp = Shader.PropertyToID("_GradientScale");

        // 混合模式
        private static readonly int srcBlend_Sp = Shader.PropertyToID("_SrcBlend");
        private static readonly int dstBlend_Sp = Shader.PropertyToID("_DstBlend");

        // Init 幂等守卫：防止重复调用 ListenToComponentChanges(true) 造成事件重复订阅
        private bool m_listeningToComponents = false;

        #endregion

    }
}
