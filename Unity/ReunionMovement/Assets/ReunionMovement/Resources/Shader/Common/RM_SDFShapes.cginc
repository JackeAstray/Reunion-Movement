#ifndef RM_SDF_SHAPES
#define RM_SDF_SHAPES

// ============================================================
// ReunionMovement 通用 SDF 形状模块 (RM_SDFShapes)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明对应的形状 Properties
// 依赖：Base/2D_SDF.cginc（SDF 基础函数）
// ============================================================

#include "../Base/2D_SDF.cginc"

// ---------- 形状参数 uniform 声明（按需由 Shader 提供） ----------

#if RECTANGLE
    uniform float4 _RectangleCornerRadius;
#endif

#if CIRCLE
    uniform float _CircleRadius;
    uniform float _CircleFitRadius;
#endif

#if PENTAGON
    uniform float4 _PentagonCornerRadius;
    uniform float _PentagonTipRadius;
    uniform float _PentagonTipSize;
#endif

#if TRIANGLE
    uniform float3 _TriangleCornerRadius;
#endif

#if HEXAGON
    uniform half2 _HexagonTipSize;
    uniform half2 _HexagonTipRadius;
    uniform half4 _HexagonCornerRadius;
#endif

#if CHAMFERBOX
    uniform float2 _ChamferBoxSize;
    uniform float4 _ChamferBoxRadius;
#endif

#if PARALLELOGRAM
    uniform float _ParallelogramValue; 
#endif

#if NSTAR_POLYGON
    uniform float _NStarPolygonSideCount;
    uniform float _NStarPolygonCornerRadius;
    uniform float _NStarPolygonInset;
    uniform float2 _NStarPolygonOffset;
#endif

#if BLOBBYCROSS
    uniform float _BlobbyCrossTime;
#endif

#if SQUIRCLE
    uniform float _SquircleTime;
#endif

#if NTRIANGLE_ROUNDED
    uniform float _NTriangleRoundedTime;
    uniform float _NTriangleRoundedNumber;
#endif

// ---------- 公共辅助函数 ----------

// 计算 SDF 遮罩：供主图和阴影复用
float RM_ComputeSdfMask(float sdfData, float pixelScale, float strokeWidth, float outlineWidth)
{
    #if STROKE
        return sampleSdfStrip(sdfData, strokeWidth + outlineWidth, pixelScale);
    #elif OUTLINED_STROKE
        return sampleSdfStrip(sdfData, outlineWidth + strokeWidth, pixelScale);
    #else
        return sampleSdf(sdfData, pixelScale);
    #endif
}

// ---------- 形状场景函数 ----------

#if RECTANGLE
half RM_RectangleScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float4 radius = _RectangleCornerRadius;
    half4 c  = half4(texcoord, size - texcoord);
    half rect = min(min(min(c.x, c.y), c.z), c.w);

    bool4 cornerRects;
    cornerRects.x = texcoord.x < radius.x && texcoord.y < radius.x;
    cornerRects.y = texcoord.x > size.x - radius.y && texcoord.y < radius.y;
    cornerRects.z = texcoord.x > size.x - radius.z && texcoord.y > size.y - radius.z;
    cornerRects.w = texcoord.x < radius.w && texcoord.y > size.y - radius.w;

    half cornerMask = any(cornerRects);

    half4 cornerCircles;
    cornerCircles.x = radius.x - length(texcoord - radius.xx);
    cornerCircles.y = radius.y - length(texcoord - half2(size.x - radius.y, radius.y));
    cornerCircles.z = radius.z - length(texcoord - (half2(size.x, size.y) - radius.zz));
    cornerCircles.w = radius.w - length(texcoord - half2(radius.w, size.y - radius.w));

    cornerCircles = min(max(cornerCircles, 0) * cornerRects, rect);
    half corners = max(max(max(cornerCircles.x, cornerCircles.y), cornerCircles.z), cornerCircles.w);
    corners = max(corners, 0.0) * cornerMask;

    return rect*(cornerMask-1) - corners;
}
#endif

#if CIRCLE
float RM_CircleScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;
    float radius = lerp(_CircleRadius, min(width, height) / 2.0, _CircleFitRadius);
    half sdf = sdCircle(texcoord - float2(width / 2.0, height / 2.0), radius);
    return sdf;
}
#endif

#if TRIANGLE
half RM_TriangleScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;
    
    half sdf = sdTriangleIsosceles(texcoord - half2(width / 2.0, height), half2(width / 2.0, -height));
    
    half3 rc = max(_TriangleCornerRadius, half3(0.001, 0.001, 0.001));

    // 左角
    half halfWidth = width / 2.0;
    half m = height / halfWidth;
    half d = sqrt(1.0 + m * m);
    half c = 0.0;
    half k = -rc.x * d + c;
    half x = (rc.x - k) / m;
    half2 circlePivot = half2(x, rc.x);
    half cornerCircle = sdCircle(texcoord - circlePivot, rc.x);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m);
    half y = m * x + c;
    half fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdf = texcoord.y < fy && texcoord.x < circlePivot.x ? cornerCircle: sdf;

    // 右角
    m = -m; c = 2.0 * height;
    k = -rc.y * d + c;
    x = (rc.y - k) / m;
    circlePivot = half2(x, rc.y);
    cornerCircle = sdCircle(texcoord - circlePivot, rc.y);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdf = texcoord.x > circlePivot.x && texcoord.y < fy ? cornerCircle: sdf;
    
    // 上角
    k = -rc.z * sqrt(1.0 + m * m) + c;
    y = m * (width / 2.0) + k;
    circlePivot = half2(halfWidth, y);
    cornerCircle = sdCircle(texcoord - circlePivot, rc.z);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, width - x, x, -1.0, 1.0);
    fy = lerp(circlePivot.y, y, abs(fy));
    sdf = texcoord.y > fy ? cornerCircle: sdf;
    
    return sdf;
}
#endif

#if PENTAGON
half RM_PentagonScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;
    
    half baseRect = sdRectanlge(texcoord - half2(width / 2.0, height / 2.0), width, height);
    half scale = height / _PentagonTipSize;
    half rhombus = sdRhombus(texcoord - float2(width / 2, _PentagonTipSize * scale), float2(width / 2, _PentagonTipSize) * scale);
    half sdfPentagon = sdfDifference(baseRect, sdfDifference(baseRect, rhombus));
    
    half ptRadius = max(_PentagonTipRadius, 0.001);
    float halfWidth = width / 2;
    float m = -_PentagonTipSize / halfWidth;
    float d = sqrt(1 + m * m);
    float c = _PentagonTipSize;
    float k = ptRadius * d + _PentagonTipSize;
    
    half2 circlePivot = half2(halfWidth, m * halfWidth + k);
    half cornerCircle = sdCircle(texcoord - circlePivot, ptRadius);
    half x = (circlePivot.y + circlePivot.x / m - c) / (m + 1 / m);
    half y = m * x + c;
    half fy = map(texcoord.x, x, width - x, -1, 1);
    fy = lerp(ptRadius, y, abs(fy));
    sdfPentagon = texcoord.y < fy ? cornerCircle: sdfPentagon;
    
    // 左中圆角
    k = _PentagonCornerRadius.w * d + _PentagonTipSize;
    circlePivot = half2(_PentagonCornerRadius.w, m * _PentagonCornerRadius.w + k);
    cornerCircle = sdCircle(texcoord - circlePivot, _PentagonCornerRadius.w);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1 / m); y = m * x + c;
    fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdfPentagon = texcoord.y > fy && texcoord.y < circlePivot.y ? cornerCircle: sdfPentagon;
    
    // 右中圆角
    m = -m; k = _PentagonCornerRadius.z * d - _PentagonTipSize;
    circlePivot = half2(width - _PentagonCornerRadius.z, m * (width - _PentagonCornerRadius.z) + k);
    cornerCircle = sdCircle(texcoord - circlePivot, _PentagonCornerRadius.z);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdfPentagon = texcoord.y > fy && texcoord.y < circlePivot.y ? cornerCircle: sdfPentagon;
    
    // 顶部圆角
    cornerCircle = sdCircle(texcoord - half2(_PentagonCornerRadius.x, height - _PentagonCornerRadius.x), _PentagonCornerRadius.x);
    bool mask = texcoord.x < _PentagonCornerRadius.x && texcoord.y > height - _PentagonCornerRadius.x;
    sdfPentagon = mask ? cornerCircle: sdfPentagon;
    cornerCircle = sdCircle(texcoord - half2(width - _PentagonCornerRadius.y, height - _PentagonCornerRadius.y), _PentagonCornerRadius.y);
    mask = texcoord.x > width - _PentagonCornerRadius.y && texcoord.y > height - _PentagonCornerRadius.y;
    sdfPentagon = mask ? cornerCircle: sdfPentagon;
    
    return sdfPentagon;
}
#endif

#if HEXAGON
half RM_HexagonScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;
    
    half baseRect = sdRectanlge(texcoord - half2(width / 2.0, height / 2.0), width, height);
    half scale = width / _HexagonTipSize.x;
    half rhombus1 = sdRhombus(texcoord - float2(_HexagonTipSize.x * scale, height / 2.0), float2(_HexagonTipSize.x, height / 2.0) * scale);
    scale = width / _HexagonTipSize.y;
    half rhombus2 = sdRhombus(texcoord - float2(width - _HexagonTipSize.y * scale, height / 2.0), float2(_HexagonTipSize.y, height / 2.0) * scale);
    half sdfHexagon = sdfDifference(sdfDifference(baseRect, -rhombus1), -rhombus2);

    // 左圆角
    float halfHeight = height / 2.0;
    float m = -halfHeight / _HexagonTipSize.x;
    float c = halfHeight;
    float d = sqrt(1.0 + m * m);
    float k = _HexagonTipRadius.x * d + c;
    half2 circlePivot = half2((halfHeight - k) / m, halfHeight);
    half cornerCircle = sdCircle(texcoord - circlePivot, _HexagonTipRadius.x);
    half x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m);
    half y = m * x + c;
    half fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdfHexagon = texcoord.y > fy && texcoord.y < height - fy ? cornerCircle: sdfHexagon;
 
    // 底部
    k = _HexagonCornerRadius.x * d + c;
    circlePivot = half2((_HexagonCornerRadius.x - k) / m, _HexagonCornerRadius.x);
    cornerCircle = sdCircle(texcoord - circlePivot, _HexagonCornerRadius.x);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdfHexagon = texcoord.y < fy && texcoord.x < circlePivot.x ? cornerCircle: sdfHexagon;

    // 顶部
    k = _HexagonCornerRadius.w * d + c;
    circlePivot = half2((_HexagonCornerRadius.w - k) / m, height - _HexagonCornerRadius.w);
    cornerCircle = sdCircle(texcoord - circlePivot, _HexagonCornerRadius.w);
    x = (_HexagonCornerRadius.w + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, x, circlePivot.x, height - y, circlePivot.y);
    sdfHexagon = texcoord.y > fy && texcoord.x < circlePivot.x ? cornerCircle: sdfHexagon;

    // 右圆角
    m = halfHeight / _HexagonTipSize.y;
    d = sqrt(1.0 + m * m);
    c = halfHeight - m * width;
    k = _HexagonTipRadius.y * d + c;
    
    circlePivot = half2((halfHeight - k) / m, halfHeight);
    cornerCircle = sdCircle(texcoord - circlePivot, _HexagonTipRadius.y);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdfHexagon = texcoord.y > fy && texcoord.y < height - fy ? cornerCircle: sdfHexagon;
    
    k = _HexagonCornerRadius.y * d + c;
    circlePivot = half2((_HexagonCornerRadius.y - k) / m, _HexagonCornerRadius.y);
    cornerCircle = sdCircle(texcoord - circlePivot, _HexagonCornerRadius.y);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdfHexagon = texcoord.y < fy && texcoord.x > circlePivot.x ? cornerCircle: sdfHexagon;
    
    k = _HexagonCornerRadius.z * d + c;
    circlePivot = half2((_HexagonCornerRadius.z - k) / m, height - _HexagonCornerRadius.z);
    cornerCircle = sdCircle(texcoord - circlePivot, _HexagonCornerRadius.z);
    x = (_HexagonCornerRadius.z + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, height - y);
    sdfHexagon = texcoord.y > fy && texcoord.x > circlePivot.x ? cornerCircle: sdfHexagon;
    
    return sdfHexagon;
}
#endif

#if CHAMFERBOX
half RM_ChamferBoxScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float2 p = (2.0 * texcoord - size) / size.y;
    float2 box = _ChamferBoxSize;
    float4 chamfer = _ChamferBoxRadius;
    chamfer = min(chamfer, float4(min(box.x, box.y), min(box.x, box.y), min(box.x, box.y), min(box.x, box.y)));

    float big = 1e5;
    float d0 = (p.x <= 0 && p.y <= 0) ? sdChamferBox(p, box, chamfer.x) : big;
    float d1 = (p.x >= 0 && p.y <= 0) ? sdChamferBox(float2(-p.x,  p.y), box, chamfer.y) : big;
    float d2 = (p.x >= 0 && p.y >= 0) ? sdChamferBox(float2(-p.x, -p.y), box, chamfer.z) : big;
    float d3 = (p.x <= 0 && p.y >= 0) ? sdChamferBox(float2( p.x, -p.y), box, chamfer.w) : big;

    float d = min(min(d0, d1), min(d2, d3));
    return d * 80.0;
}
#endif

#if PARALLELOGRAM
half RM_ParallelogramScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float2 p = (2.0 * texcoord - size) / size.y;
    float sk = 0.5 * sin(_ParallelogramValue);
    float wi = (size.x / size.y) * 0.58;
    float he = 1;
    float d = sdParallelogram(p, wi, he, sk);
    return d * 80.0;
}
#endif

#if NSTAR_POLYGON
half RM_NStarPolygonScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float width = additionalData.z;
    float height = additionalData.w;
    float size = height / 2 - _NStarPolygonCornerRadius;
    half str = sdNStarPolygon(texcoord - half2(width / 2, height / 2) - _NStarPolygonOffset, size, _NStarPolygonSideCount, _NStarPolygonInset) - _NStarPolygonCornerRadius;
    return str;
}
#endif

#if HEART
half RM_HeartScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float width = additionalData.z;
    float height = additionalData.w;
    float radius = min(width, height) * 0.8;
    float2 value = texcoord - float2(width * 0.5, height * 0.1);
    half sdf = sdHeart(value, radius) * 110;
    return sdf;
}
#endif

#if BLOBBYCROSS
half RM_BlobbyCrossScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float width = additionalData.z;
    float height = additionalData.w;
    float2 p = (2.0 * texcoord - additionalData.zw) / width;
    p *= 2.0;
    float time = _BlobbyCrossTime;
    float he = sin(time * 0.43 + 4.0);
    he = (0.001 + abs(he)) * ((he >= 0.0) ? 1.0 : -1.0);
    float ra = 0.1 + 0.5 * (0.5 + 0.5 * sin(time * 1.7)) + max(0.0, he - 0.7);
    float d = sdBlobbyCross(p, he) - ra;
    d = d * 35;
    return d;
}
#endif

#if SQUIRCLE
half RM_SquircleScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float width = additionalData.z;
    float height = additionalData.w;
    float2 p = (2.0 * texcoord - additionalData.zw) / width;
    float n = 3.0 + 3 * sin(9.8 * _SquircleTime / 2.0);
    float d = sdSquircle(p, n);
    d = d * 80;
    return d;
}
#endif

#if NTRIANGLE_ROUNDED
half RM_NTriangleRoundedScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float width = additionalData.z;
    float height = additionalData.w;
    float2 p = (2.0 * texcoord - float2(width,height)) / max(width,height);
    float time = _NTriangleRoundedTime;
    float number = _NTriangleRoundedNumber;
    float rounding = 0.1 - 0.1 * cos(radians(360.0) * time);
    float n = floor(3.0 + fmod(1.0 * number, 15.0));
    p = opRepAng(p, radians(360.0) / n, radians(30));
    float r = 1.0;
    float r_in = r * cos(radians(180.0) / n);
    float side_length = 2.0 * r_in * tan(radians(180.0) / n);
    float d = sdTriangleIsoscelesRounded(p.yx, float2(0.5 * side_length, r_in), rounding);
    return d * 80.0;
}
#endif

// ---------- SDF 分发 ----------

void RM_ComputeSdfData(float4 shapeData, float falloffDistance, out float sdfData, out float pixelScale)
{
    sdfData = 0;
    pixelScale = clamp(1.0 / falloffDistance, 1.0 / 2048.0, 2048.0);

    #if RECTANGLE
        sdfData = RM_RectangleScene(shapeData);
    #elif CIRCLE
        sdfData = RM_CircleScene(shapeData);
    #elif PENTAGON
        sdfData = RM_PentagonScene(shapeData);
    #elif TRIANGLE
        sdfData = RM_TriangleScene(shapeData);
    #elif HEXAGON
        sdfData = RM_HexagonScene(shapeData);
    #elif CHAMFERBOX
        sdfData = RM_ChamferBoxScene(shapeData);
    #elif PARALLELOGRAM
        sdfData = RM_ParallelogramScene(shapeData);
    #elif NSTAR_POLYGON
        sdfData = RM_NStarPolygonScene(shapeData);
    #elif HEART
        sdfData = RM_HeartScene(shapeData);
    #elif BLOBBYCROSS
        sdfData = RM_BlobbyCrossScene(shapeData);
    #elif SQUIRCLE
        sdfData = RM_SquircleScene(shapeData);
    #elif NTRIANGLE_ROUNDED
        sdfData = RM_NTriangleRoundedScene(shapeData);
    #endif
}

#endif // RM_SDF_SHAPES
