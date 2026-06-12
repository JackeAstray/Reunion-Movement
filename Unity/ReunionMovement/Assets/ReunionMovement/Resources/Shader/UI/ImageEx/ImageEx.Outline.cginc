// ImageEx Outline Module
// 从 ImageEx.shader 提取的描边（含虚线）逻辑，实现保持一致

void ApplyOutlinedSdf(inout half4 color, v2f IN, float sdfData, float pixelScale)
{
    #if OUTLINED
        float alpha = sampleSdf(sdfData, pixelScale);
        float lerpFac = sampleSdf(sdfData + _OutlineWidth, pixelScale);

        #if DASHED_OUTLINE_STATIC
            float dashedEffect = generateDashedEffect(IN, _CustomTime, IN.shapeData.z / IN.shapeData.w, _DrawShape);
            if (_DrawShape == 1 || _DrawShape == 3)
            {
                color = half4(lerp(color.rgb, _OutlineColor.rgb, dashedEffect), lerp(_OutlineColor.a, color.a, dashedEffect));
            }
            else
            {
                color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
            }
        #else
            if (_EnableDashedOutline == 1)
            {
                // 传递图形类型
                float dashedEffect = generateDashedEffect(IN, _CustomTime, IN.shapeData.z / IN.shapeData.w, _DrawShape);

                if (_DrawShape == 1)
                {
                    color = half4(lerp(color.rgb, _OutlineColor.rgb, dashedEffect), lerp(_OutlineColor.a, color.a, dashedEffect));
                }
                else if (_DrawShape == 3)
                {
                    color = half4(lerp(color.rgb, _OutlineColor.rgb, dashedEffect), lerp(_OutlineColor.a, color.a, dashedEffect));
                }
                else
                {
                    color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
                }
            }
            else
            {
                // 普通描边效果
                color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
            }
        #endif
        color.a *= alpha;
    #endif
}
