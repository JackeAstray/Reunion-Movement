#ifndef RM_TRANSITION
#define RM_TRANSITION

// ============================================================
// ReunionMovement 通用过渡模块 (RM_Transition)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明对应的 _Transition* Properties
// 依赖：Common.cginc（rgb_to_hsv, hsv_to_rgb, inv_lerp, rotateUV）
// ============================================================

#include "../Base/Common.cginc"

uniform int _TransitionMode;
uniform sampler2D _TransitionTex; uniform float4 _TransitionTex_ST;
uniform float4 _TransitionTex_TexelSize;
uniform half _TransitionTexRotation;
uniform half _TransitionRate;
uniform half4 _TransitionColor;
uniform half _TransitionWidth;
uniform half _TransitionSoftness;
uniform int _TransitionReverse;
uniform half2 _TransitionTex_Speed;
uniform int _TransitionPatternReverse;
uniform half _TransitionAutoPlaySpeed;
uniform int _TransitionColorFilter;
uniform int _TransitionColorGlow;
uniform sampler2D _TransitionGradientTex;
uniform half2 _TransitionRange;
uniform half _TransitionClamp;
uniform half _TransitionTexClampPadding;
uniform half _TransitionUseUv0;

half4 RM_ApplyColorFilter(int mode, half4 inColor, half4 factor, float intensity, float glow)
{
    half4 color = inColor;
    if (mode == 1) // Color.Multiply
    {
        color.rgb = color.rgb * factor.rgb;
        color *= factor.a;
    }
    else if (mode == 2) // Color.Additive
    {
        color.rgb = color.rgb + factor.rgb * color.a * factor.a;
    }
    else if (mode == 3) // Color.Subtractive
    {
        color.rgb = color.rgb - factor.rgb * color.a * color.a;
    }
    else if (mode == 4) // Color.Replace
    {
        color.rgb = factor.rgb * color.a;
        color *= factor.a;
    }
    else if (mode == 5) // Color.MultiplyLuminance
    {
        color.rgb = (1 + Luminance(color.rgb)) * factor.rgb * factor.a / 2 * color.a;
    }
    else if (mode == 6) // Color.MultiplyAdditive
    {
        color.rgb = color.rgb * (1 + factor.rgb * factor.a);
    }
    else if (mode == 7) // Color.HsvModifier
    {
        const float3 hsv = rgb_to_hsv(color.rgb);
        color.rgb = hsv_to_rgb(hsv + factor.rgb) * color.a * color.a;
        color.a = inColor.a * factor.a;
    }
    else if (mode == 8) // Color.Contrast
    {
        color.rgb = ((color.rgb - 0.5) * (factor.r + 1) + 0.5 + factor.g * 1.5) * color.a * factor.a;
        color.a = color.a * factor.a;
    }

    if (0 < mode)
    {
        color = lerp(inColor, color, intensity);
        color.a *= 1 - glow * intensity;
    }

    return color;
}

float RM_TransitionRate()
{
    if (abs(_TransitionAutoPlaySpeed) > 0.001)
        return frac(_TransitionAutoPlaySpeed * _Time.y + _TransitionRate);
    return _TransitionRate;
}

float RM_TransitionAlpha(float2 uvLocal)
{
    float2 uv = uvLocal;
    if (_TransitionTexRotation != 0)
        uv = rotateUV(uv, radians(_TransitionTexRotation), float2(0.5, 0.5));

    uv = uv * _TransitionTex_ST.xy + _TransitionTex_ST.zw;

#if TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        uv = saturate(uv);
#else
    #if TRANSITION_CLAMP_STATIC
        uv = saturate(uv);
    #else
    if (_TransitionClamp > 0.5)
    {
        uv = saturate(uv);
    }
    #endif
#endif

    float2 uvSample = uv + _Time.y * _TransitionTex_Speed;

#if TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        float2 pad = _TransitionTex_TexelSize.xy * max(_TransitionTexClampPadding, 0);
        float2 tileUv = frac(uvSample);
        tileUv = clamp(tileUv, pad, 1.0 - pad);
        uvSample = floor(uvSample) + tileUv;
#endif

    float alpha = tex2Dlod(_TransitionTex, float4(uvSample, 0, 0)).a;
    alpha = _TransitionReverse ? 1 - alpha : alpha;

#if TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        alpha = clamp(alpha, 1e-4, 1.0 - 1e-4);
#endif

    return alpha;
}

float2 RM_MoveTransitionFilter(float4 uvMask, float alpha)
{
#if !TRANSITION_MELT && !TRANSITION_BURN
    return 0;
#endif

    const float factor = alpha - RM_TransitionRate() * (1 + _TransitionWidth * 1.5) + _TransitionWidth;
    const float band = max(0, _TransitionWidth - factor);

#if TRANSITION_MELT
        return float2(0, +band * band * (uvMask.w - uvMask.y) / max(0.01, _TransitionWidth));
#elif TRANSITION_BURN
        return float2(0, -band * band * (uvMask.w - uvMask.y) / max(0.01, _TransitionWidth));
#endif

    return 0;
}

half4 RM_ApplyTransitionFilter(half4 color, float alpha, float2 uvLocal)
{
#if TRANSITION_FADE
        color *= saturate(alpha + 1 - RM_TransitionRate() * 2);
#elif TRANSITION_CUTOFF
        color *= step(0.001, alpha - RM_TransitionRate());
#elif TRANSITION_PATTERN
        const half4 patternColor = RM_ApplyColorFilter(_TransitionColorFilter, half4(color.rgb, 1), half4(_TransitionColor.rgb * color.a, 1), _TransitionColor.a, _TransitionColorGlow);

        float isPattern = min(inv_lerp(_TransitionRange.x, _TransitionRange.y, uvLocal.x), 0.995) < (_TransitionPatternReverse ? alpha : 1 - alpha);
        isPattern = _TransitionPatternReverse ? isPattern : 1 - isPattern;

        color.rgb = lerp(color.rgb, patternColor.rgb, isPattern);
#elif TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        const float factor = alpha - RM_TransitionRate() * (1 + _TransitionWidth) + _TransitionWidth;
        const float softness = max(0.0001, _TransitionWidth * _TransitionSoftness);
        const half bandLerp = saturate((_TransitionWidth - factor) * 2 / softness);
        const half softLerp = saturate(factor * 2 / softness);

        half4 bandColor = RM_ApplyColorFilter(_TransitionColorFilter, half4(color.rgb, 1),
                                 half4(_TransitionColor.rgb, 1), _TransitionColor.a, _TransitionColorGlow);
        bandColor *= color.a;

#if TRANSITION_MELT
            color = lerp(color, bandColor, bandLerp);
            return color;
#elif TRANSITION_BURN
            color = lerp(color, bandColor, bandLerp * 1.25);
            color.a *= 1 - inv_lerp(0.85, 1.0, bandLerp * 1.25);
            color.rgb *= (1 - inv_lerp(0.85, 1.0, bandLerp * 1.3)) * color.a;
            return color;
#endif

        half lerpFactor = bandLerp * softLerp;
        color = lerp(color, bandColor, lerpFactor);

#if TRANSITION_DISSOLVE
            color *= softLerp;
#elif TRANSITION_MASK
            color *= bandLerp * softLerp;
#endif
#elif TRANSITION_BLAZE
        const float maxValue = RM_TransitionRate();
        const float minValue = maxValue - _TransitionWidth / 2;
        const float rate = 1 - inv_lerp(minValue, maxValue, alpha * (1 - _TransitionWidth / 2));
        const float4 gradColor = tex2D(_TransitionGradientTex, float2(rate, 0.5));
        const float4 burntColor = gradColor * color;
        const float4 flameColor = float4(gradColor.rgb, gradColor.a * color.a);

        color = lerp(burntColor, flameColor, step(0.5, rate));
        color.rgb *= color.a;
#endif

    return color;
}

#endif // RM_TRANSITION
