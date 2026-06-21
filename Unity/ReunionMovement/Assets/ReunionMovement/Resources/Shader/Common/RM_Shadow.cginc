#ifndef RM_SHADOW
#define RM_SHADOW

// ============================================================
// ReunionMovement 通用阴影/镜像模块 (RM_Shadow)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明：
//   sampler2D _MainTex; float4 _MainTex_TexelSize; float4 _TextureSampleAdd;
//   _ShadowColor, _ShadowBlurIntensity, _SamplingWidth, _SamplingScale,
//   _AllowOutOfBoundsShadow, _ShadowScale, _ShadowMode, etc.
// 依赖：RM_SDFShapes.cginc（形状函数）, RM_Transition.cginc（过渡过滤器）
// ============================================================

#include "RM_SDFShapes.cginc"
#include "RM_Transition.cginc"

// 注：_MainTex, _MainTex_TexelSize, _TextureSampleAdd 由包含此文件的 Shader 声明
uniform half4 _Color;

uniform half4 _ShadowColor;
uniform float _ShadowBlurIntensity;
uniform float _SamplingWidth;
uniform float _SamplingScale;
uniform float _AllowOutOfBoundsShadow;
uniform float _ShadowScale;
uniform float _ShadowMode;
uniform int _ShadowMirrorDirection;
uniform float _ShadowMirrorScale;
uniform float2 _ShadowMirrorOffset;
uniform float _ShadowMirrorShowSource;
uniform float _ShadowMirrorTintMix;

// 渲染阴影/镜像（统一入口）
// shapeData: x=shapeUv.x, y=shapeUv.y, z=size.x, w=size.y
// falloffDistance: _FalloffDistance 用于 SDF 衰减
// strokeWidth: _StrokeWidth
// outlineWidth: _OutlineWidth
half4 RM_RenderShadow(
    float4 shapeData,
    float falloffDistance,
    float strokeWidth,
    float outlineWidth,
    half2 texcoord,
    float transAlpha,
    float2 transitionFilterUv,
    float vertexAlpha
)
{
    // 阴影模式为 0 时直接返回全透明，作为安全兜底
    if (_ShadowMode < 0.5) return half4(0, 0, 0, 0);

    half4 shadowOut = half4(0, 0, 0, 0); // 默认返回值，消除 "potentially uninitialized" 警告

    float2 texel = _MainTex_TexelSize.xy * _SamplingScale * _SamplingWidth;

    bool allowOOB = _AllowOutOfBoundsShadow > 0.5;

    float2 sampleUv = texcoord;
    if (!allowOOB) sampleUv = saturate(sampleUv);
    fixed4 baseSample = tex2D(_MainTex, sampleUv) + _TextureSampleAdd;

    // 镜像模式
    if (_ShadowMode == 3)
    {
        float2 mirrorUv = sampleUv;
        if (_ShadowMirrorDirection == 1)
        {
            mirrorUv.x = 1.0 - (mirrorUv.x - 0.5) * _ShadowMirrorScale - 0.5 + _ShadowMirrorOffset.x;
        }
        else
        {
            mirrorUv.y = 1.0 - (mirrorUv.y - 0.5) * _ShadowMirrorScale - 0.5 + _ShadowMirrorOffset.y;
        }
        if (!_AllowOutOfBoundsShadow) mirrorUv = saturate(mirrorUv);
        fixed4 mirrorSample = tex2D(_MainTex, mirrorUv) + _TextureSampleAdd;
        float mirrorA = mirrorSample.a;
        float mirrorShadowAlpha = mirrorA * _ShadowColor.a * vertexAlpha;
        half4 mirrorShadowOut = half4(_ShadowColor.rgb * mirrorShadowAlpha, mirrorShadowAlpha);

        if (_ShadowMirrorShowSource > 0.5)
        {
            half4 tint = lerp(_ShadowColor, _Color, clamp(_ShadowMirrorTintMix, 0.0, 1.0));
            half4 src = mirrorSample * tint;
            #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
                src = RM_ApplyTransitionFilter(src, transAlpha, transitionFilterUv);
            #endif
            return src;
        }

        return mirrorShadowOut;
    }

    float shadowMask = baseSample.a;

    // SDF 形状遮罩
    #if RECTANGLE || CIRCLE || PENTAGON || TRIANGLE || HEXAGON || CHAMFERBOX || PARALLELOGRAM || NSTAR_POLYGON || HEART || BLOBBYCROSS || SQUIRCLE || NTRIANGLE_ROUNDED
        float sdfDataShadow = 0;
        float shadowFalloffDistance = max(_ShadowBlurIntensity, 0.0001);
        float pixelScaleShadow = clamp(1.0 / shadowFalloffDistance, 1.0 / 2048.0, 2048.0);

        RM_ComputeSdfData(shapeData, falloffDistance, sdfDataShadow, pixelScaleShadow);

        shadowMask *= RM_ComputeSdfMask(sdfDataShadow, pixelScaleShadow, strokeWidth, outlineWidth);
    #endif

    float shadowAlpha = shadowMask * _ShadowColor.a * vertexAlpha;
    shadowOut = half4(_ShadowColor.rgb * shadowAlpha, shadowAlpha);

    #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
        shadowOut = RM_ApplyTransitionFilter(shadowOut, transAlpha, transitionFilterUv);
    #endif

    return shadowOut;
}

#endif // RM_SHADOW
