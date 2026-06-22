#ifndef RM_SAMPLING
#define RM_SAMPLING

// ============================================================
// ReunionMovement 通用采样模块 (RM_Sampling)
// 适用场景：UI / 2D Sprite / 3D
// 
// 在 RM_Blur 基础上扩展，新增：
//   - Pixelation 像素化
//   - RGB Shift 色散
//   - Edge Detection (Luminance/Alpha) Sobel 边缘检测
// 
// 关键字：
//   SAMPLING_PIXELATION     - 像素化/马赛克
//   SAMPLING_RGB_SHIFT      - RGB 通道色散偏移
//   SAMPLING_EDGE_LUMINANCE - 亮度 Sobel 边缘检测
//   SAMPLING_EDGE_ALPHA     - Alpha Sobel 边缘检测
// 
// 需要在包含此文件的 Shader 中声明：
//   sampler2D _MainTex; float4 _MainTex_TexelSize; float4 _TextureSampleAdd;
//   half _SamplingIntensity; half _SamplingWidth;
// ============================================================

#include "../Base/Common.cginc"

// 注：_MainTex, _MainTex_TexelSize, _TextureSampleAdd 由包含此文件的 Shader 声明
// 注：_SamplingWidth 已在 RM_Shadow.cginc 中声明（共用）
uniform half _SamplingIntensity;

// 获取用于边缘检测的通道值
half RM_SampleEdgeValue(float4 c)
{
    #if SAMPLING_EDGE_LUMINANCE
        return Luminance(c) * c.a;
    #elif SAMPLING_EDGE_ALPHA
        return c.a;
    #endif
    return 0;
}

// 统一采样入口：根据当前激活的采样关键字返回采样结果
// 注意：BLUR_FAST/MEDIUM/DETAIL 在 RM_Blur.cginc 中处理，此处仅处理非模糊模式
half4 RM_ApplySampling(float2 uv)
{
    // 像素化：将 UV 量化为离散块再采样
    #if SAMPLING_PIXELATION
    {
        const half2 pixelSize = max(2, (1 - lerp(0.5, 0.95, _SamplingIntensity)) / _MainTex_TexelSize.xy);
        float2 quantizedUv = round(uv * pixelSize) / pixelSize;
        return (tex2D(_MainTex, quantizedUv) + _TextureSampleAdd);
    }
    #endif

    // RGB 色散偏移：R/G/B 分别在 X 方向偏移采样
    #if SAMPLING_RGB_SHIFT
    {
        const half2 offset = half2(_SamplingIntensity * _MainTex_TexelSize.x * 20, 0);
        const half2 r = (tex2D(_MainTex, uv + offset) + _TextureSampleAdd).ra;
        const half2 g = (tex2D(_MainTex, uv) + _TextureSampleAdd).ga;
        const half2 b = (tex2D(_MainTex, uv - offset) + _TextureSampleAdd).ba;
        return half4(r.x * r.y, g.x * g.y, b.x * b.y, (r.y + g.y + b.y) / 3);
    }
    #endif

    // Sobel 边缘检测：3x3 邻域 + Sobel 算子
    #if SAMPLING_EDGE_LUMINANCE || SAMPLING_EDGE_ALPHA
    {
        const float2 d = _MainTex_TexelSize.xy * _SamplingWidth;

        const half v00 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(-d.x, -d.y)) + _TextureSampleAdd));
        const half v01 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(-d.x, 0.0)) + _TextureSampleAdd));
        const half v02 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(-d.x, +d.y)) + _TextureSampleAdd));
        const half v10 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(0.0, -d.y)) + _TextureSampleAdd));
        const half v12 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(0.0, +d.y)) + _TextureSampleAdd));
        const half v20 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(+d.x, -d.y)) + _TextureSampleAdd));
        const half v21 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(+d.x, 0.0)) + _TextureSampleAdd));
        const half v22 = RM_SampleEdgeValue((tex2D(_MainTex, uv + half2(+d.x, +d.y)) + _TextureSampleAdd));

        half sobel_h = v00 * -1.0 + v01 * -2.0 + v02 * -1.0 + v20 * 1.0 + v21 * 2.0 + v22 * 1.0;
        half sobel_v = v00 * -1.0 + v10 * -2.0 + v20 * -1.0 + v02 * 1.0 + v12 * 2.0 + v22 * 1.0;

        const half sobel = sqrt(sobel_h * sobel_h + sobel_v * sobel_v) * _SamplingIntensity;
        const half4 original = (tex2D(_MainTex, uv) + _TextureSampleAdd);
        return lerp(half4(0, 0, 0, 0), original, inv_lerp(0.5, 1, sobel));
    }
    #endif

    // 无特殊采样模式时返回标准采样
    return (tex2D(_MainTex, uv) + _TextureSampleAdd);
}

#endif // RM_SAMPLING
