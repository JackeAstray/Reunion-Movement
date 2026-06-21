// ============================================================
// ReunionMovement SDF Shape Shader for 3D (Unlit)
// 基于 Common/ 通用模块，适用于 3D MeshRenderer
// 使用世界空间/物体空间 UV 投影
// ============================================================
Shader "ReunionMovement/3D/SDFShape"
{
    Properties
    {
        _MainTex ("Albedo Texture", 2D) = "white" { }
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TextureSize ("Texture Size (用于 SDF 形状计算)", Vector) = (1, 1, 1, 1)
        
        _DrawShape ("绘制形状", int) = 2
        
        _StrokeWidth ("线条宽度", float) = 0
        _StrokeFill ("描边填充比例", Range(0,1)) = 0
        _FalloffDistance ("衰减距离", float) = 0.5
        _ShapeRotation ("形状旋转", float) = 0
        _ConstrainRotation("约束旋转", int) = 0
        _FlipHorizontal ("水平翻转", int) = 0
        _FlipVertical ("垂直翻转", int) = 0
        
        _RectangleCornerRadius ("矩形四个角的圆角半径", Vector) = (0, 0, 0, 0)
        _CircleRadius ("圆半径", float) = 0
        _CircleFitRadius ("拟合圆半径", float) = 0
        _PentagonCornerRadius ("五边形四个角的圆角半径", Vector) = (0, 0, 0, 0)
        _PentagonTipRadius ("五边形顶部尖角的圆角半径", float) = 0
        _PentagonTipSize ("五边形顶部尖角的大小", float) = 0
        _TriangleCornerRadius ("三角形三个角的圆角半径", Vector) = (0, 0, 0, 0)
        _HexagonTipSize ("六边形顶部尖角的大小", Vector) = (0, 0, 0, 0)
        _HexagonTipRadius ("六边形顶部尖角的圆角半径", Vector) = (0, 0, 0, 0)
        _HexagonCornerRadius ("六边形六个角的圆角半径", Vector) = (0, 0, 0, 0)
        _ChamferBoxSize ("倒角盒子尺寸", Vector) = (0.8, 0.4, 0, 0)
        _ChamferBoxRadius ("倒角半径", Vector) = (0.15, 0.15, 0.15, 0.15)
        _ParallelogramValue ("平行四边形值", Float) = 0
        _NStarPolygonSideCount ("星形多边形的边数", float) = 3
        _NStarPolygonInset ("星形多边形的内凹程度", float) = 2
        _NStarPolygonCornerRadius ("星形多边形角的圆角半径", float) = 0
        _NStarPolygonOffset ("星形多边形的偏移量", Vector) = (0, 0, 0, 0)

        _EnableGradient ("启用渐变效果", int) = 0
        _GradientInterpolationType ("渐变的插值方式", int) = 0
        _GradientRotation ("渐变旋转", float) = 0
        _GradientColor0 ("渐变颜色 0", Vector) = (0, 0, 0, 0)
        _GradientColor1 ("渐变颜色 1", Vector) = (1, 1, 1, 1)
        _GradientColor2 ("渐变颜色 2", Vector) = (0, 0, 0, 0)
        _GradientColor3 ("渐变颜色 3", Vector) = (0, 0, 0, 0)
        _GradientColor4 ("渐变颜色 4", Vector) = (0, 0, 0, 0)
        _GradientColor5 ("渐变颜色 5", Vector) = (0, 0, 0, 0)
        _GradientColor6 ("渐变颜色 6", Vector) = (0, 0, 0, 0)
        _GradientColor7 ("渐变颜色 7", Vector) = (0, 0, 0, 0)
        _GradientColorLength ("渐变颜色的数量", int) = 0
        _GradientAlpha0 ("渐变透明度 0", Vector) = (1, 0, 0, 0)
        _GradientAlpha1 ("渐变透明度 1", Vector) = (1, 1, 0, 0)
        _GradientAlpha2 ("渐变透明度 2", Vector) = (0, 0, 0, 0)
        _GradientAlpha3 ("渐变透明度 3", Vector) = (0, 0, 0, 0)
        _GradientAlpha4 ("渐变透明度 4", Vector) = (0, 0, 0, 0)
        _GradientAlpha5 ("渐变透明度 5", Vector) = (0, 0, 0, 0)
        _GradientAlpha6 ("渐变透明度 6", Vector) = (0, 0, 0, 0)
        _GradientAlpha7 ("渐变透明度 7", Vector) = (0, 0, 0, 0)
        _GradientAlphaLength ("渐变透明度的数量", int) = 0
        _CornerGradientColor0 ("角0渐变效果", Color) = (1, 0, 0, 1)
        _CornerGradientColor1 ("角1渐变效果", Color) = (0, 1, 0, 1)
        _CornerGradientColor2 ("角2渐变效果", Color) = (0, 0, 1, 1)
        _CornerGradientColor3 ("角3渐变效果", Color) = (0, 0, 0, 1)
        
        _OutlineWidth ("轮廓宽", float) = 0
        _OutlineColor ("轮廓颜色", Color) = (0, 0, 0, 1)
        _EnableDashedOutline ("启用虚线轮廓", int) = 0 

        _BlurIntensity ("模糊强度", Range(0, 1)) = 0

        _TransitionMode ("过渡模式", int) = 0
        _TransitionTex ("过渡纹理", 2D) = "white" {}
        _TransitionTexRotation ("过渡纹理旋转", Float) = 0
        _TransitionRate ("过渡进度", Range(0, 1)) = 0
        [HDR] _TransitionColor ("过渡颜色", Color) = (1, 1, 1, 1)
        _TransitionWidth ("过渡宽度", Range(0, 1)) = 0.1
        _TransitionSoftness ("过渡柔和度", Range(0, 1)) = 0.1
        _TransitionReverse ("反向过渡", int) = 0
        _TransitionTex_Speed ("过渡纹理速度", Vector) = (0, 0, 0, 0)
        _TransitionPatternReverse ("过渡模式反转", int) = 0
        _TransitionAutoPlaySpeed ("过渡自动播放速度", float) = 0
        _TransitionColorFilter ("过渡色滤镜", int) = 0
        _TransitionColorGlow ("过渡颜色发光", int) = 0
        _TransitionGradientTex ("过渡渐变纹理", 2D) = "white" {}
        _TransitionRange ("过渡范围", Vector) = (0, 1, 0, 0)
        [Toggle] _TransitionClamp ("夹过渡纹理", Float) = 1
        _TransitionTexClampPadding ("过渡图块夹边距（像素）", Range(0, 4)) = 1
        [Toggle] _TransitionUseUv0 ("过渡使用 UV0", Float) = 1

        [HDR] _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowBlurIntensity ("Shadow Decay Intensity", Range(0,100)) = 1
        _SamplingWidth ("Sampling Width", Float) = 1
        _SamplingScale ("Sampling Scale", Float) = 1
        _AllowOutOfBoundsShadow ("Allow Out Of Bounds Shadow", Float) = 1
        _ShadowScale ("Shadow Scale", Range(0.1, 4)) = 1
        _ShadowMode ("Shadow Mode", Int) = 0
        _ShadowMirrorDirection ("Shadow Mirror Direction", Int) = 0
        _ShadowMirrorScale ("Shadow Mirror Scale", Float) = 1
        _ShadowMirrorOffset ("Shadow Mirror Offset", Vector) = (0,0,0,0)
        _ShadowMirrorShowSource ("Shadow Mirror Show Source", Float) = 0
        _ShadowMirrorTintMix ("Shadow Mirror Tint Mix", Range(0,1)) = 0.5

        _BlobbyCrossTime ("水滴十字形状的动态时间参数", Float) = 0
        _SquircleTime ("方圆形形状的动态时间参数", Float) = 1
        _NTriangleRoundedTime ("N三角形圆角形状的动态时间参数", Float) = 0
        _NTriangleRoundedNumber ("N三角形圆角形状的边数", Float) = 0
    }
    
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "Default"
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "../Base/2D_SDF.cginc"
            #include "../Base/Common.cginc"
            
            // ============================================================
            // ReunionMovement 通用模块
            // ============================================================
            #include "../Common/RM_SDFShapes.cginc"
            #include "../Common/RM_Gradient.cginc"
            
            #pragma multi_compile_local _ CIRCLE TRIANGLE RECTANGLE PENTAGON HEXAGON CHAMFERBOX PARALLELOGRAM NSTAR_POLYGON HEART BLOBBYCROSS SQUIRCLE NTRIANGLE_ROUNDED
            #pragma multi_compile_local _ STROKE OUTLINED OUTLINED_STROKE
            #pragma shader_feature_local _ GRADIENT_LINEAR GRADIENT_RADIAL GRADIENT_CORNER
            #pragma shader_feature_local _ BLUR_FAST BLUR_MEDIUM BLUR_DETAIL
            #pragma shader_feature_local _ TRANSITION_FADE TRANSITION_CUTOFF TRANSITION_DISSOLVE TRANSITION_SHINY TRANSITION_MASK TRANSITION_MELT TRANSITION_BURN TRANSITION_PATTERN TRANSITION_BLAZE
            #pragma shader_feature_local _ DASHED_OUTLINE_STATIC
            #pragma shader_feature_local _ TRANSITION_CLAMP_STATIC
            #pragma shader_feature_local _ TRANSITION_UV_EFFECT_STATIC

            struct appdata_t
            {
                float4 vertex: POSITION;
                float2 texcoord: TEXCOORD0;
                float4 color: COLOR;
            };
            
            struct v2f
            {
                float4 vertex: SV_POSITION;
                float2 texcoord: TEXCOORD0;
                float4 shapeData: TEXCOORD1;
                float2 effectsUv: TEXCOORD2;
                fixed4 color: COLOR;
            };
            
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _TextureSampleAdd;
            fixed4 _TextureSize;
            half _ShapeRotation;
            half _ConstrainRotation;
            half _FlipHorizontal;
            half _FlipVertical;

            // 别名映射
            #define rectangleScene       RM_RectangleScene
            #define circleScene          RM_CircleScene
            #define triangleScene        RM_TriangleScene
            #define pentagonScene        RM_PentagonScene
            #define hexagonScene         RM_HexagonScene
            #define chamferBoxScene      RM_ChamferBoxScene
            #define parallelogramScene   RM_ParallelogramScene
            #define nStarPolygonScene    RM_NStarPolygonScene
            #define heartScene           RM_HeartScene
            #define blobbyCrossScene     RM_BlobbyCrossScene
            #define squircleScene        RM_SquircleScene
            #define nTriangleRoundedScene RM_NTriangleRoundedScene
            #define computeSdfMask(sdf, ps)  RM_ComputeSdfMask(sdf, ps, _StrokeWidth, _OutlineWidth)
            #define ComputeSdfData(shapeData, fd, sdf, ps) RM_ComputeSdfData(shapeData, fd, sdf, ps)
            #define generateDashedEffect(sd, t, ar, st) RM_GenerateDashedEffect(sd, t, ar, st)
            #define ApplyGradientColor(c, uv)   RM_ApplyGradientColor(c, uv)
            #define ApplyBlur(uv)               RM_ApplyBlur(uv)
            #define ApplyOutlinedSdf(c, sd, sdf, ps) RM_ApplyOutlinedSdf(c, sd, sdf, ps)
            #define apply_transition_filter(c, a, uv)  RM_ApplyTransitionFilter(c, a, uv)
            #define transition_alpha(uv)         RM_TransitionAlpha(uv)
            #define move_transition_filter(m, a) RM_MoveTransitionFilter(m, a)
            #define transition_rate()            RM_TransitionRate()
            #define apply_color_filter(m, c, f, i, g) RM_ApplyColorFilter(m, c, f, i, g)

            #include "../Common/RM_Transition.cginc"
            #include "../Common/RM_Blur.cginc"
            #include "../Common/RM_Outline.cginc"
            #include "../Common/RM_Shadow.cginc"

            // 3D 顶点着色器
            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.effectsUv = v.texcoord;
                OUT.color = v.color * _Color;
                
                OUT.texcoord.x = lerp(OUT.texcoord.x, 1 - OUT.texcoord.x, _FlipHorizontal);
                OUT.texcoord.y = lerp(OUT.texcoord.y, 1 - OUT.texcoord.y, _FlipVertical);
                OUT.effectsUv.x = lerp(OUT.effectsUv.x, 1 - OUT.effectsUv.x, _FlipHorizontal);
                OUT.effectsUv.y = lerp(OUT.effectsUv.y, 1 - OUT.effectsUv.y, _FlipVertical);
                
                // 使用纹理尺寸作为 SDF 形状计算的像素空间参考
                float2 size = float2(_TextureSize.x + _FalloffDistance, _TextureSize.y + _FalloffDistance);
                float shapeRotation = radians(_ShapeRotation);
                size = _ConstrainRotation > 0.0 && frac(abs(shapeRotation) / 3.14159) > 0.1 ? float2(size.y, size.x) : size;
                
                float2 shapeUv = _ConstrainRotation > 0 ? v.texcoord : v.texcoord * size;
                shapeUv = rotateUV(shapeUv, shapeRotation, _ConstrainRotation > 0 ? float2(0.5, 0.5) : size * 0.5);
                shapeUv *= _ConstrainRotation > 0.0 ? size : 1.0;
                
                shapeUv.x = lerp(shapeUv.x, abs(size.x - shapeUv.x), _FlipHorizontal);
                shapeUv.y = lerp(shapeUv.y, abs(size.y - shapeUv.y), _FlipVertical);
                
                OUT.shapeData = float4(shapeUv.x, shapeUv.y, size.x, size.y);
                OUT.shapeData.x = lerp(OUT.shapeData.x, OUT.shapeData.z - OUT.shapeData.x, _FlipHorizontal);
                OUT.shapeData.y = lerp(OUT.shapeData.y, OUT.shapeData.w - OUT.shapeData.y, _FlipVertical);
                
                return OUT;
            }
            
            fixed4 frag(v2f IN): SV_Target
            {
                half4 color = IN.color;
                half2 texcoord = IN.texcoord;
                float2 effectsUv = IN.effectsUv;
                float2 transitionBaseUv = texcoord;
                #if TRANSITION_UV_EFFECT_STATIC
                    transitionBaseUv = effectsUv;
                #else
                    transitionBaseUv = (_TransitionUseUv0 > 0.5) ? texcoord : effectsUv;
                #endif
                float2 transitionUv = transitionBaseUv;
                float2 transitionFilterUv = transitionBaseUv;

                float transAlpha = 1;
                #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
                    #if TRANSITION_PATTERN
                        const half scale = lerp(100, 1, _TransitionWidth);
                        const half2 time = half2(-transition_rate() * 2, 0);
                        if (_TransitionTexRotation != 0)
                            transitionUv = rotateUV(transitionUv, radians(_TransitionTexRotation), float2(0.5, 0.5));
                        transitionFilterUv = transitionUv;
                        transitionUv = transitionUv * _TransitionTex_ST.xy * scale + _TransitionTex_ST.zw + time;
                        transAlpha = tex2D(_TransitionTex, transitionUv).a;
                        transAlpha = _TransitionReverse ? 1 - transAlpha : transAlpha;
                    #else
                        transAlpha = transition_alpha(transitionBaseUv);
                    #endif

                    float4 uvMask = float4(0, 0, 1, 1);
                    texcoord += move_transition_filter(uvMask, transAlpha);
                #endif

                color = ApplyBlur(texcoord) * color;
                ApplyGradientColor(color, effectsUv);
                
                #if RECTANGLE || CIRCLE || PENTAGON || TRIANGLE || HEXAGON || CHAMFERBOX || PARALLELOGRAM || NSTAR_POLYGON || HEART || BLOBBYCROSS || SQUIRCLE || NTRIANGLE_ROUNDED
                    float sdfData;
                    float pixelScale;
                    ComputeSdfData(IN.shapeData, _FalloffDistance, sdfData, pixelScale);

                    #if !OUTLINED && !STROKE && !OUTLINED_STROKE
                        float sdf = sampleSdf(sdfData, pixelScale);
                        color.a *= sdf;
                    #endif

                    #if STROKE
                        float sdf = sampleSdfStrip(sdfData, _StrokeWidth + _OutlineWidth, pixelScale);
                        color.a *= sdf;
                    #endif
                    
                    ApplyOutlinedSdf(color, IN.shapeData, sdfData, pixelScale);
                     
                    #if OUTLINED_STROKE
                        float alpha = sampleSdfStrip(sdfData, _OutlineWidth + _StrokeWidth, pixelScale);
                        float lerpFac = sampleSdfStrip(sdfData + _OutlineWidth, _StrokeWidth + _FalloffDistance, pixelScale);
                        lerpFac = clamp(lerpFac, 0, 1);
                        color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a * color.a, color.a, lerpFac));
                        color.a *= alpha;
                    #endif
                #endif

                #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
                    color = apply_transition_filter(color, transAlpha, transitionFilterUv);
                #endif

                #if !RECTANGLE && !CIRCLE && !PENTAGON && !TRIANGLE && !HEXAGON && !CHAMFERBOX && !PARALLELOGRAM && !NSTAR_POLYGON && !HEART && !BLOBBYCROSS && !SQUIRCLE && !NTRIANGLE_ROUNDED
                    #if OUTLINED || STROKE || OUTLINED_STROKE
                        float width = _OutlineWidth;
                        #if STROKE || OUTLINED_STROKE
                            width += _StrokeWidth;
                        #endif
                        if (width > 0)
                        {
                            float2 d = _MainTex_TexelSize.xy * width;
                            half a00 = (tex2D(_MainTex, texcoord + float2(-d.x, -d.y)) + _TextureSampleAdd).a;
                            half a01 = (tex2D(_MainTex, texcoord + float2(-d.x, 0.0)) + _TextureSampleAdd).a;
                            half a02 = (tex2D(_MainTex, texcoord + float2(-d.x, +d.y)) + _TextureSampleAdd).a;
                            half a10 = (tex2D(_MainTex, texcoord + float2(0.0, -d.y)) + _TextureSampleAdd).a;
                            half a12 = (tex2D(_MainTex, texcoord + float2(0.0, +d.y)) + _TextureSampleAdd).a;
                            half a20 = (tex2D(_MainTex, texcoord + float2(+d.x, -d.y)) + _TextureSampleAdd).a;
                            half a21 = (tex2D(_MainTex, texcoord + float2(+d.x, 0.0)) + _TextureSampleAdd).a;
                            half a22 = (tex2D(_MainTex, texcoord + float2(+d.x, +d.y)) + _TextureSampleAdd).a;
                            half sobel_h = a00 * -1.0 + a01 * -2.0 + a02 * -1.0 + a20 * 1.0 + a21 * 2.0 + a22 * 1.0;
                            half sobel_v = a00 * -1.0 + a10 * -2.0 + a20 * -1.0 + a02 * 1.0 + a12 * 2.0 + a22 * 1.0;
                            half sobel = sqrt(sobel_h * sobel_h + sobel_v * sobel_v);
                            sobel = saturate(sobel);
                            #if STROKE
                                color.a = sobel * _OutlineColor.a * IN.color.a;
                            #else
                                color.rgb = lerp(color.rgb, _OutlineColor.rgb, sobel);
                                color.a = max(color.a, sobel * _OutlineColor.a * IN.color.a);
                            #endif
                        }
                        #if STROKE
                        else { color.a = 0; }
                        #endif
                    #endif
                #endif

                return fixed4(color);
            }
            ENDCG
        }
    }
    CustomEditor "ReunionMovement.UI.ImageExtensions.Editor.ImageShaderGUI"
}
