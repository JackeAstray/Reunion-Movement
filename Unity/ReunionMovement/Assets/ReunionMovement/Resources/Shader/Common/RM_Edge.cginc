#ifndef RM_EDGE
#define RM_EDGE

// ============================================================
// ReunionMovement 通用边缘效果模块 (RM_Edge)
// 适用场景：UI / 2D Sprite / 3D
// 
// 依赖：RM_Transition.cginc（RM_ApplyColorFilter）
//       Base/Common.cginc（inv_lerp）
// 
// 需要在包含此文件的 Shader 中声明：
//   sampler2D _MainTex; float4 _MainTex_TexelSize;
//   uniform half _EdgeWidth;
//   uniform int _EdgeColorFilter;
//   uniform half4 _EdgeColor;
//   uniform int _EdgeColorGlow;
//   uniform half _EdgeShinyRate;
//   uniform half _EdgeShinyWidth;
//   uniform half _EdgeShinyAutoPlaySpeed;
//   uniform half _EdgeMode;
// 
// 模式（uniform _EdgeMode）：
//   1 - 普通边缘发光
//   2 - 旋转高光边缘
// ============================================================

#include "../Base/Common.cginc"

// RM_ApplyColorFilter 由 RM_Transition.cginc 提供
half4 RM_ApplyColorFilter(int mode, half4 inColor, half4 factor, float intensity, float glow);

uniform half _EdgeWidth;
uniform int _EdgeColorFilter;
uniform half4 _EdgeColor;
uniform int _EdgeColorGlow;
uniform half _EdgeShinyRate;
uniform half _EdgeShinyWidth;
uniform half _EdgeShinyAutoPlaySpeed;
uniform half _EdgeMode;   // 0=None 1=Plain 2=Shiny

// 注：_MainTex, _MainTex_TexelSize 由包含此文件的 Shader 声明

// 计算边缘因子：对 12 个方向邻域采样，取最小 alpha，
// 当前像素 alpha 高而邻域有低 alpha 时 → 边缘区域 → factor 接近 1
float RM_ComputeEdgeFactor(float2 uv, float width)
{
    if (_EdgeMode == 0)
    {
        return 0;
    }

    const float2 d = _MainTex_TexelSize.xy * lerp(1, 20, width);
    float e = 1.0;

    // 12 方向邻域采样（30° 间隔）
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 1.0,        0.0),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 0.866025,   0.5),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 0.5,        0.866025),   0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 0.0,        1.0),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2(-0.5,        0.866025),   0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2(-0.866025,   0.5),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2(-1.0,        0.0),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2(-0.866025,  -0.5),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2(-0.5,       -0.866025),   0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 0.0,       -1.0),        0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 0.5,       -0.866025),   0, 0)).a);
    e = min(e, tex2Dlod(_MainTex, float4(uv + d * float2( 0.866025,  -0.5),        0, 0)).a);

    // e 越低 → 越靠近边缘 → factor 越高
    return 1 - inv_lerp(0.15, 0.3, e);
}

// 计算边缘高光动画（Shiny 模式）：基于角度 + 时间做旋转高光
float RM_IsEdgeShiny(float2 uvLocal, float aspect)
{
    if (_EdgeMode != 2) // 仅 Shiny 有扫光，Plain 恒为可见
    {
        return 1;
    }

    // 在按宽高比校正后的空间取角，避免非正方形矩形上扫光角速度不均匀
    const float2 dir = float2((uvLocal.x - 0.5) * aspect, uvLocal.y - 0.5);
    const float deg = atan2(dir.y, dir.x) / 3.14159;
    return frac(_EdgeShinyRate + _Time.y * _EdgeShinyAutoPlaySpeed + deg) < _EdgeShinyWidth;
}

// 应用边缘效果：在检测到的边缘区域用边缘颜色替换/混合
half4 RM_ApplyEdge(half4 color, float edgeFactor, float2 uvLocal, float aspect)
{
    if (_EdgeMode == 0)
    {
        return color;
    }

    const half4 edgeColor = RM_ApplyColorFilter(_EdgeColorFilter, color, _EdgeColor, 1, _EdgeColorGlow);
    const float isEdgeShiny = RM_IsEdgeShiny(uvLocal, aspect);
    return lerp(color, edgeColor, edgeFactor * isEdgeShiny);
}

#endif // RM_EDGE
