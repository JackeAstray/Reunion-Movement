#ifndef RM_TARGET
#define RM_TARGET

// ============================================================
// ReunionMovement 目标模式模块 (RM_Target)
// 适用场景：UI / 2D Sprite / 3D
// 
// 按目标颜色过滤像素可见性：
//   TARGET_HUE       - 仅显示与目标色相接近的像素
//   TARGET_LUMINANCE - 仅显示与目标亮度接近的像素
// 
// 需要在包含此文件的 Shader 中声明：
//   uniform half4 _TargetColor;
//   uniform half  _TargetRange;
//   uniform half  _TargetSoftness;
// 
// 依赖：Base/Common.cginc（rgb_to_hsv, inv_lerp）
// ============================================================

#include "../Base/Common.cginc"

uniform half4 _TargetColor;
uniform half _TargetRange;
uniform half _TargetSoftness;

// 计算目标模式可见性比例（0=不可见，1=完全可见）
half RM_GetTargetRate(const half3 color)
{
    #if TARGET_HUE
    {
        if (1 <= _TargetRange) return 1;
        if (_TargetRange <= 0) return 0;

        const half value = rgb_to_hsv(color).x;
        const half target = rgb_to_hsv(_TargetColor.rgb).x;
        half diff = abs(target - value);
        diff = min(diff, 1 - diff); // 色相是环形的，取最短距离
        return 1 - inv_lerp(_TargetRange * (1 - _TargetSoftness), _TargetRange, diff);
    }
    #elif TARGET_LUMINANCE
    {
        if (1 <= _TargetRange) return 1;
        if (_TargetRange <= 0) return 0;

        const half value = Luminance(color);
        const half target = Luminance(_TargetColor);
        const half diff = abs(target - value);
        return 1 - inv_lerp(_TargetRange * (1 - _TargetSoftness), _TargetRange, diff);
    }
    #endif

    return 1;
}

// 将目标模式比例混入最终颜色
half4 RM_ApplyTarget(half4 color, half4 original, half rate)
{
    #if TARGET_HUE || TARGET_LUMINANCE
        return lerp(original, color, rate);
    #else
        return color;
    #endif
}

#endif // RM_TARGET
