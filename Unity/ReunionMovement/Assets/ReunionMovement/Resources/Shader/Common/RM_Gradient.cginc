#ifndef RM_GRADIENT
#define RM_GRADIENT

// ============================================================
// ReunionMovement 通用渐变模块 (RM_Gradient)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明对应的 _Gradient* Properties
// ============================================================

#if GRADIENT_LINEAR || GRADIENT_RADIAL
    uniform half4 _GradientColor0;
    uniform half4 _GradientColor1;
    uniform half4 _GradientColor2;
    uniform half4 _GradientColor3;
    uniform half4 _GradientColor4;
    uniform half4 _GradientColor5;
    uniform half4 _GradientColor6;
    uniform half4 _GradientColor7;
    
    uniform half4 _GradientAlpha0;
    uniform half4 _GradientAlpha1;
    uniform half4 _GradientAlpha2;
    uniform half4 _GradientAlpha3;
    uniform half4 _GradientAlpha4;
    uniform half4 _GradientAlpha5;
    uniform half4 _GradientAlpha6;
    uniform half4 _GradientAlpha7;

    uniform half _GradientInterpolationType;
    uniform half _GradientColorLength;
    uniform half _GradientAlphaLength;
    uniform half _GradientRotation;

    static half4 _rmGradColors[8];
    static half4 _rmGradAlphas[8];

    float4 SampleGradient(float Time)
    {
        float3 color = _rmGradColors[0].rgb;
        [unroll]
        for (int c = 1; c < 8; c ++)
        {
            float colorPos = saturate((Time - _rmGradColors[c - 1].w) / (_rmGradColors[c].w - _rmGradColors[c - 1].w)) * step(c, _GradientColorLength - 1);
            color = lerp(color, _rmGradColors[c].rgb, lerp(colorPos, step(0.01, colorPos), _GradientInterpolationType));
        }

        float alpha = _rmGradAlphas[0].x;
        [unroll]
        for (int a = 1; a < 8; a ++)
        {
            float alphaPos = saturate((Time - _rmGradAlphas[a - 1].y) / (_rmGradAlphas[a].y - _rmGradAlphas[a - 1].y)) * step(a, _GradientAlphaLength - 1);
            alpha = lerp(alpha, _rmGradAlphas[a].x, lerp(alphaPos, step(0.01, alphaPos), _GradientInterpolationType));
        }
        return float4(color, alpha);
    }
#endif

#if GRADIENT_CORNER
    uniform half4 _CornerGradientColor0;
    uniform half4 _CornerGradientColor1;
    uniform half4 _CornerGradientColor2;
    uniform half4 _CornerGradientColor3;
#endif

void RM_ApplyGradientColor(inout half4 color, float2 effectsUv)
{
    #if GRADIENT_LINEAR || GRADIENT_RADIAL
        _rmGradColors[0] = _GradientColor0;
        _rmGradColors[1] = _GradientColor1;
        _rmGradColors[2] = _GradientColor2;
        _rmGradColors[3] = _GradientColor3;
        _rmGradColors[4] = _GradientColor4;
        _rmGradColors[5] = _GradientColor5;
        _rmGradColors[6] = _GradientColor6;
        _rmGradColors[7] = _GradientColor7;

        _rmGradAlphas[0] = _GradientAlpha0;
        _rmGradAlphas[1] = _GradientAlpha1;
        _rmGradAlphas[2] = _GradientAlpha2;
        _rmGradAlphas[3] = _GradientAlpha3;
        _rmGradAlphas[4] = _GradientAlpha4;
        _rmGradAlphas[5] = _GradientAlpha5;
        _rmGradAlphas[6] = _GradientAlpha6;
        _rmGradAlphas[7] = _GradientAlpha7;
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

#endif // RM_GRADIENT