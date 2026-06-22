#ifndef RM_TONE_FILTER
#define RM_TONE_FILTER

// ============================================================
// ReunionMovement 通用色调滤镜模块 (RM_ToneFilter)
// 适用场景：UI / 2D Sprite / 3D
// 
// 依赖：Base/Common.cginc（rgb_to_hsv, hsv_to_rgb）
// 
// 需要在包含此文件的 Shader 中声明：
//   uniform half _ToneIntensity;
// 
// 关键字：
//   TONE_GRAYSCALE  - 灰度化
//   TONE_SEPIA      - 怀旧棕褐
//   TONE_NEGATIVE   - 负片反相
//   TONE_RETRO      - 复古像素风格
//   TONE_POSTERIZE  - 色调分离
// ============================================================

#include "../Base/Common.cginc"

uniform half _ToneIntensity;

half4 RM_ApplyToneFilter(half4 color)
{
    #if TONE_GRAYSCALE
        // 灰度化：将颜色与亮度值按强度混合
        color.rgb = lerp(color.rgb, Luminance(color.rgb), _ToneIntensity);
    #elif TONE_SEPIA
        // 怀旧棕褐：先灰度化，再乘以棕褐色调
        color.rgb = lerp(color.rgb, Luminance(color.rgb) * half3(1.07, 0.74, 0.43), _ToneIntensity);
    #elif TONE_NEGATIVE
        // 负片反相：反转 RGB，保持 Alpha
        color.rgb = lerp(color.rgb, (1 - color.rgb) * color.a, _ToneIntensity);
    #elif TONE_RETRO
        // 复古像素风格：基于亮度分4级映射到 NES 调色板
        const half l = Luminance(color.rgb);
        const half r0 = step(l, 0.25);
        const half r1 = step(l, 0.5);
        const half r2 = step(l, 0.75);
        const half3 retro = half3(0.06, 0.22, 0.06) * r0               // 0.00–0.25: (15, 56, 15)
            + half3(0.19, 0.38, 0.19) * (1 - r0) * r1                 // 0.25–0.50: (48, 98, 48)
            + half3(0.54, 0.67, 0.06) * (1 - r1) * r2                 // 0.50–0.75: (139,172,15)
            + half3(0.60, 0.74, 0.06) * (1 - r2);                     // 0.75–1.00: (155,188,15)
        color.rgb = lerp(color.rgb, retro * color.a, _ToneIntensity);
    #elif TONE_POSTERIZE
        // 色调分离：将 HSV 的每个通道量化为离散级别
        const half3 hsv = rgb_to_hsv(color.rgb);
        const float div = round(lerp(48, 4, _ToneIntensity) / 2) * 2;
        color.rgb = hsv_to_rgb((floor(hsv * div) + 0.5) / div) * color.a;
    #endif

    return color;
}

#endif // RM_TONE_FILTER
