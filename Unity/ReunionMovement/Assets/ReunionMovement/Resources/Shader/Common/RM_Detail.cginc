#ifndef RM_DETAIL
#define RM_DETAIL

// ============================================================
// ReunionMovement 细节纹理滤镜模块 (RM_Detail)
// 适用场景：UI / 2D Sprite / 3D
// 
// 将第二张纹理以多种混合模式叠加到主图上：
//   DETAIL_MASKING           - 遮罩（基于阈值）
//   DETAIL_MULTIPLY          - 乘法
//   DETAIL_ADDITIVE          - 加法
//   DETAIL_SUBTRACTIVE       - 减法
//   DETAIL_REPLACE           - 替换
//   DETAIL_MULTIPLY_ADDITIVE - 乘法+加法
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

half4 RM_ApplyDetailFilter(half4 color, float2 uvLocal)
{
    #if DETAIL_MASKING || DETAIL_MULTIPLY || DETAIL_ADDITIVE || DETAIL_SUBTRACTIVE || DETAIL_REPLACE || DETAIL_MULTIPLY_ADDITIVE
        const half4 inColor = color;
        const float2 uv = uvLocal * _DetailTex_ST.xy + _DetailTex_ST.zw + _Time.y * _DetailTex_Speed;
        half4 detail = tex2D(_DetailTex, uv);
        detail *= _DetailColor;

        #if DETAIL_MASKING
            color *= inv_lerp(_DetailThreshold.x, _DetailThreshold.y, detail.a);
        #elif DETAIL_MULTIPLY
            color.rgb *= detail.rgb;
            color = lerp(inColor, color, _DetailIntensity * detail.a);
        #elif DETAIL_ADDITIVE
            color.rgb += detail.rgb * color.a;
            color = lerp(inColor, color, _DetailIntensity * detail.a);
        #elif DETAIL_SUBTRACTIVE
            color.rgb -= detail.rgb * color.a;
            color = lerp(inColor, color, _DetailIntensity * detail.a);
        #elif DETAIL_REPLACE
            color.rgb = detail.rgb * color.a;
            color = lerp(inColor, color, _DetailIntensity * detail.a);
        #elif DETAIL_MULTIPLY_ADDITIVE
            color.rgb *= (1 + detail.rgb);
            color = lerp(inColor, color, _DetailIntensity * detail.a);
        #endif
    #endif

    return color;
}

#endif // RM_DETAIL
