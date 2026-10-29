using System;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Astray.Utilities.UI.ImageExtensions;
using Astray.Common;

namespace Astray.EditorTools.ImageExtensions
{
    [CustomEditor(typeof(ImageEx), true)]
    [CanEditMultipleObjects]
    public partial class ImageExEditor : ImageEditor
    {
        private SerializedProperty spSprite;
        private SerializedProperty spCameraTexture;
        private SerializedProperty spAppendShadow;
        private SerializedProperty spShadowOffsetLocal;
        private SerializedProperty spCircle, spTriangle, spRectangle, spPentagon, spHexagon, spChamferBox, spQuadrilateral, spNStarPolygon, spHeart, spBlobbyCross, spSquircle, spNTriangleRounded;
        private SerializedProperty spPreserveAspect;
        private SerializedProperty spFillMethod, spFillOrigin, spFillAmount, spFillClockwise;
        private SerializedProperty spAlphaThreshold;
        private SerializedProperty spConstrainRotation, spShapeRotation, spFlipHorizontal, spFlipVertical;
        private SerializedProperty spShape;
        private SerializedProperty spStrokeWidth, spOutlineWidth, spOutlineColor, spFalloffDistance, spEnableDashedOutline, spCustomTime;
        private SerializedProperty spShadowMirrorDirection;
        private SerializedProperty spShadowScale;
        private SerializedProperty spShadowMirrorScale, spShadowMirrorOffset;
        private SerializedProperty spShadowMirrorShowSource;
        private SerializedProperty spShadowMirrorTintMix;

        // 非法 DrawShape 值限频告警标记（Inspector 绘制路径不抛异常）
        private bool m_invalidShapeLogged;
        private SerializedProperty spShadowColorFilter, spShadowColorGlow;

        private SerializedProperty spMaterialSettings, spMaterial, spImageType;

        private SerializedProperty spGradient;
        private SerializedProperty spBlurType, spBlurIntensity;
        private SerializedProperty spShadowMode;
        private SerializedProperty spTransitionMode, spTransitionTex, spTransitionRate, spTransitionColor, spTransitionWidth, spTransitionSoftness, spTransitionReverse;
        private SerializedProperty spTransitionTexScale, spTransitionTexOffset, spTransitionTexRotation, spTransitionKeepAspectRatio;
        private SerializedProperty spTransitionSpeed, spTransitionPatternReverse, spTransitionAutoPlaySpeed, spTransitionColorFilter, spTransitionColorGlow, spTransitionGradientValue, spTransitionRange;
        // 过渡高级参数（此前遗漏的序列化字段，补充 Inspector 入口）
        private SerializedProperty spTransitionClamp, spTransitionTexClampPadding, spTransitionUseUv0;

        // Phase 1: 色调滤镜
        private SerializedProperty spToneFilter, spToneIntensity;
        // Phase 1: 独立颜色滤镜
        private SerializedProperty spColorFilterMode, spColorValue, spColorIntensity, spColorGlow;
        // Phase 1: 边缘效果
        private SerializedProperty spEdgeMode, spEdgeWidth, spEdgeColorFilterMode, spEdgeColor, spEdgeColorGlow;
        private SerializedProperty spEdgeShinyRate, spEdgeShinyWidth, spEdgeShinyAutoPlaySpeed;

        // Phase 2: 采样增强
        private SerializedProperty spSamplingMode, spSamplingIntensity;
        // Phase 2: 目标模式
        private SerializedProperty spTargetMode, spTargetColor, spTargetRange, spTargetSoftness;
        // Phase 2: 图案区域
        private SerializedProperty spPatternArea;

        // Phase 3: 细节纹理
        private SerializedProperty spDetailMode, spDetailTex, spDetailTexScale, spDetailTexOffset, spDetailTexSpeed;
        private SerializedProperty spDetailIntensity, spDetailThreshold, spDetailColor;
        // Phase 3: 渐变纹理
        private SerializedProperty spEnableGradientTex, spGradientTex, spGradientOffset, spGradientScale;
        // Phase 3: 混合模式
        private SerializedProperty spBlendType;

        private bool shaderChannelsNeedUpdate;

        protected override void OnEnable()
        {
            foreach (Object obj in serializedObject.targetObjects)
            {
                ((ImageEx)obj).UpdateSerializedValuesFromSharedMaterial();
            }

            base.OnEnable();

            // 检查父 Canvas 是否缺少 ImageEx 着色器所需的附加通道（TexCoord1/TexCoord2/Tangent），
            // 缺失时 FixShaderChannelGUI 显示修复提示（运行时 OnValidate 会自动补，这里作可见安全网）
            DetectMissingShaderChannels();

            spAppendShadow = serializedObject.FindProperty("appendShadow");
            spShadowOffsetLocal = serializedObject.FindProperty("shadowOffsetLocal");

            spSprite = serializedObject.FindProperty("m_Sprite");
            spCameraTexture = serializedObject.FindProperty("cameraTexture");

            spShape = serializedObject.FindProperty("drawShape");

            spStrokeWidth = serializedObject.FindProperty("strokeWidth");
            spOutlineWidth = serializedObject.FindProperty("outlineWidth");
            spOutlineColor = serializedObject.FindProperty("outlineColor");
            spFalloffDistance = serializedObject.FindProperty("falloffDistance");
            spEnableDashedOutline = serializedObject.FindProperty("enableDashedOutline");
            spCustomTime = serializedObject.FindProperty("customTime");
            spShadowMirrorDirection = serializedObject.FindProperty("shadowMirrorDirection");
            spShadowScale = serializedObject.FindProperty("shadowScale");
            spShadowMirrorScale = serializedObject.FindProperty("shadowMirrorScale");
            spShadowMirrorOffset = serializedObject.FindProperty("shadowMirrorOffset");
            spShadowMirrorShowSource = serializedObject.FindProperty("shadowMirrorShowSource");
            spShadowMirrorTintMix = serializedObject.FindProperty("shadowMirrorTintMix");
            spShadowColorFilter = serializedObject.FindProperty("shadowColorFilter");
            spShadowColorGlow = serializedObject.FindProperty("shadowColorGlow");

            spMaterialSettings = serializedObject.FindProperty("materialMode");
            spMaterial = serializedObject.FindProperty("m_Material");
            spImageType = serializedObject.FindProperty("imageType");

            spFillMethod = serializedObject.FindProperty("m_FillMethod");
            spFillOrigin = serializedObject.FindProperty("m_FillOrigin");
            spFillAmount = serializedObject.FindProperty("m_FillAmount");
            spFillClockwise = serializedObject.FindProperty("m_FillClockwise");

            spConstrainRotation = serializedObject.FindProperty("constrainRotation");
            spShapeRotation = serializedObject.FindProperty("shapeRotation");
            spFlipHorizontal = serializedObject.FindProperty("flipHorizontal");
            spFlipVertical = serializedObject.FindProperty("flipVertical");

            spAlphaThreshold = serializedObject.FindProperty("alphaThreshold");

            spCircle = serializedObject.FindProperty("circle");
            spRectangle = serializedObject.FindProperty("rectangle");
            spTriangle = serializedObject.FindProperty("triangle");
            spPentagon = serializedObject.FindProperty("pentagon");
            spHexagon = serializedObject.FindProperty("hexagon");
            spChamferBox = serializedObject.FindProperty("chamferBox");
            spQuadrilateral = serializedObject.FindProperty("quadrilateral");
            spNStarPolygon = serializedObject.FindProperty("nStarPolygon");
            spHeart = serializedObject.FindProperty("heart");
            spBlobbyCross = serializedObject.FindProperty("blobbyCross");
            spSquircle = serializedObject.FindProperty("squircle");
            spNTriangleRounded = serializedObject.FindProperty("nTriangleRounded");

            spPreserveAspect = serializedObject.FindProperty("m_PreserveAspect");

            spGradient = serializedObject.FindProperty("gradientEffect");
            spBlurType = serializedObject.FindProperty("blurType");
            spBlurIntensity = serializedObject.FindProperty("blurIntensity");
            spShadowMode = serializedObject.FindProperty("shadowMode");

            spTransitionMode = serializedObject.FindProperty("transitionMode");
            spTransitionTex = serializedObject.FindProperty("transitionTexture");
            spTransitionTexScale = serializedObject.FindProperty("transitionTexScale");
            spTransitionTexOffset = serializedObject.FindProperty("transitionTexOffset");
            spTransitionTexRotation = serializedObject.FindProperty("transitionTexRotation");
            spTransitionKeepAspectRatio = serializedObject.FindProperty("transitionKeepAspectRatio");
            spTransitionRate = serializedObject.FindProperty("transitionRate");
            spTransitionColor = serializedObject.FindProperty("transitionColor");
            spTransitionWidth = serializedObject.FindProperty("transitionWidth");
            spTransitionSoftness = serializedObject.FindProperty("transitionSoftness");
            spTransitionReverse = serializedObject.FindProperty("transitionReverse");
            spTransitionSpeed = serializedObject.FindProperty("transitionSpeed");
            spTransitionPatternReverse = serializedObject.FindProperty("transitionPatternReverse");
            spTransitionAutoPlaySpeed = serializedObject.FindProperty("transitionAutoPlaySpeed");
            spTransitionColorFilter = serializedObject.FindProperty("transitionColorFilter");
            spTransitionColorGlow = serializedObject.FindProperty("transitionColorGlow");
            // 注意：transitionGradient 为运行时从 transitionGradientValue 生成，不在 Inspector 直接编辑
            spTransitionGradientValue = serializedObject.FindProperty("transitionGradientValue");
            spTransitionRange = serializedObject.FindProperty("transitionRange");

            // 过渡高级参数（手动绘制编辑器必须显式声明+FindProperty，否则 Inspector 不显示）
            spTransitionClamp = serializedObject.FindProperty("transitionClamp");
            spTransitionTexClampPadding = serializedObject.FindProperty("transitionTexClampPadding");
            spTransitionUseUv0 = serializedObject.FindProperty("transitionUseUv0");

            // Phase 1: 色调滤镜
            spToneFilter = serializedObject.FindProperty("m_ToneFilter");
            spToneIntensity = serializedObject.FindProperty("m_ToneIntensity");
            // Phase 1: 独立颜色滤镜
            spColorFilterMode = serializedObject.FindProperty("m_ColorFilterMode");
            spColorValue = serializedObject.FindProperty("m_ColorValue");
            spColorIntensity = serializedObject.FindProperty("m_ColorIntensity");
            spColorGlow = serializedObject.FindProperty("m_ColorGlow");
            // Phase 1: 边缘效果
            spEdgeMode = serializedObject.FindProperty("m_EdgeMode");
            spEdgeWidth = serializedObject.FindProperty("m_EdgeWidth");
            spEdgeColorFilterMode = serializedObject.FindProperty("m_EdgeColorFilterMode");
            spEdgeColor = serializedObject.FindProperty("m_EdgeColor");
            spEdgeColorGlow = serializedObject.FindProperty("m_EdgeColorGlow");
            spEdgeShinyRate = serializedObject.FindProperty("m_EdgeShinyRate");
            spEdgeShinyWidth = serializedObject.FindProperty("m_EdgeShinyWidth");
            spEdgeShinyAutoPlaySpeed = serializedObject.FindProperty("m_EdgeShinyAutoPlaySpeed");

            // Phase 2: 采样增强
            spSamplingMode = serializedObject.FindProperty("m_SamplingMode");
            spSamplingIntensity = serializedObject.FindProperty("m_SamplingIntensity");
            // Phase 2: 目标模式
            spTargetMode = serializedObject.FindProperty("m_TargetMode");
            spTargetColor = serializedObject.FindProperty("m_TargetColor");
            spTargetRange = serializedObject.FindProperty("m_TargetRange");
            spTargetSoftness = serializedObject.FindProperty("m_TargetSoftness");
            // Phase 2: 图案区域
            spPatternArea = serializedObject.FindProperty("m_PatternArea");

            // Phase 3: 细节纹理
            spDetailMode = serializedObject.FindProperty("m_DetailMode");
            spDetailTex = serializedObject.FindProperty("m_DetailTex");
            spDetailTexScale = serializedObject.FindProperty("m_DetailTexScale");
            spDetailTexOffset = serializedObject.FindProperty("m_DetailTexOffset");
            spDetailTexSpeed = serializedObject.FindProperty("m_DetailTexSpeed");
            spDetailIntensity = serializedObject.FindProperty("m_DetailIntensity");
            spDetailThreshold = serializedObject.FindProperty("m_DetailThreshold");
            spDetailColor = serializedObject.FindProperty("m_DetailColor");
            // Phase 3: 渐变纹理
            spEnableGradientTex = serializedObject.FindProperty("m_EnableGradientTex");
            spGradientTex = serializedObject.FindProperty("m_GradientTex");
            spGradientOffset = serializedObject.FindProperty("m_GradientOffset");
            spGradientScale = serializedObject.FindProperty("m_GradientScale");
            // Phase 3: 混合模式
            spBlendType = serializedObject.FindProperty("m_BlendType");
        }
    }
}
