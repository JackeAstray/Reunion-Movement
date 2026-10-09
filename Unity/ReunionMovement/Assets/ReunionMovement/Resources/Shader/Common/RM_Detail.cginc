#ifndef RM_DETAIL
#define RM_DETAIL

// ============================================================
// ReunionMovement 细节纹理滤镜模块 (RM_Detail)
// 适用场景：UI / 2D Sprite / 3D
// 
// 将第二张纹理以多种混合模式叠加到主图上（uniform _DetailMode）：
//   1 - 遮罩（基于阈值）
//   2 - 乘法
//   3 - 加法
//   4 - 替换
//   5 - 乘法+加法
//   6 - 减法
// 
// 需要在包含此文件的 Shader 中声明：
//   sampler2D _DetailTex; float4 _DetailTex_ST;
//   half2 _DetailTex_Speed; half _DetailIntensity;
//   half2 _DetailThreshold; half4 _DetailColor;
// ============================================================

#include "../Base/Common.cginc"

uniform sampler2D _DetailTex;
uniform float4 _DetailTex_ST;
uniform half2 _DetailTex_Speed;
uniform half _DetailIntensity;
uniform half2 _DetailThreshold;
uniform half4 _DetailColor;
uniform half _DetailMode;   // 1=Masking 2=Multiply 3=Additive 4=Replace 5=MultiplyAdditive 6=Subtractive

half4 RM_ApplyDetailFilter(half4 color, float2 uvLocal)
{
    const int mode = _DetailMode;
    if (mode == 0)
    {
        return color;
    }

    const half4 inColor = color;
    const float2 uv = uvLocal * _DetailTex_ST.xy + _DetailTex_ST.zw + _Time.y * _DetailTex_Speed;
    half4 detail = tex2D(_DetailTex, uv);
    detail *= _DetailColor;

    if (mode == 1) // Masking
    {
        color *= inv_lerp(_DetailThreshold.x, _DetailThreshold.y, detail.a);
    }
    else if (mode == 2) // Multiply
    {
        color.rgb *= detail.rgb;
        color = lerp(inColor, color, _DetailIntensity * detail.a);
    }
    else if (mode == 3) // Additive
    {
        color.rgb += detail.rgb * color.a;
        color = lerp(inColor, color, _DetailIntensity * detail.a);
    }
    else if (mode == 4) // Replace
    {
        color.rgb = detail.rgb * color.a;
        color = lerp(inColor, color, _DetailIntensity * detail.a);
    }
    else if (mode == 5) // MultiplyAdditive
    {
        color.rgb *= (1 + detail.rgb);
        color = lerp(inColor, color, _DetailIntensity * detail.a);
    }
    else if (mode == 6) // Subtractive
    {
        color.rgb -= detail.rgb * color.a;
        color = lerp(inColor, color, _DetailIntensity * detail.a);
    }

    return color;
}

#endif // RM_DETAIL
