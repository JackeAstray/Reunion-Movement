#ifndef RM_GRADIENT
#define RM_GRADIENT

// ============================================================
// ReunionMovement 通用渐变模块 (RM_Gradient)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明对应的 _Gradient* Properties
//
// 【为什么这里不用 shader 关键字】
// 渐变类型/渐变纹理开关由 C# 在运行时用 Material.EnableKeyword 打开，而
// #pragma shader_feature 的变体只有在「构建期存在启用该关键字的材质」时才会
// 被编进包，否则会被裁剪；Player 里 EnableKeyword 静默失效
// （strictShaderVariantMatching=0 时连警告都没有），表现为「编辑器正常、真机没效果」。
// 实测 Android 包内 ImageEx 的变体空间只有 208 = 2*2*13(形状)*4(描边)，
// 说明 GRADIENT_* 等关键字变体全部被裁掉了。
//
// 因此本模块改为读取材质 uniform 的运行时分支：
//   1) 分支条件是 uniform，整个 draw call 一致，移动端开销可忽略；
//   2) 不产生任何额外 shader 变体（反而更小）；
//   3) Android / WebGL / 编辑器 都无条件生效。
//
// _GradientType 取值对齐 C# 的 GradientType 枚举：0=Linear 1=Corner 2=Radial
// ============================================================

uniform half _EnableGradient;        // 渐变总开关 0/1
uniform half _GradientType;          // 0=Linear 1=Corner 2=Radial
uniform half _EnableGradientTex;     // 用渐变纹理代替色带 0/1

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

// Phase 3: 渐变偏移/缩放/纹理
uniform half _GradientOffset;
uniform half _GradientScale;

uniform sampler2D _GradientTex;
uniform float4 _GradientTex_ST;

uniform half4 _CornerGradientColor0;
uniform half4 _CornerGradientColor1;
uniform half4 _CornerGradientColor2;
uniform half4 _CornerGradientColor3;

static half4 _rmGradColors[8];
static half4 _rmGradAlphas[8];

// 色带采样：按 _GradientColorLength / _GradientAlphaLength 在 8 个槽位之间插值
float4 SampleGradient(float Time)
{
    float3 color = _rmGradColors[0].rgb;
    [unroll]
    for (int c = 1; c < 8; c ++)
    {
        // 未使用的槽位色标都是 0，直接相除会得到 0/0 = NaN。
        // D3D 的 saturate(_sat) 会把 NaN 压成 0 从而掩盖问题，而移动端 GLES/Vulkan 的
        // clamp 会传播 NaN（整块颜色变 NaN），所以这里给分母加下限兜底。
        float span = max(_rmGradColors[c].w - _rmGradColors[c - 1].w, 1e-5);
        float colorPos = saturate((Time - _rmGradColors[c - 1].w) / span) * step(c, _GradientColorLength - 1);
        color = lerp(color, _rmGradColors[c].rgb, lerp(colorPos, step(0.01, colorPos), _GradientInterpolationType));
    }

    float alpha = _rmGradAlphas[0].x;
    [unroll]
    for (int a = 1; a < 8; a ++)
    {
        float span = max(_rmGradAlphas[a].y - _rmGradAlphas[a - 1].y, 1e-5);
        float alphaPos = saturate((Time - _rmGradAlphas[a - 1].y) / span) * step(a, _GradientAlphaLength - 1);
        alpha = lerp(alpha, _rmGradAlphas[a].x, lerp(alphaPos, step(0.01, alphaPos), _GradientInterpolationType));
    }
    return float4(color, alpha);
}

void RM_ApplyGradientColor(inout half4 color, float2 effectsUv, float aspect)
{
    if (_EnableGradient < 0.5)
    {
        return;
    }

    // Linear / Radial 需要色带数据；Corner 只用四个角的颜色
    if (_GradientType == 0 || _GradientType == 2)
    {
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
    }

    if (_GradientType == 0)
    {
        // -------------------- 线性（Linear） --------------------
        const float gradientRotation = radians(_GradientRotation);
        // 屏幕空间等角投影：方向按宽高比校正，再归一化使渐变始终铺满矩形(0°/90° 结果不变)
        const float2 gradientDir = float2(cos(gradientRotation) * aspect, sin(gradientRotation));
        const float gradientRange = max(abs(gradientDir.x) + abs(gradientDir.y), 1e-4);
        float t = (gradientDir.x * (effectsUv.x - 0.5) + gradientDir.y * (effectsUv.y - 0.5)) / gradientRange + 0.5;
        // Phase 3: 应用偏移和缩放
        t = (t - 0.5) * _GradientScale + 0.5 + _GradientOffset;
        t = saturate(t);

        half4 grad = half4(1, 1, 1, 1);
        if (_EnableGradientTex > 0.5)
        {
            grad = tex2D(_GradientTex, float2(t, 0.5));
        }
        else
        {
            grad = SampleGradient(t);
        }
        color *= grad;
    }
    else if (_GradientType == 2)
    {
        // -------------------- 径向（Radial） --------------------
        half fac = saturate(length(effectsUv - float2(.5, .5)) * 2 * _GradientScale + _GradientOffset);
        fac = saturate(fac);

        half4 grad = half4(1, 1, 1, 1);
        if (_EnableGradientTex > 0.5)
        {
            grad = tex2D(_GradientTex, float2(fac, 0.5));
        }
        else
        {
            grad = SampleGradient(fac);
        }
        color *= grad;
    }
    else if (_GradientType == 1)
    {
        // -------------------- 四角（Corner） --------------------
        half4 topCol = lerp(_CornerGradientColor2, _CornerGradientColor3, effectsUv.x);
        half4 bottomCol = lerp(_CornerGradientColor0, _CornerGradientColor1, effectsUv.x);
        half4 finalCol = lerp(topCol, bottomCol, effectsUv.y);

        color *= finalCol;
    }
    // 其它非法 _GradientType：不叠加任何渐变（与旧版「无关键字 ⇒ 无渐变」语义一致）
}

#endif // RM_GRADIENT
