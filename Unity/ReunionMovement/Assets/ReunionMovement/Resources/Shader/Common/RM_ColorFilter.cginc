#ifndef RM_COLOR_FILTER
#define RM_COLOR_FILTER

// ============================================================
// ReunionMovement 通用颜色滤镜模块 (RM_ColorFilter)
// 适用场景：UI / 2D Sprite / 3D
// 
// 依赖：RM_Transition.cginc（RM_ApplyColorFilter）
//       Base/Common.cginc（rgb_to_hsv, hsv_to_rgb, inv_lerp）
// 
// 需要在包含此文件的 Shader 中声明：
//   uniform int _ColorFilter;
//   uniform half4 _ColorValue;
//   uniform half _ColorIntensity;
//   uniform int _ColorGlow;
// 
// 关键字：
//   COLOR_FILTER - 启用独立颜色滤镜
// 
// 滤镜模式（_ColorFilter 值）：
//   1 = Multiply        - 乘法混合
//   2 = Additive        - 加法混合
//   3 = Subtractive     - 减法混合
//   4 = Replace         - 替换
//   5 = MultiplyLuminance - 亮度乘法
//   6 = MultiplyAdditive  - 乘法+加法
//   7 = HsvModifier     - HSV 偏移
//   8 = Contrast        - 对比度+亮度
// ============================================================

// RM_ApplyColorFilter 已在 RM_Transition.cginc 中定义
// 此处仅声明以消除编译警告，实际实现由 RM_Transition.cginc 提供
half4 RM_ApplyColorFilter(int mode, half4 inColor, half4 factor, float intensity, float glow);

uniform int _ColorFilter;
uniform half4 _ColorValue;
uniform half _ColorIntensity;
uniform int _ColorGlow;

half4 RM_ApplyStandaloneColorFilter(half4 color)
{
    #if COLOR_FILTER
        return RM_ApplyColorFilter(_ColorFilter, color, _ColorValue, _ColorIntensity, _ColorGlow);
    #else
        return color;
    #endif
}

#endif // RM_COLOR_FILTER
