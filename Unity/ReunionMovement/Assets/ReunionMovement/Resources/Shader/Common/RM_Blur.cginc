#ifndef RM_BLUR
#define RM_BLUR

// ============================================================
// ReunionMovement 通用模糊模块 (RM_Blur)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明：
//   sampler2D _MainTex; float4 _MainTex_TexelSize; float4 _TextureSampleAdd;
//   half _BlurIntensity; half _BlurType;
// ============================================================

// 注：_MainTex, _MainTex_TexelSize, _TextureSampleAdd 由包含此文件的 Shader 声明
uniform half _BlurIntensity;
uniform half _BlurType;   // 0=None 1=Fast 2=Medium 3=Detail（对齐 C# BlurType 枚举）

// 按半径展开的可分离高斯核采样（KERNEL_ 为标准化前的权重，函数内再做归一化）
// 三种半径各自的权重逐一保留，与改造前逐位一致，避免改动视觉效果
#define RM_BLUR_SAMPLE(KSIZE, KERNEL_)                                                          \
    {                                                                                            \
        float4 o = 0;                                                                            \
        float sum = 0;                                                                           \
        float2 shift = 0;                                                                        \
        const half2 blurStep = _MainTex_TexelSize.xy * _BlurIntensity * 2;                       \
        [unroll]                                                                                 \
        for (int x = 0; x < KSIZE; x++)                                                          \
        {                                                                                        \
            shift.x = blurStep.x * (float(x) - KSIZE / 2);                                       \
            [unroll]                                                                             \
            for (int y = 0; y < KSIZE; y++)                                                      \
            {                                                                                    \
                shift.y = blurStep.y * (float(y) - KSIZE / 2);                                   \
                float2 bluredUv = uv + shift;                                                    \
                float weight = KERNEL_[x] * KERNEL_[y];                                          \
                o += (tex2D(_MainTex, bluredUv) + _TextureSampleAdd) * weight;                    \
                sum += weight;                                                                   \
            }                                                                                    \
        }                                                                                        \
        return sum > 0 ? o / sum : (tex2D(_MainTex, uv) + _TextureSampleAdd);                    \
    }

half4 RM_ApplyBlur(float2 uv)
{
    if (_BlurType > 0.5 && _BlurIntensity > 0)
    {
        if (_BlurType < 1.5) // Fast
        {
            const float KERNEL_[5] = {0.2486, 0.7046, 1.0, 0.7046, 0.2486};
            RM_BLUR_SAMPLE(5, KERNEL_)
        }
        if (_BlurType < 2.5) // Medium
        {
            const float KERNEL_[9] = { 0.0438, 0.1719, 0.4566, 0.8204, 1.0, 0.8204, 0.4566, 0.1719, 0.0438};
            RM_BLUR_SAMPLE(9, KERNEL_)
        }
        if (_BlurType < 3.5) // Detail
        {
            const float KERNEL_[13] = { 0.0438, 0.1138, 0.2486, 0.4566, 0.7046, 0.9141, 1.0, 0.9141, 0.7046, 0.4566, 0.2486, 0.1138, 0.0438};
            RM_BLUR_SAMPLE(13, KERNEL_)
        }
    }
    return (tex2D(_MainTex, uv) + _TextureSampleAdd);
}

#endif // RM_BLUR
