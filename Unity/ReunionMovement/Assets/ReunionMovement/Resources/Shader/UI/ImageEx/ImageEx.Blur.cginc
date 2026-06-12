// ImageEx Blur Module
// 从 ImageEx.shader 提取的模糊相关函数，实现保持一致

half4 ApplyBlur(float2 uv)
{
    #if BLUR_FAST || BLUR_MEDIUM || BLUR_DETAIL
        if (_BlurIntensity > 0)
        {
            #if BLUR_FAST
                const int KERNEL_SIZE = 5;
                const float KERNEL_[5] = {0.2486, 0.7046, 1.0, 0.7046, 0.2486};
            #elif BLUR_MEDIUM
                const int KERNEL_SIZE = 9;
                const float KERNEL_[9] = { 0.0438, 0.1719, 0.4566, 0.8204, 1.0, 0.8204, 0.4566, 0.1719, 0.0438};
            #elif BLUR_DETAIL
                const int KERNEL_SIZE = 13;
                const float KERNEL_[13] = { 0.0438, 0.1138, 0.2486, 0.4566, 0.7046, 0.9141, 1.0, 0.9141, 0.7046, 0.4566, 0.2486, 0.1138, 0.0438};
            #endif

            float4 o = 0;
            float sum = 0;
            float2 shift = 0;
            const half2 blurStep = _MainTex_TexelSize.xy * _BlurIntensity * 2;

            for (int x = 0; x < KERNEL_SIZE; x++)
            {
                shift.x = blurStep.x * (float(x) - KERNEL_SIZE / 2);
                for (int y = 0; y < KERNEL_SIZE; y++)
                {
                    shift.y = blurStep.y * (float(y) - KERNEL_SIZE / 2);
                    float2 bluredUv = uv + shift;
                    float weight = KERNEL_[x] * KERNEL_[y];
                    o += (tex2D(_MainTex, bluredUv) + _TextureSampleAdd) * weight;
                    sum += weight;
                }
            }
            return sum > 0 ? o / sum : (tex2D(_MainTex, uv) + _TextureSampleAdd);
        }
    #endif
    return (tex2D(_MainTex, uv) + _TextureSampleAdd);
}
