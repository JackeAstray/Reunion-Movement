// ImageEx Gradient Module
// 从 ImageEx.shader 提取的渐变相关逻辑，实现保持一致

#if GRADIENT_LINEAR || GRADIENT_RADIAL
float4 SampleGradient(float Time)
{
    float3 color = colors[0].rgb;
    [unroll]
    for (int c = 1; c < 8; c ++)
    {
        float colorPos = saturate((Time - colors[c - 1].w) / (colors[c].w - colors[c - 1].w)) * step(c, _GradientColorLength - 1);
        color = lerp(color, colors[c].rgb, lerp(colorPos, step(0.01, colorPos), _GradientInterpolationType));
    }

    float alpha = alphas[0].x;
    [unroll]
    for (int a = 1; a < 8; a ++)
    {
        float alphaPos = saturate((Time - alphas[a - 1].y) / (alphas[a].y - alphas[a - 1].y)) * step(a, _GradientAlphaLength - 1);
        alpha = lerp(alpha, alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), _GradientInterpolationType));
    }
    return float4(color, alpha);
}
#endif

void ApplyGradientColor(inout half4 color, float2 effectsUv)
{
    #if GRADIENT_LINEAR || GRADIENT_RADIAL
        colors[0] = _GradientColor0;
        colors[1] = _GradientColor1;
        colors[2] = _GradientColor2;
        colors[3] = _GradientColor3;
        colors[4] = _GradientColor4;
        colors[5] = _GradientColor5;
        colors[6] = _GradientColor6;
        colors[7] = _GradientColor7;

        alphas[0] = _GradientAlpha0;
        alphas[1] = _GradientAlpha1;
        alphas[2] = _GradientAlpha2;
        alphas[3] = _GradientAlpha3;
        alphas[4] = _GradientAlpha4;
        alphas[5] = _GradientAlpha5;
        alphas[6] = _GradientAlpha6;
        alphas[7] = _GradientAlpha7;
    #endif

    #if GRADIENT_LINEAR
        half gradientRotation = radians(_GradientRotation);
        half t = cos(gradientRotation) * (effectsUv.x -0.5) + 
                 sin(gradientRotation) * (effectsUv.y -0.5) +0.5;
        half4 grad = SampleGradient(t);
        color *= grad;
    #endif

    #if GRADIENT_RADIAL
        half fac = saturate(length(effectsUv - float2(.5, .5)) *2);
        half4 grad = SampleGradient(clamp(fac,0,1));
        color *= grad;
    #endif

    #if GRADIENT_CORNER
        half4 topCol = lerp(_CornerGradientColor2, _CornerGradientColor3, effectsUv.x);
        half4 bottomCol = lerp(_CornerGradientColor0, _CornerGradientColor1, effectsUv.x);
        half4 finalCol = lerp(topCol, bottomCol, effectsUv.y);

        color *= finalCol;
    #endif
}
