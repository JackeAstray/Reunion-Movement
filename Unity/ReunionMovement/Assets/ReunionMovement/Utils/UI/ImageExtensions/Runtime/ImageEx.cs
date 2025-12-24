using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ImageExtensions;
using Unity.VisualScripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReunionMovement.UI.ImageExtensions
{
    [AddComponentMenu("UI/ReunionMovement/ImageEx")]
    public class ImageEx : Image
    {
        [SerializeField] private bool appendShadow = false;
        [SerializeField] private Vector2 shadowOffsetLocal = new Vector2(8, -8);
        [SerializeField][ColorUsage(true, true)] private Color shadowColor = new Color(0, 0, 0, 0.5f);
        [SerializeField][Range(0, 8)] private float shadowBlurIntensity = 1f;
        [SerializeField] private float samplingWidth = 1f;
        [SerializeField] private float samplingScale = 1f;
        [SerializeField] private bool allowOutOfBoundsShadow = true;

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
        [SerializeField] private Vector2 transitionRange;
        [SerializeField] private bool transitionClamp = true;
        [SerializeField][Range(0, 4)] private float transitionTexClampPadding = 1f;
        [SerializeField] private bool transitionUseUv0 = true;

        [SerializeField] private float strokeWidth;

        [SerializeField] private float outlineWidth;
        [SerializeField] private Color outlineColor = Color.black;
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
        [SerializeField] private ParallelogramImg parallelogram = new ParallelogramImg();
        [SerializeField] private NStarPolygonImg nStarPolygon = new NStarPolygonImg();
        [SerializeField] private HeartImg heart = new HeartImg();
        [SerializeField] private BlobbyCrossImg blobbyCross = new BlobbyCrossImg();
        [SerializeField] private SquircleImg squircle = new SquircleImg();
        [SerializeField] private NTriangleRoundedImg nTriangleRounded = new NTriangleRoundedImg();

        [SerializeField] private GradientEffect gradientEffect = new GradientEffect();
        #endregion

        #region Material PropertyIds

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
        private static readonly int allowOutOfBoundsShadow_Sp = Shader.PropertyToID("_AllowOutOfBoundsShadow");

        private static readonly int outlineWidth_Sp = Shader.PropertyToID("_OutlineWidth");
        private static readonly int outlineColor_Sp = Shader.PropertyToID("_OutlineColor");
        private static readonly int enableDashedOutline_Sp = Shader.PropertyToID("_EnableDashedOutline");
        private static readonly int customTime_Sp = Shader.PropertyToID("_CustomTime");

        private static readonly int falloffDistance_Sp = Shader.PropertyToID("_FalloffDistance");
        private static readonly int shapeRotation_Sp = Shader.PropertyToID("_ShapeRotation");
        private static readonly int constrainedRotation_Sp = Shader.PropertyToID("_ConstrainRotation");
        private static readonly int flipHorizontal_Sp = Shader.PropertyToID("_FlipHorizontal");
        private static readonly int flipVertical_Sp = Shader.PropertyToID("_FlipVertical");

        #endregion

        #region 公共属性

        #region 绘图设置

        /// <summary>
        /// 要绘制形状的类型
        /// </summary>
        public DrawShape DrawShape
        {
            get => drawShape;
            set
            {
                drawShape = value;
                if (material == m_Material)
                {
                    m_Material.SetInt(drawShape_Sp, (int)drawShape);
                }

                base.SetMaterialDirty();
                base.SetVerticesDirty();
            }
        }

        /// <summary>
        /// 绘制形状的线条宽度。0不是线条
        /// </summary>
        public float StrokeWidth
        {
            get => strokeWidth;
            set
            {
                strokeWidth = value;
                strokeWidth = strokeWidth < 0 ? 0 : strokeWidth;
                if (material == m_Material)
                {
                    m_Material.SetFloat(strokeWidth_Sp, strokeWidth);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 绘制形状的轮廓宽度。0不是轮廓。
        /// </summary>
        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                outlineWidth = outlineWidth < 0 ? 0 : outlineWidth;
                if (m_Material == material)
                {
                    m_Material.SetFloat(outlineWidth_Sp, outlineWidth);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 轮廓的颜色。如果“轮廓宽度”的值为0，则没有效果
        /// </summary>
        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                if (m_Material == material)
                {
                    m_Material.SetColor(outlineColor_Sp, outlineColor);
                }

                base.SetMaterialDirty();
            }
        }

        public int EnableDashedOutline
        {
            get => enableDashedOutline;
            set
            {
                enableDashedOutline = value;
                if (m_Material == material)
                {
                    m_Material.SetInt(enableDashedOutline_Sp, enableDashedOutline);
                }
                base.SetMaterialDirty();
            }
        }

        public float CustomTime
        {
            get => customTime;
            set
            {
                customTime = value;
                if (m_Material == material)
                {
                    m_Material.SetFloat(customTime_Sp, customTime);
                }
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 形状的边缘衰减距离
        /// </summary>
        public float FalloffDistance
        {
            get { return falloffDistance; }
            set
            {
                falloffDistance = Mathf.Max(value, 0f);
                if (material == m_Material)
                {
                    m_Material.SetFloat(falloffDistance_Sp, falloffDistance);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 如果设置为true，则将旋转约束为0、90、270度角。
        /// 但是形状的宽度和高度根据需要进行更换以避免剪裁。
        /// 如果设置为false，则任何形状都可以以任意角度旋转，但通常会导致形状的剪裁。
        /// </summary>
        public bool ConstrainRotation
        {
            get { return constrainRotation; }
            set
            {
                constrainRotation = value;

                if (m_Material == material)
                {
                    m_Material.SetInt(constrainedRotation_Sp, value ? 1 : 0);
                }
                if (value)
                {
                    shapeRotation = ConstrainRotationValue(shapeRotation);
                }

                base.SetVerticesDirty();
                base.SetMaterialDirty();
            }
        }

        private float ConstrainRotationValue(float val)
        {
            float finalRotation = val - val % 90;
            if (Mathf.Abs(finalRotation) >= 360) finalRotation = 0;
            return finalRotation;
        }

        /// <summary>
        /// 形状的旋转
        /// </summary>
        public float ShapeRotation
        {
            get { return shapeRotation; }
            set
            {
                shapeRotation = constrainRotation ? ConstrainRotationValue(value) : value;
                if (m_Material == material)
                {
                    m_Material.SetFloat(shapeRotation_Sp, shapeRotation);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 水平翻转形状
        /// </summary>
        public bool FlipHorizontal
        {
            get { return flipHorizontal; }
            set
            {
                flipHorizontal = value;
                if (m_Material == material)
                {
                    m_Material.SetInt(flipHorizontal_Sp, flipHorizontal ? 1 : 0);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 垂直翻转形状
        /// </summary>
        public bool FlipVertical
        {
            get { return flipVertical; }
            set
            {
                flipVertical = value;
                if (m_Material == material)
                {
                    m_Material.SetInt(flipVertical_Sp, flipVertical ? 1 : 0);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// Alpha阈值
        /// </summary>
        public float AlphaThreshold
        {
            get { return alphaThreshold; }
            set
            {
                alphaThreshold = value;
                try
                {
                    alphaHitTestMinimumThreshold = alphaThreshold;
                }
                catch (InvalidOperationException) { }
            }
        }

        /// <summary>
        /// Defines what material type of use to render the shape. Dynamic or Shared.
        /// Default is Dynamic and will issue one draw call per image object. If set to shared, assigned
        /// material in the material slot will be used to render the image. It will fallback to dynamic
        /// if no material in the material slot is assigned
        /// </summary>
        public MaterialMode MaterialMode
        {
            get { return materialMode; }
            set
            {
                if (materialMode == value) return;
                materialMode = value;
                InitializeComponents();
                if (material == m_Material)
                {
                    InitValuesFromSharedMaterial();
#if UNITY_EDITOR
                    parseAgainOnValidate = true;
#endif
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 用于渲染形状的共享材质。材质必须使用“ReunionMovement/UI/ImageEx”着色器
        /// </summary>
        public override Material material
        {
            get
            {
                if (m_Material && materialMode == MaterialMode.Shared)
                {
                    return m_Material;
                }

                return DynamicMaterial;
            }
            set
            {
                m_Material = value;

                if (m_Material && materialMode == MaterialMode.Shared && m_Material.shader.name == shaderName)
                {
                    InitValuesFromSharedMaterial();
#if UNITY_EDITOR
                    parseAgainOnValidate = true;
#endif
                }

                InitializeComponents();
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 图像的类型。仅支持两种类型。简单和填充。
        /// 默认值和回退值为“简单”。
        /// </summary>
        public new Type type
        {
            get => imageType;
            set
            {
                if (imageType == value) return;
                switch (value)
                {
                    case Type.Simple:
                    case Type.Filled:
                        imageType = value;
                        break;
                    case Type.Tiled:
                    case Type.Sliced:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(value.ToString(), value, null);
                }

                base.type = imageType;
            }
        }

        #endregion

        public TriangleImg Triangle
        {
            get => triangle;
            set
            {
                triangle = value;
                SetMaterialDirty();
            }
        }

        public RectangleImg Rectangle
        {
            get => rectangle;
            set
            {
                rectangle = value;
                SetMaterialDirty();
            }
        }

        public CircleImg Circle
        {
            get => circle;
            set
            {
                circle = value;
                SetMaterialDirty();
            }
        }

        public PentagonImg Pentagon
        {
            get => pentagon;
            set
            {
                pentagon = value;
                SetMaterialDirty();
            }
        }

        public HexagonImg Hexagon
        {
            get => hexagon;
            set
            {
                hexagon = value;
                SetMaterialDirty();
            }
        }

        public ChamferBoxImg ChamferBox
        {
            get => chamferBox;
            set
            {
                chamferBox = value;
                SetMaterialDirty();
            }
        }

        public ParallelogramImg Parallelogram
        {
            get => parallelogram;
            set
            {
                parallelogram = value;
                SetMaterialDirty();
            }
        }

        public NStarPolygonImg NStarPolygon
        {
            get => nStarPolygon;
            set
            {
                nStarPolygon = value;
                SetMaterialDirty();
            }
        }

        public HeartImg Heart
        {
            get => heart;
            set
            {
                heart = value;
                SetMaterialDirty();
            }
        }

        public BlobbyCrossImg BlobbyCross
        {
            get => blobbyCross;
            set
            {
                blobbyCross = value;
                SetMaterialDirty();
            }
        }

        public SquircleImg Squircle
        {
            get => squircle;
            set
            {
                squircle = value;
                SetMaterialDirty();
            }
        }

        public NTriangleRoundedImg NTriangleRounded
        {
            get => nTriangleRounded;
            set
            {
                nTriangleRounded = value;
                SetMaterialDirty();
            }
        }

        public GradientEffect GradientEffect
        {
            get => gradientEffect;
            set
            {
                gradientEffect = value;
                SetMaterialDirty();
            }
        }

        public BlurType Blur
        {
            get => blurType;
            set
            {
                blurType = value;
                SetMaterialDirty();
            }
        }

        public float BlurIntensity
        {
            get => blurIntensity;
            set
            {
                blurIntensity = value;
                SetMaterialDirty();
            }
        }

        public TransitionMode Transition
        {
            get => transitionMode;
            set
            {
                transitionMode = value;
                SetMaterialDirty();
            }
        }

        public Texture TransitionTexture
        {
            get => transitionTexture;
            set
            {
                transitionTexture = value;
                SetMaterialDirty();
            }
        }

        public Vector2 TransitionTexScale
        {
            get => transitionTexScale;
            set
            {
                transitionTexScale = value;
                SetMaterialDirty();
            }
        }

        public Vector2 TransitionTexOffset
        {
            get => transitionTexOffset;
            set
            {
                transitionTexOffset = value;
                SetMaterialDirty();
            }
        }

        public float TransitionTexRotation
        {
            get => transitionTexRotation;
            set
            {
                transitionTexRotation = value;
                SetMaterialDirty();
            }
        }

        public bool TransitionKeepAspectRatio
        {
            get => transitionKeepAspectRatio;
            set
            {
                transitionKeepAspectRatio = value;
                SetMaterialDirty();
            }
        }

        public float TransitionRate
        {
            get => transitionRate;
            set
            {
                transitionRate = value;
                SetMaterialDirty();
            }
        }

        public Color TransitionColor
        {
            get => transitionColor;
            set
            {
                transitionColor = value;
                SetMaterialDirty();
            }
        }

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

        public bool TransitionReverse
        {
            get => transitionReverse;
            set
            {
                transitionReverse = value;
                SetMaterialDirty();
            }
        }

        public Vector2 TransitionSpeed
        {
            get => transitionSpeed;
            set
            {
                transitionSpeed = value;
                SetMaterialDirty();
            }
        }

        public bool TransitionPatternReverse
        {
            get => transitionPatternReverse;
            set
            {
                transitionPatternReverse = value;
                SetMaterialDirty();
            }
        }

        public float TransitionAutoPlaySpeed
        {
            get => transitionAutoPlaySpeed;
            set
            {
                transitionAutoPlaySpeed = value;
                SetMaterialDirty();
            }
        }

        public ColorMode TransitionColorFilter
        {
            get => transitionColorFilter;
            set
            {
                transitionColorFilter = value;
                SetMaterialDirty();
            }
        }

        public bool TransitionColorGlow
        {
            get => transitionColorGlow;
            set
            {
                transitionColorGlow = value;
                SetMaterialDirty();
            }
        }

        public Texture TransitionGradient
        {
            get => transitionGradient;
            set
            {
                transitionGradient = value;
                SetMaterialDirty();
            }
        }

        public Gradient TransitionGradientValue
        {
            get => transitionGradientValue;
            set
            {
                transitionGradientValue = value;
                RefreshTransitionGradient();
            }
        }

        public Vector2 TransitionRange
        {
            get => transitionRange;
            set
            {
                transitionRange = value;
                SetMaterialDirty();
            }
        }

        public bool TransitionClamp
        {
            get => transitionClamp;
            set
            {
                transitionClamp = value;
                SetMaterialDirty();
            }
        }

        public float TransitionTexClampPadding
        {
            get => transitionTexClampPadding;
            set
            {
                transitionTexClampPadding = Mathf.Clamp(value, 0, 4);
                SetMaterialDirty();
            }
        }

        public bool TransitionUseUv0
        {
            get => transitionUseUv0;
            set
            {
                transitionUseUv0 = value;
                SetMaterialDirty();
            }
        }

        public Color ShadowColor
        {
            get => shadowColor;
            set
            {
                shadowColor = value;
                SetMaterialDirty();
            }
        }

        public float ShadowBlurIntensity
        {
            get => shadowBlurIntensity;
            set
            {
                shadowBlurIntensity = Mathf.Clamp(value, 0f, 8f);
                SetMaterialDirty();
            }
        }

        public float SamplingWidth
        {
            get => samplingWidth;
            set
            {
                samplingWidth = value;
                SetMaterialDirty();
            }
        }

        public float SamplingScale
        {
            get => samplingScale;
            set
            {
                samplingScale = value;
                SetMaterialDirty();
            }
        }

        public bool AllowOutOfBoundsShadow
        {
            get => allowOutOfBoundsShadow;
            set
            {
                allowOutOfBoundsShadow = value;
                SetMaterialDirty();
            }
        }
        #endregion

        #region 私有变量

        private Material dynamicMaterial;

        private Material DynamicMaterial
        {
            get
            {
                if (dynamicMaterial == null)
                {
                    Shader shader = Shader.Find(shaderName);
                    if (shader == null)
                    {
                        Debug.LogWarning($"Shader '{shaderName}' not found. Falling back to 'Sprites/Default'.");
                        shader = Shader.Find("Sprites/Default");
                    }

                    if (shader != null)
                    {
                        dynamicMaterial = new Material(shader);
                        dynamicMaterial.name += " [Dynamic]";
                    }
                    else
                    {
                        // As a last resort, create a basic material to avoid null refs
                        dynamicMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
                        dynamicMaterial.name += " [Dynamic - Fallback]";
                    }
                }

                return dynamicMaterial;
            }
        }

#if UNITY_EDITOR
        private bool parseAgainOnValidate;
#endif

        private Sprite ActiveSprite
        {
            get
            {
                Sprite overrideSprite1 = overrideSprite;
                return overrideSprite1 != null ? overrideSprite1 : sprite;
            }
        }

        #endregion

#if UNITY_EDITOR
        public void UpdateSerializedValuesFromSharedMaterial()
        {
            if (m_Material && MaterialMode == MaterialMode.Shared)
            {
                InitValuesFromSharedMaterial();
                base.SetMaterialDirty();
            }
        }

        protected override void OnValidate()
        {
            InitializeComponents();
            if (parseAgainOnValidate)
            {
                InitValuesFromSharedMaterial();
                parseAgainOnValidate = false;
            }

            DrawShape = drawShape;

            StrokeWidth = strokeWidth;
            OutlineWidth = outlineWidth;
            OutlineColor = outlineColor;
            FalloffDistance = falloffDistance;
            ConstrainRotation = constrainRotation;
            ShapeRotation = shapeRotation;
            FlipHorizontal = flipHorizontal;
            FlipVertical = flipVertical;
            AlphaThreshold = alphaThreshold;

            triangle.OnValidate();
            circle.OnValidate();
            rectangle.OnValidate();
            pentagon.OnValidate();
            hexagon.OnValidate();
            chamferBox.OnValidate();
            parallelogram.OnValidate();
            nStarPolygon.OnValidate();
            heart.OnValidate();
            blobbyCross.OnValidate();
            squircle.OnValidate();
            nTriangleRounded.OnValidate();

            gradientEffect.OnValidate();

            Blur = blurType;
            BlurIntensity = blurIntensity;

            Transition = transitionMode;
            TransitionTexture = transitionTexture;
            TransitionTexScale = transitionTexScale;
            TransitionTexOffset = transitionTexOffset;
            TransitionTexRotation = transitionTexRotation;
            TransitionKeepAspectRatio = transitionKeepAspectRatio;
            TransitionRate = transitionRate;
            TransitionColor = transitionColor;
            TransitionWidth = transitionWidth;
            TransitionSoftness = transitionSoftness;
            TransitionReverse = transitionReverse;
            TransitionSpeed = transitionSpeed;
            TransitionPatternReverse = transitionPatternReverse;
            TransitionAutoPlaySpeed = transitionAutoPlaySpeed;
            TransitionColorFilter = transitionColorFilter;
            TransitionColorGlow = transitionColorGlow;
            TransitionGradient = transitionGradient;
            TransitionRange = transitionRange;
            TransitionClamp = transitionClamp;
            TransitionTexClampPadding = transitionTexClampPadding;
            TransitionUseUv0 = transitionUseUv0;

            base.OnValidate();
            base.SetMaterialDirty();
        }
#endif
        /// <summary>
        /// 刷新过渡渐变纹理
        /// </summary>
        public void RefreshTransitionGradient()
        {
            if (transitionGradientValue == null)
            {
                transitionGradientValue = new Gradient();
            }

            int width = 256;
            int height = 1;

            if (transitionGradient == null || transitionGradient.width != width || transitionGradient.height != height)
            {
                transitionGradient = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Transition Gradient",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            Texture2D tex = transitionGradient as Texture2D;
            if (tex != null)
            {
                for (int i = 0; i < width; i++)
                {
                    tex.SetPixel(i, 0, transitionGradientValue.Evaluate((float)i / (width - 1)));
                }
                tex.Apply();
            }

            SetMaterialDirty();
        }

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            circle.Init(m_Material, material, rectTransform);
            triangle.Init(m_Material, material, rectTransform);
            rectangle.Init(m_Material, material, rectTransform);
            pentagon.Init(m_Material, material, rectTransform);
            hexagon.Init(m_Material, material, rectTransform);
            chamferBox.Init(m_Material, material, rectTransform);
            parallelogram.Init(m_Material, material, rectTransform);
            nStarPolygon.Init(m_Material, material, rectTransform);
            heart.Init(m_Material, material, rectTransform);
            blobbyCross.Init(m_Material, material, rectTransform);
            squircle.Init(m_Material, material, rectTransform);
            nTriangleRounded.Init(m_Material, material, rectTransform);
            gradientEffect.Init(m_Material, material, rectTransform);
        }

        /// <summary>
        /// 修复画布中的附加着色通道
        /// </summary>
        void FixAdditionalShaderChannelsInCanvas()
        {
            Canvas c = canvas;
            if (canvas == null) return;
            AdditionalCanvasShaderChannels additionalShaderChannels = c.additionalShaderChannels;
            additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
            c.additionalShaderChannels = additionalShaderChannels;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            InitializeComponents();
            base.Reset();
        }
#else
        void Reset() {
            InitializeComponents();
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            InitializeComponents();
            FixAdditionalShaderChannelsInCanvas();
            if (m_Material && MaterialMode == MaterialMode.Shared)
            {
                InitValuesFromSharedMaterial();
            }
            ListenToComponentChanges(true);
            base.SetAllDirty();
        }

        protected override void OnDestroy()
        {
            ListenToComponentChanges(false);
            base.OnDestroy();
        }

        /// <summary>
        /// 监听组件更改
        /// </summary>
        /// <param name="toggle"></param>
        protected void ListenToComponentChanges(bool toggle)
        {
            if (toggle)
            {
                circle.onComponentSettingsChanged += OnComponentSettingsChanged;
                triangle.onComponentSettingsChanged += OnComponentSettingsChanged;
                rectangle.onComponentSettingsChanged += OnComponentSettingsChanged;
                pentagon.onComponentSettingsChanged += OnComponentSettingsChanged;
                hexagon.onComponentSettingsChanged += OnComponentSettingsChanged;
                chamferBox.onComponentSettingsChanged += OnComponentSettingsChanged;
                parallelogram.onComponentSettingsChanged += OnComponentSettingsChanged;
                nStarPolygon.onComponentSettingsChanged += OnComponentSettingsChanged;
                heart.onComponentSettingsChanged += OnComponentSettingsChanged;
                blobbyCross.onComponentSettingsChanged += OnComponentSettingsChanged;
                squircle.onComponentSettingsChanged += OnComponentSettingsChanged;
                nTriangleRounded.onComponentSettingsChanged += OnComponentSettingsChanged;
                gradientEffect.onComponentSettingsChanged += OnComponentSettingsChanged;
            }
            else
            {
                circle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                triangle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                rectangle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                pentagon.onComponentSettingsChanged -= OnComponentSettingsChanged;
                hexagon.onComponentSettingsChanged -= OnComponentSettingsChanged;
                chamferBox.onComponentSettingsChanged -= OnComponentSettingsChanged;
                parallelogram.onComponentSettingsChanged -= OnComponentSettingsChanged;
                nStarPolygon.onComponentSettingsChanged -= OnComponentSettingsChanged;
                heart.onComponentSettingsChanged -= OnComponentSettingsChanged;
                blobbyCross.onComponentSettingsChanged -= OnComponentSettingsChanged;
                squircle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                nTriangleRounded.onComponentSettingsChanged -= OnComponentSettingsChanged;
                gradientEffect.onComponentSettingsChanged -= OnComponentSettingsChanged;
            }
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            FixAdditionalShaderChannelsInCanvas();
        }

        /// <summary>
        /// 当组件设置更改时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnComponentSettingsChanged(object sender, EventArgs e)
        {
            base.SetMaterialDirty();
        }


        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            circle.UpdateCircleRadius(rectTransform);
            heart.UpdateCircleRadius(rectTransform);
            base.SetMaterialDirty();
        }

        /// <summary>
        /// 生成网格
        /// </summary>
        /// <param name="vh"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            switch (type)
            {
                case Type.Simple:
                case Type.Sliced:
                    // Use overload that can append a shadow quad. Shadow support is exposed in the editor under Transition settings.
                    ImageHelper.GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                        color, falloffDistance, appendShadow, shadowOffsetLocal);
                    break;
                case Type.Filled:
                    ImageHelper.GenerateFilledSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                        color, fillMethod, fillAmount, fillOrigin, fillClockwise, falloffDistance);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// 获取修改后的材质
        /// </summary>
        /// <param name="baseMaterial"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override Material GetModifiedMaterial(Material baseMaterial)
        {

            Material mat = base.GetModifiedMaterial(baseMaterial);


            if (m_Material && MaterialMode == MaterialMode.Shared)
            {
                InitValuesFromSharedMaterial();
            }

            DisableAllMaterialKeywords(mat);

            RectTransform rt = rectTransform;

            mat.SetFloat(outlineWidth_Sp, outlineWidth);
            mat.SetInt(enableDashedOutline_Sp, enableDashedOutline);
            mat.SetFloat(customTime_Sp, customTime);

            mat.SetFloat(strokeWidth_Sp, strokeWidth);

            mat.SetColor(outlineColor_Sp, OutlineColor);
            mat.SetFloat(falloffDistance_Sp, FalloffDistance);

            mat.SetInt(blurType_Sp, (int)blurType);
            mat.SetFloat(blurIntensity_Sp, blurIntensity);

            mat.SetInt(transitionMode_Sp, (int)transitionMode);
            mat.SetTexture(transitionTex_Sp, transitionTexture);

            Vector2 scale = transitionTexScale;
            Vector2 offset = transitionTexOffset;

            if (transitionKeepAspectRatio && transitionTexture != null && rectTransform != null)
            {
                float rectAspect = rectTransform.rect.width / rectTransform.rect.height;
                float texAspect = (float)transitionTexture.width / transitionTexture.height;

                if (texAspect > rectAspect)
                {
                    scale.y *= rectAspect / texAspect;
                    offset.y += (1 - rectAspect / texAspect) * 0.5f;
                }
                else
                {
                    scale.x *= texAspect / rectAspect;
                    offset.x += (1 - texAspect / rectAspect) * 0.5f;
                }
            }

            mat.SetVector(transitionTex_ST_Sp, new Vector4(scale.x, scale.y, offset.x, offset.y));
            mat.SetFloat(transitionTexRotation_Sp, transitionTexRotation);
            mat.SetFloat(transitionRate_Sp, transitionRate);
            mat.SetColor(transitionColor_Sp, transitionColor);
            mat.SetFloat(transitionWidth_Sp, transitionWidth);
            mat.SetFloat(transitionSoftness_Sp, transitionSoftness);
            mat.SetInt(transitionReverse_Sp, transitionReverse ? 1 : 0);
            mat.SetVector(transitionTexSpeed_Sp, transitionSpeed);
            mat.SetInt(transitionPatternReverse_Sp, transitionPatternReverse ? 1 : 0);
            mat.SetFloat(transitionAutoPlaySpeed_Sp, transitionAutoPlaySpeed);
            mat.SetInt(transitionColorFilter_Sp, (int)transitionColorFilter);
            mat.SetInt(transitionColorGlow_Sp, transitionColorGlow ? 1 : 0);
            mat.SetTexture(transitionGradientTex_Sp, transitionGradient);
            mat.SetVector(transitionRange_Sp, transitionRange);
            bool runtimeClamp = transitionClamp;
            if (transitionMode == TransitionMode.Shiny || transitionMode == TransitionMode.Mask || transitionMode == TransitionMode.Melt || transitionMode == TransitionMode.Burn)
                runtimeClamp = true;
            mat.SetFloat(transitionClamp_Sp, runtimeClamp ? 1 : 0);
            mat.SetFloat(transitionTexClampPadding_Sp, transitionTexClampPadding);
            mat.SetFloat(transitionUseUv0_Sp, transitionUseUv0 ? 1 : 0);

            // Shadow material params
            mat.SetColor(shadowColor_Sp, shadowColor);
            mat.SetFloat(shadowBlurIntensity_Sp, shadowBlurIntensity);
            mat.SetFloat(samplingWidth_Sp, samplingWidth);
            mat.SetFloat(samplingScale_Sp, samplingScale);
            mat.SetFloat(allowOutOfBoundsShadow_Sp, allowOutOfBoundsShadow ? 1f : 0f);

            switch (transitionMode)
            {
                case TransitionMode.None:
                    mat.DisableKeyword("TRANSITION_FADE");
                    mat.DisableKeyword("TRANSITION_CUTOFF");
                    mat.DisableKeyword("TRANSITION_DISSOLVE");
                    mat.DisableKeyword("TRANSITION_SHINY");
                    mat.DisableKeyword("TRANSITION_MASK");
                    mat.DisableKeyword("TRANSITION_MELT");
                    mat.DisableKeyword("TRANSITION_BURN");
                    mat.DisableKeyword("TRANSITION_PATTERN");
                    mat.DisableKeyword("TRANSITION_BLAZE");
                    break;
                case TransitionMode.Fade:
                    mat.EnableKeyword("TRANSITION_FADE");
                    break;
                case TransitionMode.Cutoff:
                    mat.EnableKeyword("TRANSITION_CUTOFF");
                    break;
                case TransitionMode.Dissolve:
                    mat.EnableKeyword("TRANSITION_DISSOLVE");
                    break;
                case TransitionMode.Shiny:
                    mat.EnableKeyword("TRANSITION_SHINY");
                    break;
                case TransitionMode.Mask:
                    mat.EnableKeyword("TRANSITION_MASK");
                    break;
                case TransitionMode.Melt:
                    mat.EnableKeyword("TRANSITION_MELT");
                    break;
                case TransitionMode.Burn:
                    mat.EnableKeyword("TRANSITION_BURN");
                    break;
                case TransitionMode.Pattern:
                    mat.EnableKeyword("TRANSITION_PATTERN");
                    break;
                case TransitionMode.Blaze:
                    mat.EnableKeyword("TRANSITION_BLAZE");
                    break;
            }

            switch (blurType)
            {
                case BlurType.None:
                    mat.DisableKeyword("BLUR_FAST");
                    mat.DisableKeyword("BLUR_MEDIUM");
                    mat.DisableKeyword("BLUR_DETAIL");
                    break;
                case BlurType.Fast:
                    mat.EnableKeyword("BLUR_FAST");
                    break;
                case BlurType.Medium:
                    mat.EnableKeyword("BLUR_MEDIUM");
                    break;
                case BlurType.Detail:
                    mat.EnableKeyword("BLUR_DETAIL");
                    break;
            }

            if (strokeWidth > 0 && outlineWidth > 0)
            {
                mat.EnableKeyword("OUTLINED_STROKE");
            }
            else
            {
                if (strokeWidth > 0)
                {
                    mat.EnableKeyword("STROKE");
                }
                else if (outlineWidth > 0)
                {
                    mat.EnableKeyword("OUTLINED");
                }
                else
                {
                    mat.DisableKeyword("OUTLINED_STROKE");
                    mat.DisableKeyword("STROKE");
                    mat.DisableKeyword("OUTLINED");
                }
            }

            if (DrawShape != DrawShape.None)
            {
                float pixelSize = 1 / Mathf.Max(0, FalloffDistance);
                mat.SetFloat(pixelWorldScale_Sp, Mathf.Clamp(pixelSize, 0f, 999999f));
            }

            triangle.ModifyMaterial(ref mat);
            circle.ModifyMaterial(ref mat, falloffDistance);
            rectangle.ModifyMaterial(ref mat);
            pentagon.ModifyMaterial(ref mat);
            hexagon.ModifyMaterial(ref mat);
            chamferBox.ModifyMaterial(ref mat);
            parallelogram.ModifyMaterial(ref mat);
            nStarPolygon.ModifyMaterial(ref mat);
            heart.ModifyMaterial(ref mat);
            blobbyCross.ModifyMaterial(ref mat);
            squircle.ModifyMaterial(ref mat);
            nTriangleRounded.ModifyMaterial(ref mat);

            gradientEffect.ModifyMaterial(ref mat);


            switch (DrawShape)
            {
                case DrawShape.None:
                    mat.DisableKeyword("CIRCLE");
                    mat.DisableKeyword("TRIANGLE");
                    mat.DisableKeyword("RECTANGLE");
                    mat.DisableKeyword("PENTAGON");
                    mat.DisableKeyword("HEXAGON");
                    mat.DisableKeyword("CHAMFERBOX");
                    mat.DisableKeyword("PARALLELOGRAM");
                    mat.DisableKeyword("NSTAR_POLYGON");
                    mat.DisableKeyword("HEART");
                    mat.DisableKeyword("BLOBBYCROSS");
                    mat.DisableKeyword("SQUIRCLE");
                    mat.DisableKeyword("NTRIANGLE_ROUNDED");
                    break;
                case DrawShape.Circle:
                    mat.EnableKeyword("CIRCLE");
                    break;
                case DrawShape.Triangle:
                    mat.EnableKeyword("TRIANGLE");
                    break;
                case DrawShape.Rectangle:
                    mat.EnableKeyword("RECTANGLE");
                    break;
                case DrawShape.Pentagon:
                    mat.EnableKeyword("PENTAGON");
                    break;
                case DrawShape.NStarPolygon:
                    mat.EnableKeyword("NSTAR_POLYGON");
                    break;
                case DrawShape.Hexagon:
                    mat.EnableKeyword("HEXAGON");
                    break;
                case DrawShape.ChamferBox:
                    mat.EnableKeyword("CHAMFERBOX");
                    break;
                case DrawShape.Parallelogram:
                    mat.EnableKeyword("PARALLELOGRAM");
                    break;
                case DrawShape.Heart:
                    mat.EnableKeyword("HEART");
                    break;
                case DrawShape.BlobbyCross:
                    mat.EnableKeyword("BLOBBYCROSS");
                    break;
                case DrawShape.Squircle:
                    mat.EnableKeyword("SQUIRCLE");
                    break;
                case DrawShape.NTriangleRounded:
                    mat.EnableKeyword("NTRIANGLE_ROUNDED");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            mat.SetInt(drawShape_Sp, (int)DrawShape);
            mat.SetInt(flipHorizontal_Sp, flipHorizontal ? 1 : 0);
            mat.SetInt(flipVertical_Sp, flipVertical ? 1 : 0);

            mat.SetFloat(shapeRotation_Sp, shapeRotation);
            mat.SetInt(constrainedRotation_Sp, constrainRotation ? 1 : 0);

            return mat;
        }

        /// <summary>
        /// 禁用所有材质关键字
        /// </summary>
        /// <param name="mat"></param>
        private void DisableAllMaterialKeywords(Material mat)
        {
            mat.DisableKeyword("PROCEDURAL");
            mat.DisableKeyword("HYBRID");

            mat.DisableKeyword("CIRCLE");
            mat.DisableKeyword("TRIANGLE");
            mat.DisableKeyword("RECTANGLE");
            mat.DisableKeyword("PENTAGON");
            mat.DisableKeyword("HEXAGON");
            mat.DisableKeyword("CHAMFERBOX");
            mat.DisableKeyword("PARALLELOGRAM");
            mat.DisableKeyword("NSTAR_POLYGON");
            mat.DisableKeyword("HEART");
            mat.DisableKeyword("BLOBBYCROSS");
            mat.DisableKeyword("SQUIRCLE");
            mat.DisableKeyword("NTRIANGLE_ROUNDED");

            mat.DisableKeyword("STROKE");
            mat.DisableKeyword("OUTLINED");
            mat.DisableKeyword("OUTLINED_STROKE");

            mat.DisableKeyword("ROUNDED_CORNERS");

            mat.DisableKeyword("GRADIENT_LINEAR");
            mat.DisableKeyword("GRADIENT_CORNER");
            mat.DisableKeyword("GRADIENT_RADIAL");

            mat.DisableKeyword("BLUR_FAST");
            mat.DisableKeyword("BLUR_MEDIUM");
            mat.DisableKeyword("BLUR_DETAIL");

            mat.DisableKeyword("TRANSITION_FADE");
            mat.DisableKeyword("TRANSITION_CUTOFF");
            mat.DisableKeyword("TRANSITION_DISSOLVE");
            mat.DisableKeyword("TRANSITION_SHINY");
            mat.DisableKeyword("TRANSITION_MASK");
            mat.DisableKeyword("TRANSITION_MELT");
            mat.DisableKeyword("TRANSITION_BURN");
            mat.DisableKeyword("TRANSITION_PATTERN");
            mat.DisableKeyword("TRANSITION_BLAZE");
        }

        /// <summary>
        /// 从共享材质初始化值
        /// </summary>
        public void InitValuesFromSharedMaterial()
        {
            if (m_Material == null) return;
            Material mat = m_Material;

            //Basic Settings
            drawShape = (DrawShape)mat.GetInt(drawShape_Sp);

            blurType = (BlurType)mat.GetInt(blurType_Sp);
            blurIntensity = mat.GetFloat(blurIntensity_Sp);

            transitionMode = (TransitionMode)mat.GetInt(transitionMode_Sp);
            transitionTexture = mat.GetTexture(transitionTex_Sp);
            Vector4 st = mat.GetVector(transitionTex_ST_Sp);
            transitionTexScale = new Vector2(st.x, st.y);
            transitionTexOffset = new Vector2(st.z, st.w);
            transitionTexRotation = mat.GetFloat(transitionTexRotation_Sp);
            transitionRate = mat.GetFloat(transitionRate_Sp);
            transitionColor = mat.GetColor(transitionColor_Sp);
            transitionWidth = mat.GetFloat(transitionWidth_Sp);
            transitionSoftness = mat.GetFloat(transitionSoftness_Sp);
            transitionReverse = mat.GetInt(transitionReverse_Sp) == 1;
            transitionSpeed = mat.GetVector(transitionTexSpeed_Sp);
            transitionPatternReverse = mat.GetInt(transitionPatternReverse_Sp) == 1;
            transitionAutoPlaySpeed = mat.GetFloat(transitionAutoPlaySpeed_Sp);
            transitionColorFilter = (ColorMode)mat.GetInt(transitionColorFilter_Sp);
            transitionColorGlow = mat.GetInt(transitionColorGlow_Sp) == 1;
            transitionGradient = mat.GetTexture(transitionGradientTex_Sp);
            transitionRange = mat.GetVector(transitionRange_Sp);
            transitionClamp = mat.GetFloat(transitionClamp_Sp) == 1;
            transitionTexClampPadding = mat.GetFloat(transitionTexClampPadding_Sp);
            transitionUseUv0 = mat.GetFloat(transitionUseUv0_Sp) == 1;

            strokeWidth = mat.GetFloat(strokeWidth_Sp);
            falloffDistance = mat.GetFloat(falloffDistance_Sp);

            outlineWidth = mat.GetFloat(outlineWidth_Sp);
            outlineColor = mat.GetColor(outlineColor_Sp);
            enableDashedOutline = mat.GetInt(enableDashedOutline_Sp);
            customTime = mat.GetFloat(customTime_Sp);

            flipHorizontal = mat.GetInt(flipHorizontal_Sp) == 1;
            flipVertical = mat.GetInt(flipVertical_Sp) == 1;
            constrainRotation = mat.GetInt(constrainedRotation_Sp) == 1;
            shapeRotation = mat.GetFloat(shapeRotation_Sp);

            triangle.InitValuesFromMaterial(ref mat);
            circle.InitValuesFromMaterial(ref mat);
            rectangle.InitValuesFromMaterial(ref mat);
            pentagon.InitValuesFromMaterial(ref mat);
            hexagon.InitValuesFromMaterial(ref mat);
            chamferBox.InitValuesFromMaterial(ref mat);
            parallelogram.InitValuesFromMaterial(ref mat);
            nStarPolygon.InitValuesFromMaterial(ref mat);
            heart.InitValuesFromMaterial(ref mat);
            blobbyCross.InitValuesFromMaterial(ref mat);
            squircle.InitValuesFromMaterial(ref mat);
            nTriangleRounded.InitValuesFromMaterial(ref mat);

            //GradientEffect
            gradientEffect.InitValuesFromMaterial(ref mat);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 创建材质资产
        /// </summary>
        /// <returns></returns>
        public Material CreateMaterialAssetFromComponentSettings()
        {
            Material matAsset = new Material(Shader.Find(shaderName));
            matAsset = GetModifiedMaterial(matAsset);
            string path = EditorUtility.SaveFilePanelInProject("通过ImageEx创建材质",
                "Material", "mat", "选择位置");
            AssetDatabase.CreateAsset(matAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return matAsset;
        }
#endif
    }
}