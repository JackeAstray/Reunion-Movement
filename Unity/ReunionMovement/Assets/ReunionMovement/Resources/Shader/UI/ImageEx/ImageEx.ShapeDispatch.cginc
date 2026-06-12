// ImageEx Shape Dispatch Module
// 从 ImageEx.shader 提取的形状分发逻辑，实现保持一致

void ComputeSdfData(v2f IN, out float sdfData, out float pixelScale)
{
    sdfData = 0;
    pixelScale = clamp(1.0/_FalloffDistance, 1.0/2048.0, 2048.0);

    #if RECTANGLE
        sdfData = rectangleScene(IN.shapeData);
    #elif CIRCLE
        sdfData = circleScene(IN.shapeData);
    #elif PENTAGON
        sdfData = pentagonScene(IN.shapeData);
    #elif TRIANGLE
        sdfData = triangleScene(IN.shapeData);
    #elif HEXAGON
        sdfData = hexagonScene(IN.shapeData);
    #elif CHAMFERBOX
        sdfData = chamferBoxScene(IN.shapeData);
    #elif PARALLELOGRAM
        sdfData = parallelogramScene(IN.shapeData);
    #elif NSTAR_POLYGON
        sdfData = nStarPolygonScene(IN.shapeData);
    #elif HEART
        sdfData = heartScene(IN.shapeData);
    #elif BLOBBYCROSS
        sdfData = blobbyCrossScene(IN.shapeData);
    #elif SQUIRCLE
        sdfData = squircleScene(IN.shapeData);
    #elif NTRIANGLE_ROUNDED
        sdfData = nTriangleRoundedScene(IN.shapeData);
    #endif
}
