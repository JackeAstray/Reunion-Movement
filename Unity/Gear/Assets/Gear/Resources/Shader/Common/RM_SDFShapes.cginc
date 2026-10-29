#ifndef RM_SDF_SHAPES
#define RM_SDF_SHAPES

// ============================================================
// ReunionMovement 通用 SDF 形状模块 (RM_SDFShapes)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明对应的形状 Properties
// 依赖：Base/2D_SDF.cginc（SDF 基础函数）
// ============================================================

#include "../Base/2D_SDF.cginc"

uniform int _DrawShape;   // 对齐 C# DrawShape 枚举：0=None 1=Circle ... 12=NTriangleRounded

// 注：以下形状参数与形状函数原先由 RECTANGLE/CIRCLE/... 关键字包起来，用于按变体裁剪。
// 现改为全部随变体一起编译，运行时按 _DrawShape 分发（见文件末尾 RM_ComputeSdfData）：
// 形状关键字是 multi_compile，会让变体数 ×13，而每个变体都要装下全部特效代码，
// 实测体积膨胀近 8 倍；改为运行时分发后变体数从 208 降到 4（仅剩 UI 裁剪两轴），
// 总体积反而小于改造前，且 13 种形状都能在运行时自由切换。

// ---------- 形状参数 uniform 声明（按需由 Shader 提供） ----------

    uniform float4 _RectangleCornerRadius;

    uniform float _CircleRadius;
    uniform float _CircleFitRadius;

    uniform float4 _PentagonCornerRadius;
    uniform float _PentagonTipRadius;
    uniform float _PentagonTipSize;

    uniform float3 _TriangleCornerRadius;

    uniform half2 _HexagonTipSize;
    uniform half2 _HexagonTipRadius;
    uniform half4 _HexagonCornerRadius;

    uniform float2 _ChamferBoxSize;
    uniform float4 _ChamferBoxRadius;

    uniform float2 _QuadTopLeft;
    uniform float2 _QuadTopRight;
    uniform float2 _QuadBottomLeft;
    uniform float2 _QuadBottomRight;

    uniform float _NStarPolygonSideCount;
    uniform float _NStarPolygonCornerRadius;
    uniform float _NStarPolygonInset;
    uniform float2 _NStarPolygonOffset;

    uniform float _BlobbyCrossTime;

    uniform float _SquircleTime;

    uniform float _NTriangleRoundedTime;
    uniform float _NTriangleRoundedNumber;

// ---------- 公共辅助函数 ----------

// 计算 SDF 遮罩：供主图和阴影复用
float RM_ComputeSdfMask(float sdfData, float pixelScale, float strokeWidth, float outlineWidth)
{
    // 原为 STROKE / OUTLINED_STROKE 关键字：两者都是「按笔宽+轮廓宽做带状采样」，
    // 仅参数顺序不同（求和结果一致），故统一按 strokeWidth 判定
    if (strokeWidth > 0)
    {
        return sampleSdfStrip(sdfData, strokeWidth + outlineWidth, pixelScale);
    }
    return sampleSdf(sdfData, pixelScale);
}

// ---------- 形状场景函数 ----------

half RM_RectangleScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    // 半径不得超过短边一半，否则同侧两个角圆重叠成双圆凸包，顶/底中心出现 V 缝
    float4 radius = min(_RectangleCornerRadius, min(size.x, size.y) * 0.5);
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

half RM_TriangleScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;
    
    half sdf = sdTriangleIsosceles(texcoord - half2(width / 2.0, height), half2(width / 2.0, -height));
    
    // 圆角约束：底边两角之和 ≤ 宽度，顶角半径 ≤ 短边，否则角圆重叠成双凸包（同 Rectangle）
    half3 rc = max(_TriangleCornerRadius, half3(0.001, 0.001, 0.001));
    rc.xy *= min(1.0, width / max(rc.x + rc.y, 0.001));
    rc.z = min(rc.z, min(width, height));

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

half RM_PentagonScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;

    // 圆角约束：顶部两角之和 ≤ 宽度，其余角/尖端半径 ≤ 短边，尖端尺寸防除零
    half4 cr = _PentagonCornerRadius;
    cr.xy *= min(1.0, width / max(cr.x + cr.y, 0.001));
    cr = min(cr, min(width, height));
    half ptRadius = min(max(_PentagonTipRadius, 0.001), min(width, height));
    half tipSize = max(_PentagonTipSize, 0.001);

    half baseRect = sdRectanlge(texcoord - half2(width / 2.0, height / 2.0), width, height);
    half scale = height / tipSize;
    half rhombus = sdRhombus(texcoord - float2(width / 2, tipSize * scale), float2(width / 2, tipSize) * scale);
    half sdfPentagon = sdfDifference(baseRect, sdfDifference(baseRect, rhombus));
    
    float halfWidth = width / 2;
    float m = -tipSize / halfWidth;
    float d = sqrt(1 + m * m);
    float c = tipSize;
    float k = ptRadius * d + tipSize;
    
    half2 circlePivot = half2(halfWidth, m * halfWidth + k);
    half cornerCircle = sdCircle(texcoord - circlePivot, ptRadius);
    half x = (circlePivot.y + circlePivot.x / m - c) / (m + 1 / m);
    half y = m * x + c;
    half fy = map(texcoord.x, x, width - x, -1, 1);
    fy = lerp(ptRadius, y, abs(fy));
    sdfPentagon = texcoord.y < fy ? cornerCircle: sdfPentagon;
    
    // 左中圆角
    k = cr.w * d + tipSize;
    circlePivot = half2(cr.w, m * cr.w + k);
    cornerCircle = sdCircle(texcoord - circlePivot, cr.w);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1 / m); y = m * x + c;
    fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdfPentagon = texcoord.y > fy && texcoord.y < circlePivot.y ? cornerCircle: sdfPentagon;
    
    // 右中圆角
    m = -m; k = cr.z * d - tipSize;
    circlePivot = half2(width - cr.z, m * (width - cr.z) + k);
    cornerCircle = sdCircle(texcoord - circlePivot, cr.z);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdfPentagon = texcoord.y > fy && texcoord.y < circlePivot.y ? cornerCircle: sdfPentagon;
    
    // 顶部圆角
    cornerCircle = sdCircle(texcoord - half2(cr.x, height - cr.x), cr.x);
    bool mask = texcoord.x < cr.x && texcoord.y > height - cr.x;
    sdfPentagon = mask ? cornerCircle: sdfPentagon;
    cornerCircle = sdCircle(texcoord - half2(width - cr.y, height - cr.y), cr.y);
    mask = texcoord.x > width - cr.y && texcoord.y > height - cr.y;
    sdfPentagon = mask ? cornerCircle: sdfPentagon;
    
    return sdfPentagon;
}

half RM_HexagonScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);
    float width = size.x;
    float height = size.y;

    // 圆角约束：底/顶两角之和 ≤ 宽度，角/尖端半径 ≤ 短边，尖端尺寸防除零
    half4 cr = _HexagonCornerRadius;
    cr.xy *= min(1.0, width / max(cr.x + cr.y, 0.001));
    cr.zw *= min(1.0, width / max(cr.z + cr.w, 0.001));
    cr = min(cr, min(width, height));
    half2 tr = min(max(_HexagonTipRadius.xy, 0.001), min(width, height));
    half2 tipSize = max(_HexagonTipSize.xy, 0.001);

    half baseRect = sdRectanlge(texcoord - half2(width / 2.0, height / 2.0), width, height);
    half scale = width / tipSize.x;
    half rhombus1 = sdRhombus(texcoord - float2(tipSize.x * scale, height / 2.0), float2(tipSize.x, height / 2.0) * scale);
    scale = width / tipSize.y;
    half rhombus2 = sdRhombus(texcoord - float2(width - tipSize.y * scale, height / 2.0), float2(tipSize.y, height / 2.0) * scale);
    half sdfHexagon = sdfDifference(sdfDifference(baseRect, -rhombus1), -rhombus2);

    // 左圆角
    float halfHeight = height / 2.0;
    float m = -halfHeight / tipSize.x;
    float c = halfHeight;
    float d = sqrt(1.0 + m * m);
    float k = tr.x * d + c;
    half2 circlePivot = half2((halfHeight - k) / m, halfHeight);
    half cornerCircle = sdCircle(texcoord - circlePivot, tr.x);
    half x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m);
    half y = m * x + c;
    half fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdfHexagon = texcoord.y > fy && texcoord.y < height - fy ? cornerCircle: sdfHexagon;
 
    // 底部
    k = cr.x * d + c;
    circlePivot = half2((cr.x - k) / m, cr.x);
    cornerCircle = sdCircle(texcoord - circlePivot, cr.x);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, x, circlePivot.x, y, circlePivot.y);
    sdfHexagon = texcoord.y < fy && texcoord.x < circlePivot.x ? cornerCircle: sdfHexagon;

    // 顶部
    k = cr.w * d + c;
    circlePivot = half2((cr.w - k) / m, height - cr.w);
    cornerCircle = sdCircle(texcoord - circlePivot, cr.w);
    x = (cr.w + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, x, circlePivot.x, height - y, circlePivot.y);
    sdfHexagon = texcoord.y > fy && texcoord.x < circlePivot.x ? cornerCircle: sdfHexagon;

    // 右圆角
    m = halfHeight / tipSize.y;
    d = sqrt(1.0 + m * m);
    c = halfHeight - m * width;
    k = tr.y * d + c;
    
    circlePivot = half2((halfHeight - k) / m, halfHeight);
    cornerCircle = sdCircle(texcoord - circlePivot, tr.y);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdfHexagon = texcoord.y > fy && texcoord.y < height - fy ? cornerCircle: sdfHexagon;
    
    k = cr.y * d + c;
    circlePivot = half2((cr.y - k) / m, cr.y);
    cornerCircle = sdCircle(texcoord - circlePivot, cr.y);
    x = (circlePivot.y + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, y);
    sdfHexagon = texcoord.y < fy && texcoord.x > circlePivot.x ? cornerCircle: sdfHexagon;
    
    k = cr.z * d + c;
    circlePivot = half2((cr.z - k) / m, height - cr.z);
    cornerCircle = sdCircle(texcoord - circlePivot, cr.z);
    x = (cr.z + circlePivot.x / m - c) / (m + 1.0 / m); y = m * x + c;
    fy = map(texcoord.x, circlePivot.x, x, circlePivot.y, height - y);
    sdfHexagon = texcoord.y > fy && texcoord.x > circlePivot.x ? cornerCircle: sdfHexagon;
    
    return sdfHexagon;
}

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

// 四边形（梯形 / 平行四边形等）：四个角可独立偏移。
// 偏移值为矩形宽/高比例（-0.5 ~ 0.5），正值朝矩形内部收缩。
// 四个角（像素空间，y 向上）按逆时针排列：BL -> BR -> TR -> TL。
half RM_QuadrilateralScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float2 size = float2(additionalData.z, additionalData.w);

    // 偏移（-0.5 ~ 0.5 比例）乘以矩形尺寸得到像素偏移；
    // clamp 保证角点不会越过中线，保持四边形凸性、避免自交
    float2 oBl = clamp(_QuadBottomLeft, -0.5, 0.5) * size;
    float2 oBr = clamp(_QuadBottomRight, -0.5, 0.5) * size;
    float2 oTr = clamp(_QuadTopRight, -0.5, 0.5) * size;
    float2 oTl = clamp(_QuadTopLeft, -0.5, 0.5) * size;

    float2 bl = float2(0.0, 0.0) + oBl;
    float2 br = float2(size.x, 0.0) + oBr;
    float2 tr = float2(size.x, size.y) + oTr;
    float2 tl = float2(0.0, size.y) + oTl;

    return sdQuad(texcoord, bl, br, tr, tl);
}

half RM_NStarPolygonScene(float4 additionalData)
{
    float2 texcoord = additionalData.xy;
    float width = additionalData.z;
    float height = additionalData.w;
    // 圆角半径超过高的一半时星形尺寸为负，钳制到短边一半以内
    float cornerRadius = min(_NStarPolygonCornerRadius, height * 0.5);
    float size = height / 2 - cornerRadius;
    half str = sdNStarPolygon(texcoord - half2(width / 2, height / 2) - _NStarPolygonOffset, size, _NStarPolygonSideCount, _NStarPolygonInset) - cornerRadius;
    return str;
}

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

// ---------- SDF 分发 ----------

void RM_ComputeSdfData(float4 shapeData, float falloffDistance, out float sdfData, out float pixelScale)
{
    sdfData = 0;
    pixelScale = clamp(1.0 / falloffDistance, 1.0 / 2048.0, 2048.0);

    // 运行时按 _DrawShape 分发（0=None 时保持 sdfData=0，调用方不会走 SDF 路径）
    if (_DrawShape == 3)        sdfData = RM_RectangleScene(shapeData);
    else if (_DrawShape == 1)   sdfData = RM_CircleScene(shapeData);
    else if (_DrawShape == 4)   sdfData = RM_PentagonScene(shapeData);
    else if (_DrawShape == 2)   sdfData = RM_TriangleScene(shapeData);
    else if (_DrawShape == 5)   sdfData = RM_HexagonScene(shapeData);
    else if (_DrawShape == 6)   sdfData = RM_ChamferBoxScene(shapeData);
    else if (_DrawShape == 7)   sdfData = RM_QuadrilateralScene(shapeData);
    else if (_DrawShape == 8)   sdfData = RM_NStarPolygonScene(shapeData);
    else if (_DrawShape == 9)   sdfData = RM_HeartScene(shapeData);
    else if (_DrawShape == 10)  sdfData = RM_BlobbyCrossScene(shapeData);
    else if (_DrawShape == 11)  sdfData = RM_SquircleScene(shapeData);
    else if (_DrawShape == 12)  sdfData = RM_NTriangleRoundedScene(shapeData);
}

#endif // RM_SDF_SHAPES
