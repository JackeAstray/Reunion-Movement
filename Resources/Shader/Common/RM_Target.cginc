#ifndef RM_TARGET
#define RM_TARGET

// ============================================================
// ReunionMovement 目标模式模块 (RM_Target)
// 适用场景：UI / 2D Sprite / 3D
// 
// 按目标颜色过滤像素可见性（uniform _TargetMode）：
//   1 - 仅显示与目标色相接近的像素
//   2 - 仅显示与目标亮度接近的像素
// 
// 需要在包含此文件的 Shader 中声明：
//   uniform half4 _TargetColor;
//   uniform half  _TargetRange;
//   uniform half  _TargetSoftness;
//   uniform half  _TargetMode;
// 
// 依赖：Base/Common.cginc（rgb_to_hsv, inv_lerp）
// ============================================================

#include "../Base/Common.cginc"

uniform half4 _TargetColor;
uniform half _TargetRange;
uniform half _TargetSoftness;
uniform half _TargetMode;   // 0=None 1=Hue 2=Luminance

// 计算目标模式可见性比例（0=不可见，1=完全可见）
half RM_GetTargetRate(const half3 color)
{
    if (_TargetMode == 1) // Hue
    {
        if (1 <= _TargetRange) return 1;
        if (_TargetRange <= 0) return 0;

        const half value = rgb_to_hsv(color).x;
        const half target = rgb_to_hsv(_TargetColor.rgb).x;
        half diff = abs(target - value);
        diff = min(diff, 1 - diff); // 色相是环形的，取最短距离
        return 1 - inv_lerp(_TargetRange * (1 - _TargetSoftness), _TargetRange, diff);
    }
    if (_TargetMode == 2) // Luminance
    {
        if (1 <= _TargetRange) return 1;
        if (_TargetRange <= 0) return 0;

        const half value = Luminance(color);
        const half target = Luminance(_TargetColor);
        const half diff = abs(target - value);
        return 1 - inv_lerp(_TargetRange * (1 - _TargetSoftness), _TargetRange, diff);
    }

    return 1;
}

// 将目标模式比例混入最终颜色
half4 RM_ApplyTarget(half4 color, half4 original, half rate)
{
    if (_TargetMode == 0)
    {
        return color;
    }
    return lerp(original, color, rate);
}

#endif // RM_TARGET
