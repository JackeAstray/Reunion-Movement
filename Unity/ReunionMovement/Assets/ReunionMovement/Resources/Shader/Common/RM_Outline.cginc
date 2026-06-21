#ifndef RM_OUTLINE
#define RM_OUTLINE

// ============================================================
// ReunionMovement 通用描边模块 (RM_Outline)
// 适用场景：UI / 2D Sprite / 3D
// 依赖：RM_SDFShapes.cginc（ComputeSdfData）, Base/Common.cginc（generateDashedPattern, getEdge）
// ============================================================

#include "../Base/Common.cginc"

uniform half _OutlineWidth;
uniform half4 _OutlineColor;
uniform int _EnableDashedOutline;
uniform float _CustomTime;
uniform half _StrokeWidth;
uniform half _StrokeFill;
uniform half _FalloffDistance;
uniform int _DrawShape;

// 生成虚线效果（支持圆形和矩形）
float RM_GenerateDashedEffect(float4 shapeData, float time, float aspectRatio, int shapeType)
{
    float wavelength = 0.2;
    float dashRatio = 0.5;
    float dashedEffect = 0.0;

    if (shapeType == 1) // CIRCLE
    {
        #if CIRCLE
        float2 center = float2(shapeData.z * 0.5, shapeData.w * 0.5);
        float angle = atan2(shapeData.y - center.y, shapeData.x - center.x); 
        float normalizedAngle = (angle + 3.1415926) / (2.0 * 3.1415926);

        float distance = length(float2(shapeData.x - center.x, shapeData.y - center.y));
        float width = shapeData.z;
        float height = shapeData.w;
        float radius = lerp(_CircleRadius, min(width, height) / 2.0, _CircleFitRadius);

        float outerRadius = radius;
        float innerRadius = radius - _OutlineWidth;
        
        float edgeMask = smoothstep(innerRadius, innerRadius + 0.01, distance) * 
                         (1.0 - smoothstep(outerRadius - 0.01, outerRadius, distance));

        float dashPattern = step(0.5, frac(normalizedAngle * 15.0 + _CustomTime));
        dashedEffect = dashPattern * edgeMask;
        #endif
    }
    else if (shapeType == 3) // RECTANGLE
    {
        float2 uv = shapeData.xy / float2(shapeData.z, shapeData.w);
        time = fmod(time, wavelength / 2);

        float dashedTop = generateDashedPattern(uv.x + time, wavelength, dashRatio);
        float dashedBottom = generateDashedPattern(uv.x - time, wavelength, dashRatio);
        float dashedLeft = generateDashedPattern(uv.y + time, wavelength, dashRatio);
        float dashedRight = generateDashedPattern(uv.y - time, wavelength, dashRatio);

        float edgeWidth = _OutlineWidth / shapeData.w;
        float edgeTop = getEdge(1.0 - uv.y, edgeWidth, 1.0);
        float edgeBottom = getEdge(uv.y, edgeWidth, 1.0);
        float edgeLeft = getEdge(uv.x, edgeWidth, aspectRatio);
        float edgeRight = getEdge(1.0 - uv.x, edgeWidth, aspectRatio);

        dashedEffect = edgeTop * dashedTop +
                       edgeBottom * dashedBottom +
                       edgeLeft * dashedLeft +
                       edgeRight * dashedRight;
    }
    return saturate(dashedEffect);
}

// 应用 SDF 描边效果
void RM_ApplyOutlinedSdf(inout half4 color, float4 shapeData, float sdfData, float pixelScale)
{
    #if OUTLINED
        float alpha = sampleSdf(sdfData, pixelScale);
        float lerpFac = sampleSdf(sdfData + _OutlineWidth, pixelScale);

        #if DASHED_OUTLINE_STATIC
            float dashedEffect = RM_GenerateDashedEffect(shapeData, _CustomTime, shapeData.z / shapeData.w, _DrawShape);
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
                float dashedEffect = RM_GenerateDashedEffect(shapeData, _CustomTime, shapeData.z / shapeData.w, _DrawShape);

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
                color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
            }
        #endif
        color.a *= alpha;
    #endif
}

#endif // RM_OUTLINE
