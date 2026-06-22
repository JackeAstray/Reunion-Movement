Shader "ReunionMovement/UI/ImageEx"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" { }
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TextureSize ("Texture Size", Vector) = (1, 1, 1, 1)
        
        _DrawShape ("绘制形状", int) = 2
        
        _StrokeWidth ("线条宽度", float) = 0
        _StrokeFill ("描边填充比例", Range(0,1)) = 0
        _FalloffDistance ("衰减距离", float) = 0.5
        _PixelWorldScale ("像素与世界单位之间的缩放比例", Range(0.01, 5)) = 1
        _ShapeRotation ("形状旋转", float) = 0
        _ConstrainRotation("约束旋转", int) = 0
        _FlipHorizontal ("水平翻转", int) = 0
        _FlipVertical ("垂直翻转", int) = 0
        
        _RectangleCornerRadius ("矩形四个角的圆角半径", Vector) = (0, 0, 0, 0)
        _CircleRadius ("圆半径", float) = 0
        _CircleFitRadius ("拟合圆半径", float) = 0
        _PentagonCornerRadius ("定义五边形的四个角的圆角半径", Vector) = (0, 0, 0, 0)
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
        _GradientType ("渐变的类型", int) = 0
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
        // 虚线开关、自定义时间
        _EnableDashedOutline ("启用虚线轮廓", int) = 0 
        _CustomTime ("自定义时间值", Float) = 0

        _BlurType ("模糊类型", int) = 0
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
        [Toggle] _TransitionUseUv0 ("过渡使用精灵 UV0", Float) = 1

        // 阴影相关属性（用于控制投影/镜像阴影的颜色、衰减、采样与镜像行为）
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

        // -------------------- 色调滤镜（TONE） --------------------
        _ToneFilter ("色调滤镜", int) = 0
        _ToneIntensity ("色调滤镜强度", Range(0, 1)) = 1

        // -------------------- 独立颜色滤镜（COLOR FILTER） --------------------
        _ColorFilter ("颜色滤镜模式", int) = 0
        _ColorValue ("颜色滤镜值", Color) = (1, 1, 1, 1)
        _ColorIntensity ("颜色滤镜强度", Range(0, 1)) = 1
        _ColorGlow ("颜色滤镜发光", int) = 0

        // -------------------- 边缘效果（EDGE） --------------------
        _EdgeMode ("边缘效果模式", int) = 0
        _EdgeWidth ("边缘宽度", Range(0, 1)) = 0.5
        _EdgeColorFilter ("边缘颜色滤镜", int) = 4
        [HDR] _EdgeColor ("边缘颜色", Color) = (1, 1, 1, 1)
        _EdgeColorGlow ("边缘颜色发光", int) = 0
        _EdgeShinyRate ("边缘高光位置", Range(0, 1)) = 0.5
        _EdgeShinyWidth ("边缘高光宽度", Range(0, 1)) = 0.5
        _EdgeShinyAutoPlaySpeed ("边缘高光自动速度", Range(-5, 5)) = 1

        // -------------------- 采样增强（SAMPLING） --------------------
        _SamplingMode ("采样模式", int) = 0
        _SamplingIntensity ("采样强度", Range(0, 1)) = 0.5

        // -------------------- 目标模式（TARGET） --------------------
        _TargetMode ("目标模式", int) = 0
        _TargetColor ("目标颜色", Color) = (1, 1, 1, 1)
        _TargetRange ("目标范围", Range(0, 1)) = 0.1
        _TargetSoftness ("目标柔和度", Range(0, 1)) = 0.5

        // -------------------- 图案区域（PATTERN AREA） --------------------
        _PatternArea ("图案区域", int) = 0

        _StencilComp ("模板比较", Float) = 8
        _Stencil ("模板ID", Float) = 0
        _StencilOp ("模板操作", Float) = 0
        _StencilWriteMask ("模板写入掩码", Float) = 255
        _StencilReadMask ("模板读取掩码", Float) = 255
        
        _ColorMask ("颜色掩码", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("使用Alpha剪辑", Float) = 0
    }
    
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            Name "Default"
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../Base/2D_SDF.cginc"
            #include "../../Base/Common.cginc"
            
            // ============================================================
            // ReunionMovement 通用模块 (Common/)
            // 渐变 / 过渡 / 模糊 / 描边 / 形状 / 阴影
            // ============================================================
            #include "../../Common/RM_SDFShapes.cginc"
            #include "../../Common/RM_Gradient.cginc"
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            #pragma multi_compile_local _ CIRCLE TRIANGLE RECTANGLE PENTAGON HEXAGON CHAMFERBOX PARALLELOGRAM NSTAR_POLYGON HEART BLOBBYCROSS SQUIRCLE NTRIANGLE_ROUNDED

            #pragma multi_compile_local _ STROKE OUTLINED OUTLINED_STROKE
            #pragma shader_feature_local _ GRADIENT_LINEAR GRADIENT_RADIAL GRADIENT_CORNER
            #pragma shader_feature_local _ BLUR_FAST BLUR_MEDIUM BLUR_DETAIL
            #pragma shader_feature_local _ TRANSITION_FADE TRANSITION_CUTOFF TRANSITION_DISSOLVE TRANSITION_SHINY TRANSITION_MASK TRANSITION_MELT TRANSITION_BURN TRANSITION_PATTERN TRANSITION_BLAZE
            #pragma shader_feature_local _ DASHED_OUTLINE_STATIC
            #pragma shader_feature_local _ TRANSITION_CLAMP_STATIC
            #pragma shader_feature_local _ TRANSITION_UV_EFFECT_STATIC
            #pragma shader_feature_local _ TONE_GRAYSCALE TONE_SEPIA TONE_NEGATIVE TONE_RETRO TONE_POSTERIZE
            #pragma shader_feature_local _ COLOR_FILTER
            #pragma shader_feature_local _ EDGE_PLAIN EDGE_SHINY
            #pragma shader_feature_local _ SAMPLING_PIXELATION SAMPLING_RGB_SHIFT SAMPLING_EDGE_LUMINANCE SAMPLING_EDGE_ALPHA
            #pragma shader_feature_local _ TARGET_HUE TARGET_LUMINANCE

            struct appdata_t
            {
                float4 vertex: POSITION;
                float4 color: COLOR;
                float2 texcoord: TEXCOORD0;
                float2 uv1: TEXCOORD1;
                float2 size: TEXCOORD2;
                float4 tangent: TANGENT; // tangent.w 用于阴影顶点标记
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex: SV_POSITION;
                fixed4 color: COLOR;
                float2 texcoord: TEXCOORD0;
                float4 shapeData: TEXCOORD1;
                float2 effectsUv: TEXCOORD2;
                float4 worldPosition : TEXCOORD3;
                fixed isShadowVertexFlag : TEXCOORD4; // tangent.w 阴影顶点标记
                
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _TextureSampleAdd;
            fixed4 _TextureSize;
            float4 _ClipRect;
            half _PixelWorldScale;
            half _ShapeRotation;
            half _ConstrainRotation;
            half _FlipHorizontal;
            half _FlipVertical;

            // 注：形状参数 (RectangleCornerRadius, CircleRadius 等) 已在 RM_SDFShapes.cginc 中声明
            // 注：渐变参数 (GradientColor0-7 等) 已在 RM_Gradient.cginc 中声明
            
            // ============================================================
            // 别名映射：将旧函数名映射到 Common 模块的 RM_* 版本
            // ============================================================
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
            #define ComputeSdfData(IN, sdf, ps) RM_ComputeSdfData(IN.shapeData, _FalloffDistance, sdf, ps)
            #define generateDashedEffect(IN, t, ar, st) RM_GenerateDashedEffect(IN.shapeData, t, ar, st)
            #define ApplyGradientColor(c, uv)   RM_ApplyGradientColor(c, uv)
            #define ApplyBlur(uv)               RM_ApplyBlur(uv)
            #define ApplyOutlinedSdf(c, IN, sdf, ps) RM_ApplyOutlinedSdf(c, IN.shapeData, sdf, ps)
            #define apply_transition_filter(c, a, uv, ef)  RM_ApplyTransitionFilter(c, a, uv, ef)
            #define transition_alpha(uv)         RM_TransitionAlpha(uv)
            #define move_transition_filter(m, a) RM_MoveTransitionFilter(m, a)
            #define transition_rate()            RM_TransitionRate()
            #define apply_color_filter(m, c, f, i, g) RM_ApplyColorFilter(m, c, f, i, g)

            // -------------------- 过渡（TRANSITION） --------------------
            #include "../../Common/RM_Transition.cginc"
            // -------------------- 模糊（BLUR） --------------------
            #include "../../Common/RM_Blur.cginc"
            // -------------------- 描边（OUTLINE） --------------------
            #include "../../Common/RM_Outline.cginc"
            // -------------------- 阴影（SHADOW） --------------------
            #include "../../Common/RM_Shadow.cginc"
            // -------------------- 色调滤镜（TONE） --------------------
            #include "../../Common/RM_ToneFilter.cginc"
            // -------------------- 独立颜色滤镜（COLOR FILTER） --------------------
            #include "../../Common/RM_ColorFilter.cginc"
            // -------------------- 边缘效果（EDGE） --------------------
            #include "../../Common/RM_Edge.cginc"
            // -------------------- 采样增强（SAMPLING） --------------------
            #include "../../Common/RM_Sampling.cginc"
            // -------------------- 目标模式（TARGET） --------------------
            #include "../../Common/RM_Target.cginc"

            //顶点着色器
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.effectsUv = v.uv1;
                
                // 将翻转应用到纹理 UV 与 effects UV，确保采样与过渡在水平/垂直翻转时表现正确
                // _FlipHorizontal/_FlipVertical 由 ImageEx 组件以 0/1 的形式设置
                OUT.texcoord.x = lerp(OUT.texcoord.x, 1 - OUT.texcoord.x, _FlipHorizontal);
                OUT.texcoord.y = lerp(OUT.texcoord.y, 1 - OUT.texcoord.y, _FlipVertical);
                OUT.effectsUv.x = lerp(OUT.effectsUv.x, 1 - OUT.effectsUv.x, _FlipHorizontal);
                OUT.effectsUv.y = lerp(OUT.effectsUv.y, 1 - OUT.effectsUv.y, _FlipVertical);
                
                float2 size = float2(v.size.x + _FalloffDistance, v.size.y + _FalloffDistance);
                float shapeRotation = radians(_ShapeRotation);
                size = _ConstrainRotation > 0.0 && frac(abs(shapeRotation) / 3.14159) > 0.1? float2(size.y, size.x) : size;
                
                float2 shapeUv = _ConstrainRotation > 0 ? v.uv1 : v.uv1 * size;
                shapeUv = rotateUV(shapeUv, shapeRotation, _ConstrainRotation > 0? float2(0.5, 0.5) : size * 0.5);
                shapeUv*= _ConstrainRotation > 0.0? size : 1.0;
                
                // 在形状数据上应用翻转（shapeUv 在像素空间中），确保程序化形状的 SDF 计算也考虑翻转
                // _FlipHorizontal/_FlipVertical 由 ImageEx 组件以 0/1 的形式设置
                shapeUv.x = lerp(shapeUv.x, abs(size.x - shapeUv.x), _FlipHorizontal);
                shapeUv.y = lerp(shapeUv.y, abs(size.y - shapeUv.y), _FlipVertical);
                
                OUT.shapeData = float4(shapeUv.x, shapeUv.y, size.x, size.y);
                
                // 对形状数据应用翻转（shapeUv 以像素空间表示），以确保程序化形状的 SDF 考虑翻转
                OUT.shapeData.x = lerp(OUT.shapeData.x, OUT.shapeData.z - OUT.shapeData.x, _FlipHorizontal);
                OUT.shapeData.y = lerp(OUT.shapeData.y, OUT.shapeData.w - OUT.shapeData.y, _FlipVertical);
                
                #ifdef UNITY_HALF_TEXEL_OFFSET
                    OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1.0, 1.0);
                #endif
                OUT.isShadowVertexFlag = v.tangent.w;
                OUT.color = v.color * _Color;

                return OUT;
            }
            
            //片元着色器
            fixed4 frag(v2f IN): SV_Target
            {
                // Prepare UVs/transition info before deciding shadow or main fragment so shadow can follow transitions.
                // 在决定使用阴影分支或主图分支之前，准备 UV 与过渡信息，以便阴影能跟随过渡状态
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

                // 过渡逻辑（计算 transAlpha，并为 Melt/Burn 模式允许移动 UV）
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

                    // Move UVs for Melt/Burn
                    float4 uvMask = float4(0, 0, 1, 1); // Simplified mask for full rect
                    texcoord += move_transition_filter(uvMask, transAlpha);
                #endif

                // 检测是否为阴影顶点，并使用单独逻辑渲染阴影（仍包含过渡处理）
                // 使用 tangent.w 标记检测阴影顶点，避免黑色 Tint 导致误判
                if (IN.isShadowVertexFlag > 0.5)
                {
                    return RM_RenderShadow(IN.shapeData, _FalloffDistance, _StrokeWidth, _OutlineWidth, texcoord, transAlpha, transitionFilterUv, IN.color.a);
                }

                // 统一采样入口：模糊（BLUR_*）或 Phase2 采样模式（Pixelation/RGB Shift/Edge）
                #if SAMPLING_PIXELATION || SAMPLING_RGB_SHIFT || SAMPLING_EDGE_LUMINANCE || SAMPLING_EDGE_ALPHA
                    color = RM_ApplySampling(texcoord) * color;
                #else
                    color = ApplyBlur(texcoord) * color;
                #endif

                // 继续主片元路径：先对主纹理进行可选模糊采样（ApplyBlur），然后按功能模块（渐变、SDF、描边、过渡）依次处理颜色与 alpha
                // 保留原始的基础采样颜色
                fixed4 baseSample = (tex2D(_MainTex, texcoord) + _TextureSampleAdd) * IN.color;

                // 计算边缘因子（在渐变之前，基于原始纹理 alpha 做 12 方向邻域检测）
                float edgeFactor = 0;
                #if EDGE_PLAIN || EDGE_SHINY
                    edgeFactor = RM_ComputeEdgeFactor(texcoord, _EdgeWidth);
                #endif

                ApplyGradientColor(color, effectsUv);

                // 应用色调滤镜（Tone Filter）：灰度化 / 怀旧 / 负片 / 复古 / 色调分离
                color = RM_ApplyToneFilter(color);
                
                #if RECTANGLE || CIRCLE || PENTAGON || TRIANGLE || HEXAGON || CHAMFERBOX || PARALLELOGRAM || NSTAR_POLYGON || HEART || BLOBBYCROSS || SQUIRCLE || NTRIANGLE_ROUNDED
                    float sdfData;
                    float pixelScale;
                    ComputeSdfData(IN, sdfData, pixelScale);

                    #if !OUTLINED && !STROKE && !OUTLINED_STROKE
                        float sdf = sampleSdf(sdfData, pixelScale);
                        color.a *= sdf;
                    #endif

                    #if STROKE
                        float sdf = sampleSdfStrip(sdfData, _StrokeWidth + _OutlineWidth, pixelScale);
                        color.a *= sdf;
                    #endif
                    
                    ApplyOutlinedSdf(color, IN, sdfData, pixelScale);
                     
                    #if OUTLINED_STROKE
                        float alpha = sampleSdfStrip(sdfData, _OutlineWidth + _StrokeWidth, pixelScale);
                        float lerpFac = sampleSdfStrip(sdfData + _OutlineWidth, _StrokeWidth + _FalloffDistance, pixelScale);
                        lerpFac = clamp(lerpFac, 0, 1);
                        color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a * color.a, color.a, lerpFac));
                        color.a *= alpha;
                    #endif
                #endif

                // 应用过渡过滤器
                #if TRANSITION_FADE || TRANSITION_CUTOFF || TRANSITION_DISSOLVE || TRANSITION_SHINY || TRANSITION_MASK || TRANSITION_MELT || TRANSITION_BURN || TRANSITION_PATTERN || TRANSITION_BLAZE
                    // transAlpha 和 transitionFilterUv 已在前面计算，可在此处使用
                    color = apply_transition_filter(color, transAlpha, transitionFilterUv, edgeFactor);
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

                            // 原有描边逻辑
                            #if STROKE
                                color.a = sobel * _OutlineColor.a * IN.color.a;
                            #else
                                color.rgb = lerp(color.rgb, _OutlineColor.rgb, sobel);
                                color.a = max(color.a, sobel * _OutlineColor.a * IN.color.a);
                            #endif
                        }
                        #if STROKE
                        else
                        {
                            color.a = 0;
                        }
                        #endif
                    #endif
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                // 应用独立颜色滤镜（Color Filter）：乘法 / 加法 / 减法 / 替换 / HSV 偏移 / 对比度
                #if COLOR_FILTER
                    color = RM_ApplyStandaloneColorFilter(color);
                #endif

                // 应用边缘效果（Edge Mode）：普通边缘发光 / 旋转高光边缘
                #if EDGE_PLAIN || EDGE_SHINY
                    color = RM_ApplyEdge(color, edgeFactor, effectsUv);
                #endif

                // 应用目标模式（Target Mode）：基于色相/亮度的目标颜色过滤
                #if TARGET_HUE || TARGET_LUMINANCE
                    half targetRate = RM_GetTargetRate(baseSample.rgb);
                    color = RM_ApplyTarget(color, baseSample, targetRate);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return fixed4(color);
            }
            ENDCG
        }
    }
    CustomEditor "ReunionMovement.UI.ImageExtensions.Editor.ImageShaderGUI"
}