// ImageEx Transition Module
// 从 ImageEx.shader 提取的过渡相关函数，实现保持一致

half4 apply_color_filter(int mode, half4 inColor, half4 factor, float intensity, float glow)
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

float transition_rate()
{
    if (abs(_TransitionAutoPlaySpeed) > 0.001)
        return frac(_TransitionAutoPlaySpeed * _Time.y + _TransitionRate);
    return _TransitionRate;
}

float transition_alpha(float2 uvLocal)
{
    float2 uv = uvLocal;
    if (_TransitionTexRotation != 0)
        uv = rotateUV(uv, radians(_TransitionTexRotation), float2(0.5, 0.5));

    uv = uv * _TransitionTex_ST.xy + _TransitionTex_ST.zw;

    // 说明：
    // - Shiny/Mask/Melt/Burn 模式对纹理环绕带来的接缝很敏感。
    // - 当使用 _TransitionTexRotation 时，UV 可能超出 [0,1] 区间；如果纹理为 Repeat 且使用双线性滤波，会出现随旋转移动的接缝。
    // 对这些过渡模式，我们会无条件将 UV 限制在有效范围内以避免接缝。
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

    // 当 WrapMode=Repeat 且 FilterMode=Bilinear 时，在图块边缘采样会与对边混合，导致随旋转移动的接缝。
    // 为避免直接对齐采样像素（会破坏平滑运动），对每个重复图块内部使用少量像素的内边距进行 clamp。
#if TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        float2 pad = _TransitionTex_TexelSize.xy * max(_TransitionTexClampPadding, 0);
        float2 tileUv = frac(uvSample);
        tileUv = clamp(tileUv, pad, 1.0 - pad);
        uvSample = floor(uvSample) + tileUv;
#endif

    // 强制使用 LOD 0，避免 mip 级别混合导致在锐利过渡图案上出现接缝。
    float alpha = tex2Dlod(_TransitionTex, float4(uvSample, 0, 0)).a;
    alpha = _TransitionReverse ? 1 - alpha : alpha;

    // 避免出现精确的 0/1 值（某些过渡模式会放大边缘精度问题），对 alpha 做微小夹取
#if TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        alpha = clamp(alpha, 1e-4, 1.0 - 1e-4);
#endif

    return alpha;
}

float2 move_transition_filter(float4 uvMask, float alpha)
{
#if !TRANSITION_MELT && !TRANSITION_BURN
    return 0;
#endif

    const float factor = alpha - transition_rate() * (1 + _TransitionWidth * 1.5) + _TransitionWidth;
    const float band = max(0, _TransitionWidth - factor);

#if TRANSITION_MELT
        return float2(0, +band * band * (uvMask.w - uvMask.y) / max(0.01, _TransitionWidth));
#elif TRANSITION_BURN
        return float2(0, -band * band * (uvMask.w - uvMask.y) / max(0.01, _TransitionWidth));
#endif

    return 0;
}

half4 apply_transition_filter(half4 color, float alpha, float2 uvLocal)
{
#if TRANSITION_FADE
        color *= saturate(alpha + 1 - transition_rate() * 2);
#elif TRANSITION_CUTOFF
        color *= step(0.001, alpha - transition_rate());
#elif TRANSITION_PATTERN
        const half4 patternColor = apply_color_filter(_TransitionColorFilter, half4(color.rgb, 1), half4(_TransitionColor.rgb * color.a, 1), _TransitionColor.a, _TransitionColorGlow);

        float isPattern = min(inv_lerp(_TransitionRange.x, _TransitionRange.y, uvLocal.x), 0.995) < (_TransitionPatternReverse ? alpha : 1 - alpha);
        isPattern = _TransitionPatternReverse ? isPattern : 1 - isPattern;

        color.rgb = lerp(color.rgb, patternColor.rgb, isPattern);
#elif TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN
        const float factor = alpha - transition_rate() * (1 + _TransitionWidth) + _TransitionWidth;
        const float softness = max(0.0001, _TransitionWidth * _TransitionSoftness);
        const half bandLerp = saturate((_TransitionWidth - factor) * 2 / softness);
        const half softLerp = saturate(factor * 2 / softness);

        half4 bandColor = apply_color_filter(_TransitionColorFilter, half4(color.rgb, 1),
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
        const float maxValue = transition_rate();
        const float minValue = maxValue - _TransitionWidth / 2;
        const float rate = 1 - inv_lerp(minValue, maxValue, alpha * (1 - _TransitionWidth / 2));
        const float4 gradColor = tex2D(_TransitionGradientTex, float2(rate, 0.5)); // 将过渡纹理用作渐变
        const float4 burntColor = gradColor * color;
        const float4 flameColor = float4(gradColor.rgb, gradColor.a * color.a);

        color = lerp(burntColor, flameColor, step(0.5, rate));
        color.rgb *= color.a;
#endif

    return color;
}