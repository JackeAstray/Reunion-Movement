// ImageEx Shadow Module
// 从 ImageEx.shader 提取的阴影/镜像分支，实现保持一致

half4 RenderShadow(v2f IN, half2 texcoord, float transAlpha, float2 transitionFilterUv)
{
    float2 texel = _MainTex_TexelSize.xy * _SamplingScale * _SamplingWidth;

    half4 blurSample = 0;
    bool allowOOB = _AllowOutOfBoundsShadow > 0.5;

    // 阴影缩放已在网格阶段完成，这里保持原始采样，避免与 SDF 再次缩放叠加导致圆角失真
    float shadowScale = max(_ShadowScale, 0.0001);
    float2 sampleUv = texcoord;
    if (!allowOOB) sampleUv = saturate(sampleUv);
    fixed4 baseSample = tex2D(_MainTex, sampleUv) + _TextureSampleAdd;

    // 如果为镜像模式（Shadow Mirror），则采样镜像纹理并根据设置生成反射效果而非阴影。
    // 注意：镜像模式会使用 _ShadowMirrorDirection/_ShadowMirrorScale/_ShadowMirrorOffset 控制变换。
    if (_ShadowMode == 3)
    {
        float2 mirrorUv = sampleUv;
        if (_ShadowMirrorDirection == 1)
        {
            mirrorUv.x = 1.0 - (mirrorUv.x - 0.5) * _ShadowMirrorScale - 0.5 + _ShadowMirrorOffset.x;
        }
        else
        {
            mirrorUv.y = 1.0 - (mirrorUv.y - 0.5) * _ShadowMirrorScale - 0.5 + _ShadowMirrorOffset.y;
        }
        if (!_AllowOutOfBoundsShadow) mirrorUv = saturate(mirrorUv);
        fixed4 mirrorSample = tex2D(_MainTex, mirrorUv) + _TextureSampleAdd;
        float mirrorA = mirrorSample.a;
        float mirrorShadowAlpha = mirrorA * _ShadowColor.a * IN.color.a;
        half4 mirrorShadowOut = half4(_ShadowColor.rgb * mirrorShadowAlpha, mirrorShadowAlpha);

        if (_ShadowMirrorShowSource > 0.5)
        {
            half4 tint = lerp(_ShadowColor, _Color, clamp(_ShadowMirrorTintMix, 0.0, 1.0));
            half4 src = mirrorSample * tint;
            #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
                src = apply_transition_filter(src, transAlpha, transitionFilterUv);
            #endif
            return src;
        }

        return mirrorShadowOut;
    }

    float shadowMask = baseSample.a;

    // 阴影复用 ImageEx 现有的衰减逻辑：仍使用相同 sampleSdf 曲线，但由阴影参数驱动距离
    #if RECTANGLE || CIRCLE || PENTAGON || TRIANGLE || HEXAGON || CHAMFERBOX || PARALLELOGRAM || NSTAR_POLYGON || HEART || BLOBBYCROSS || SQUIRCLE || NTRIANGLE_ROUNDED
        float sdfDataShadow = 0;
        float shadowFalloffDistance = max(_ShadowBlurIntensity, 0.0001);
        float pixelScaleShadow = clamp(1.0 / shadowFalloffDistance, 1.0 / 2048.0, 2048.0);

        // 阴影缩放已在网格阶段完成，SDF 使用原 shapeData 以保持圆角与边缘形状一致
        #if RECTANGLE
            sdfDataShadow = rectangleScene(IN.shapeData);
        #elif CIRCLE
            sdfDataShadow = circleScene(IN.shapeData);
        #elif PENTAGON
            sdfDataShadow = pentagonScene(IN.shapeData);
        #elif TRIANGLE
            sdfDataShadow = triangleScene(IN.shapeData);
        #elif HEXAGON
            sdfDataShadow = hexagonScene(IN.shapeData);
        #elif CHAMFERBOX
            sdfDataShadow = chamferBoxScene(IN.shapeData);
        #elif PARALLELOGRAM
            sdfDataShadow = parallelogramScene(IN.shapeData);
        #elif NSTAR_POLYGON
            sdfDataShadow = nStarPolygonScene(IN.shapeData);
        #elif HEART
            sdfDataShadow = heartScene(IN.shapeData);
        #elif BLOBBYCROSS
            sdfDataShadow = blobbyCrossScene(IN.shapeData);
        #elif SQUIRCLE
            sdfDataShadow = squircleScene(IN.shapeData);
        #elif NTRIANGLE_ROUNDED
            sdfDataShadow = nTriangleRoundedScene(IN.shapeData);
        #endif

        shadowMask *= computeSdfMask(sdfDataShadow, pixelScaleShadow);
    #endif

    float shadowAlpha = shadowMask * _ShadowColor.a * IN.color.a;
    half4 shadowOut = half4(_ShadowColor.rgb * shadowAlpha, shadowAlpha);

    #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
        shadowOut = apply_transition_filter(shadowOut, transAlpha, transitionFilterUv);
    #endif

    return shadowOut;
}
