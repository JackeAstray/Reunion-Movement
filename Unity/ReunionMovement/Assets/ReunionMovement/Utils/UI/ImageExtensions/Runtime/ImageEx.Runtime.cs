using System;
using ReunionMovement.Common;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part: Runtime (same class, no behavior/serialization change)
    /// </summary>
    public partial class ImageEx
    {
        #region 私有变量

        private Material dynamicMaterial;

        // ===== Shared 模式串扰诊断 =====
        // 同一共享材质被 keywordMask 不同的多个 ImageEx 使用时，渲染由最后写入者决定，
        // 形状/特效会互相覆盖。以下静态注册表用于检测并告警一次（不改变渲染行为）。
        private static readonly System.Collections.Generic.Dictionary<Material, int> s_sharedMatLastMask
            = new System.Collections.Generic.Dictionary<Material, int>(8);
        private static readonly System.Collections.Generic.HashSet<Material> s_sharedMatWarned
            = new System.Collections.Generic.HashSet<Material>();

        /// <summary>Shared 模式是否已回读共享材质（避免每帧回读）</summary>
        private bool m_sharedValuesLoaded;
        /// <summary>上次回读的共享材质引用（引用变化时重新回读）</summary>
        private Material m_loadedSharedMaterial;

        private Material DynamicMaterial
        {
            get
            {
                if (dynamicMaterial == null)
                {
                    Shader shader = Shader.Find(shaderName);
                    if (shader == null)
                    {
                        Log.Warning("着色器 '{0}' 未找到，已回退到 'Sprites/Default'。请确认项目中存在对应着色器。", shaderName);
                        shader = Shader.Find("Sprites/Default");
                    }

                    if (shader != null)
                    {
                        dynamicMaterial = new Material(shader);
                        dynamicMaterial.name += " [Dynamic]";
                    }
                    else
                    {
                        // 最后手段：创建一个基础材质以避免空引用错误
                        dynamicMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
                        dynamicMaterial.name += " [Dynamic - Fallback]";
                    }
                }

                return dynamicMaterial;
            }
        }

#if UNITY_EDITOR
        private bool parseAgainOnValidate;
#endif

        private Sprite ActiveSprite
        {
            get
            {
                Sprite overrideSprite1 = overrideSprite;
                return overrideSprite1 != null ? overrideSprite1 : sprite;
            }
        }

        #endregion

#if UNITY_EDITOR
        public void UpdateSerializedValuesFromSharedMaterial()
        {
            if (m_Material && MaterialMode == MaterialMode.Shared)
            {
                InitValuesFromSharedMaterial();
                base.SetMaterialDirty();
            }
        }

        protected override void OnValidate()
        {
            InitializeComponents();
            // 编辑模式下也立即补齐 Canvas 通道（尤其 Tangent），避免场景中 ImageEx 透明
            FixAdditionalShaderChannelsInCanvas();
            if (parseAgainOnValidate)
            {
                InitValuesFromSharedMaterial();
                parseAgainOnValidate = false;
            }

            DrawShape = drawShape;

            StrokeWidth = strokeWidth;
            OutlineWidth = outlineWidth;
            OutlineColor = outlineColor;
            FalloffDistance = falloffDistance;
            ConstrainRotation = constrainRotation;
            ShapeRotation = shapeRotation;
            FlipHorizontal = flipHorizontal;
            FlipVertical = flipVertical;
            AlphaThreshold = alphaThreshold;
            CameraTexture = cameraTexture;

            triangle.OnValidate();
            circle.OnValidate();
            rectangle.OnValidate();
            pentagon.OnValidate();
            hexagon.OnValidate();
            chamferBox.OnValidate();
            quadrilateral.OnValidate();
            nStarPolygon.OnValidate();
            heart.OnValidate();
            blobbyCross.OnValidate();
            squircle.OnValidate();
            nTriangleRounded.OnValidate();

            gradientEffect.OnValidate();

            Blur = blurType;
            BlurIntensity = blurIntensity;

            Transition = transitionMode;
            TransitionTexture = transitionTexture;
            TransitionTexScale = transitionTexScale;
            TransitionTexOffset = transitionTexOffset;
            TransitionTexRotation = transitionTexRotation;
            TransitionKeepAspectRatio = transitionKeepAspectRatio;
            TransitionRate = transitionRate;
            TransitionColor = transitionColor;
            TransitionWidth = transitionWidth;
            TransitionSoftness = transitionSoftness;
            TransitionReverse = transitionReverse;
            TransitionSpeed = transitionSpeed;
            TransitionPatternReverse = transitionPatternReverse;
            TransitionAutoPlaySpeed = transitionAutoPlaySpeed;
            TransitionColorFilter = transitionColorFilter;
            TransitionColorGlow = transitionColorGlow;
            TransitionGradient = transitionGradient;
            TransitionRange = transitionRange;
            TransitionClamp = transitionClamp;
            TransitionTexClampPadding = transitionTexClampPadding;
            TransitionUseUv0 = transitionUseUv0;

            // Phase 1
            Tone = m_ToneFilter;
            ToneIntensity = m_ToneIntensity;
            ColorFilterMode = m_ColorFilterMode;
            ColorValue = m_ColorValue;
            ColorIntensity = m_ColorIntensity;
            ColorGlow = m_ColorGlow;
            Edge = m_EdgeMode;
            EdgeWidth = m_EdgeWidth;
            EdgeColorFilterMode = m_EdgeColorFilterMode;
            EdgeColor = m_EdgeColor;
            EdgeColorGlow = m_EdgeColorGlow;
            EdgeShinyRate = m_EdgeShinyRate;
            EdgeShinyWidth = m_EdgeShinyWidth;
            EdgeShinyAutoPlaySpeed = m_EdgeShinyAutoPlaySpeed;

            // Phase 2
            Sampling = m_SamplingMode;
            SamplingIntensity = m_SamplingIntensity;
            Target = m_TargetMode;
            TargetColor = m_TargetColor;
            TargetRange = m_TargetRange;
            TargetSoftness = m_TargetSoftness;
            TransitionPatternArea = m_PatternArea;

            // Phase 3
            Detail = m_DetailMode;
            DetailTex = m_DetailTex;
            DetailTexScale = m_DetailTexScale;
            DetailTexOffset = m_DetailTexOffset;
            DetailTexSpeed = m_DetailTexSpeed;
            DetailIntensity = m_DetailIntensity;
            DetailThreshold = m_DetailThreshold;
            DetailColor = m_DetailColor;
            EnableGradientTex = m_EnableGradientTex;
            GradientTex = m_GradientTex;
            GradientOffset = m_GradientOffset;
            GradientScale = m_GradientScale;
            Blend = m_BlendType;

            ShadowScale = shadowScale;

            base.OnValidate();
            base.SetMaterialDirty();
        }
#endif
        /// <summary>
        /// 刷新过渡渐变纹理
        /// </summary>
        public void RefreshTransitionGradient()
        {
            if (transitionGradientValue == null)
            {
                transitionGradientValue = new Gradient();
            }

            int width = 256;
            int height = 1;

            if (transitionGradient == null || transitionGradient.width != width || transitionGradient.height != height)
            {
                // 替换前先销毁旧的运行时生成纹理（用户资产不销毁），避免尺寸变化时累积泄漏
                if (transitionGradient != null && transitionGradientIsRuntime)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(transitionGradient);
                    else
                        UnityEngine.Object.DestroyImmediate(transitionGradient);
                }

                transitionGradient = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Transition Gradient",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                transitionGradientIsRuntime = true;
            }

            Texture2D tex = transitionGradient as Texture2D;
            if (tex != null)
            {
                for (int i = 0; i < width; i++)
                {
                    tex.SetPixel(i, 0, transitionGradientValue.Evaluate((float)i / (width - 1)));
                }
                tex.Apply();
            }

            SetMaterialDirty();
        }

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            circle.Init(m_Material, material, rectTransform);
            triangle.Init(m_Material, material, rectTransform);
            rectangle.Init(m_Material, material, rectTransform);
            pentagon.Init(m_Material, material, rectTransform);
            hexagon.Init(m_Material, material, rectTransform);
            chamferBox.Init(m_Material, material, rectTransform);
            quadrilateral.Init(m_Material, material, rectTransform);
            nStarPolygon.Init(m_Material, material, rectTransform);
            heart.Init(m_Material, material, rectTransform);
            blobbyCross.Init(m_Material, material, rectTransform);
            squircle.Init(m_Material, material, rectTransform);
            nTriangleRounded.Init(m_Material, material, rectTransform);
            gradientEffect.Init(m_Material, material, rectTransform);
        }

        /// <summary>
        /// 修复画布中的附加着色通道。
        /// ImageEx 着色器需要：TexCoord1(effectsUv) / TexCoord2(size) / Tangent(阴影顶点标记 tangent.w)。
        /// 若 Canvas 的 Additional Shader Channels 未包含 Tangent，顶点 tangent 会是垃圾值，
        /// 导致 isShadowVertexFlag 误判、主图被当成阴影顶点渲染（表现为透明/异常）。
        /// </summary>
        void FixAdditionalShaderChannelsInCanvas()
        {
            Canvas c = canvas;
            if (c == null) c = GetComponentInParent<Canvas>();
            if (c == null) return;

            AdditionalCanvasShaderChannels channels = c.additionalShaderChannels;
            AdditionalCanvasShaderChannels needed = channels
                | AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.TexCoord2
                | AdditionalCanvasShaderChannels.Tangent;
            if (channels != needed)
                c.additionalShaderChannels = needed;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            InitializeComponents();
            base.Reset();
        }
#else
        void Reset() {
            InitializeComponents();
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            InitializeComponents();
            FixAdditionalShaderChannelsInCanvas();
            if (m_Material && MaterialMode == MaterialMode.Shared)
            {
                InitValuesFromSharedMaterial();
            }
            // 幂等防护：Init() 是 public 可被外部重复调用，重复 += 会使每个 setter 触发 2+ 次脏标记
            if (!m_listeningToComponents)
            {
                ListenToComponentChanges(true);
                m_listeningToComponents = true;
            }
            base.SetAllDirty();
        }

        protected override void OnDestroy()
        {
            if (m_listeningToComponents)
            {
                ListenToComponentChanges(false);
                m_listeningToComponents = false;
            }

            // 销毁运行时创建的动态材质，避免长时间运行/切场景时材质泄漏
            if (dynamicMaterial != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(dynamicMaterial);
                else
                    UnityEngine.Object.DestroyImmediate(dynamicMaterial);
                dynamicMaterial = null;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// 监听组件更改
        /// </summary>
        /// <param name="toggle"></param>
        protected void ListenToComponentChanges(bool toggle)
        {
            if (toggle)
            {
                circle.onComponentSettingsChanged += OnComponentSettingsChanged;
                triangle.onComponentSettingsChanged += OnComponentSettingsChanged;
                rectangle.onComponentSettingsChanged += OnComponentSettingsChanged;
                pentagon.onComponentSettingsChanged += OnComponentSettingsChanged;
                hexagon.onComponentSettingsChanged += OnComponentSettingsChanged;
                chamferBox.onComponentSettingsChanged += OnComponentSettingsChanged;
                quadrilateral.onComponentSettingsChanged += OnComponentSettingsChanged;
                nStarPolygon.onComponentSettingsChanged += OnComponentSettingsChanged;
                heart.onComponentSettingsChanged += OnComponentSettingsChanged;
                blobbyCross.onComponentSettingsChanged += OnComponentSettingsChanged;
                squircle.onComponentSettingsChanged += OnComponentSettingsChanged;
                nTriangleRounded.onComponentSettingsChanged += OnComponentSettingsChanged;
                gradientEffect.onComponentSettingsChanged += OnComponentSettingsChanged;
            }
            else
            {
                circle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                triangle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                rectangle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                pentagon.onComponentSettingsChanged -= OnComponentSettingsChanged;
                hexagon.onComponentSettingsChanged -= OnComponentSettingsChanged;
                chamferBox.onComponentSettingsChanged -= OnComponentSettingsChanged;
                quadrilateral.onComponentSettingsChanged -= OnComponentSettingsChanged;
                nStarPolygon.onComponentSettingsChanged -= OnComponentSettingsChanged;
                heart.onComponentSettingsChanged -= OnComponentSettingsChanged;
                blobbyCross.onComponentSettingsChanged -= OnComponentSettingsChanged;
                squircle.onComponentSettingsChanged -= OnComponentSettingsChanged;
                nTriangleRounded.onComponentSettingsChanged -= OnComponentSettingsChanged;
                gradientEffect.onComponentSettingsChanged -= OnComponentSettingsChanged;
            }
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            FixAdditionalShaderChannelsInCanvas();
        }

        /// <summary>
        /// 当组件设置更改时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnComponentSettingsChanged(object sender, EventArgs e)
        {
            base.SetMaterialDirty();
        }


        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            circle.UpdateCircleRadius(rectTransform);
            heart.UpdateCircleRadius(rectTransform);
            base.SetMaterialDirty();
        }

        /// <summary>8 方向轮廓阴影的方向常量（避免每次网格重建分配数组）</summary>
        private static readonly Vector2[] Outline8Directions = new Vector2[]
        {
            new Vector2(1, 0), new Vector2(0.707f, 0.707f), new Vector2(0, 1),
            new Vector2(-0.707f, 0.707f), new Vector2(-1, 0),
            new Vector2(-0.707f, -0.707f), new Vector2(0, -1),
            new Vector2(0.707f, -0.707f)
        };

        /// <summary>
        /// 生成网格
        /// </summary>
        /// <param name="vh"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            switch (type)
            {
                case Type.Simple:
                case Type.Sliced:
                    Vector2 effectiveShadowOffset = shadowOffsetLocal;
                    if (flipHorizontal) effectiveShadowOffset.x = -effectiveShadowOffset.x;
                    if (flipVertical) effectiveShadowOffset.y = -effectiveShadowOffset.y;

                    // 先绘制主图（无阴影）
                    ImageHelper.GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                        color, falloffDistance, false, Vector2.zero);

                    // 根据阴影模式追加阴影四边形
                    if (appendShadow)
                    {
                        float dist = effectiveShadowOffset.magnitude;
                        if (dist < 0.01f) dist = 8f; // 最小偏移

                        switch (shadowMode)
                        {
                            case ShadowMode.Shadow:
                                // 单层投影
                                ImageHelper.AddShadowQuad(vh, preserveAspect, canvas, rectTransform,
                                    ActiveSprite, color, effectiveShadowOffset, shadowScale);
                                break;

                            case ShadowMode.Shadow3:
                                // 3层迭代投影，偏移递增、alpha 递减
                                for (int i = 0; i < 3; i++)
                                {
                                    float iterFade = Mathf.Pow(0.75f, i); // 1.0, 0.75, 0.5625
                                    Vector2 iterOffset = effectiveShadowOffset * (i + 1);
                                    ImageHelper.AddShadowQuad(vh, preserveAspect, canvas, rectTransform,
                                        ActiveSprite, color, iterOffset, shadowScale, iterFade);
                                }
                                break;

                            case ShadowMode.Mirror:
                                // 镜像阴影：使用单层但依赖 shader 镜像逻辑
                                ImageHelper.AddShadowQuad(vh, preserveAspect, canvas, rectTransform,
                                    ActiveSprite, color, effectiveShadowOffset, shadowScale);
                                break;

                            case ShadowMode.Outline8:
                                // 8方向轮廓阴影
                                Vector2[] dirs = Outline8Directions;
                                for (int i = 0; i < 8; i++)
                                {
                                    Vector2 outlineOffset = dirs[i] * dist;
                                    ImageHelper.AddShadowQuad(vh, preserveAspect, canvas, rectTransform,
                                        ActiveSprite, color, outlineOffset, shadowScale, 1f);
                                }
                                break;
                        }
                    }
                    break;
                case Type.Filled:
                    ImageHelper.GenerateFilledSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                        color, fillMethod, fillAmount, fillOrigin, fillClockwise, falloffDistance);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 关键字掩码缓存（Dynamic 模式性能优化）：掩码与材质未变化时跳过 ~60 次 DisableKeyword 调用
        private int appliedKeywordMask = -1;
        private Material appliedKeywordMaterial;

        /// <summary>
        /// 计算当前期望的关键字掩码。
        /// 必须覆盖 GetModifiedMaterial 中所有会被 Enable 的关键字来源；
        /// 掩码不变 ⇒ 关键字状态必然不变（可安全跳过 DisableAllMaterialKeywords）。
        /// </summary>
        private int ComputeKeywordMask()
        {
            int mask = (int)transitionMode;                                 // bits 0-3  TRANSITION_*
            mask |= ((int)blurType & 0x3) << 4;                             // bits 4-5  BLUR_*
            int strokeState;
            if (strokeWidth > 0 && outlineWidth > 0) strokeState = 1;       // OUTLINED_STROKE
            else if (strokeWidth > 0) strokeState = 2;                      // STROKE
            else if (outlineWidth > 0) strokeState = 3;                     // OUTLINED
            else strokeState = 0;
            mask |= strokeState << 6;                                       // bits 6-7
            mask |= ((int)DrawShape & 0xF) << 8;                            // bits 8-11 形状关键字
            mask |= ((int)m_ToneFilter & 0x7) << 12;                        // bits 12-14 TONE_*
            mask |= (m_ColorFilterMode != ColorMode.None ? 1 : 0) << 15;    // bit 15    COLOR_FILTER
            mask |= ((int)m_EdgeMode & 0x3) << 16;                          // bits 16-17 EDGE_*
            mask |= ((int)m_SamplingMode & 0x7) << 18;                      // bits 18-20 SAMPLING_*
            mask |= ((int)m_TargetMode & 0x3) << 21;                        // bits 21-22 TARGET_*
            mask |= (m_EnableGradientTex && m_GradientTex != null ? 1 : 0) << 23; // bit 23 GRADIENT_TEXTURE
            mask |= ((int)m_DetailMode & 0x7) << 24;                        // bits 24-26 DETAIL_*
            // 渐变效果关键字（gradientEffect.ModifyMaterial 负责 Enable/Disable，纳入掩码）
            int gradState = gradientEffect.Enabled ? ((int)gradientEffect.GradientType + 1) : 0;
            mask |= (gradState & 0x3) << 27;                                // bits 27-28 GRADIENT_LINEAR/CORNER/RADIAL
            return mask;
        }

        /// <summary>
        /// Shared 模式串扰诊断：同一共享材质被设置不同的多个 ImageEx 使用时告警一次。
        /// Shared 模式下每个实例都会 DisableAll 后写入自己的关键字/属性，最终状态由最后调用者决定。
        /// </summary>
        private static void DiagnoseSharedMaterialContention(Material shared, int keywordMask)
        {
            if (shared == null) return;
            if (s_sharedMatLastMask.TryGetValue(shared, out int prevMask))
            {
                if (prevMask != keywordMask && s_sharedMatWarned.Add(shared))
                {
                    Log.Warning("[ImageEx] 检测到共享材质被多个设置不同的 ImageEx 使用（关键字掩码不一致），"
                        + "渲染结果将由最后一个实例决定，形状/特效可能互相覆盖。"
                        + "建议为每个实例使用独立材质（Material Mode = Dynamic）或保持各实例设置一致。");
                }
                s_sharedMatLastMask[shared] = keywordMask;
            }
            else
            {
                s_sharedMatLastMask[shared] = keywordMask;
            }
        }

        /// <summary>
        /// 获取修改后的材质
        /// </summary>
        /// <param name="baseMaterial"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override Material GetModifiedMaterial(Material baseMaterial)
        {

            Material mat = base.GetModifiedMaterial(baseMaterial);

            // Shared 模式：仅在材质引用变化或首次启用时回读共享材质字段，
            // 避免每次 GetModifiedMaterial（动画期间可能每帧调用）都执行 ~100 次 Get* 回读
            if (m_Material && MaterialMode == MaterialMode.Shared && (!m_sharedValuesLoaded || m_loadedSharedMaterial != m_Material))
            {
                m_sharedValuesLoaded = true;
                m_loadedSharedMaterial = m_Material;
                InitValuesFromSharedMaterial();
            }

            // 关键字掩码缓存：掩码与材质未变化时跳过全部关键字调用
            // （DisableAll ~51 次 + ApplyActiveKeywords 的 Enable/Disable）。
            // 仅 Dynamic 模式生效——共享材质由多个实例共同写入，不做缓存以保持原语义。
            int keywordMask = ComputeKeywordMask();
            if (m_Material != null || appliedKeywordMaterial != mat || appliedKeywordMask != keywordMask)
            {
                // Shared 模式：检测多实例串扰（不同 keywordMask 共用同一材质）并告警一次
                DiagnoseSharedMaterialContention(m_Material, keywordMask);
                DisableAllMaterialKeywords(mat);
                ApplyActiveKeywords(mat);
                appliedKeywordMask = keywordMask;
                appliedKeywordMaterial = mat;
            }

            RectTransform rt = rectTransform;

            mat.SetFloat(outlineWidth_Sp, outlineWidth);
            mat.SetInt(enableDashedOutline_Sp, enableDashedOutline);
            mat.SetFloat(customTime_Sp, customTime);
            // 已移除 OUTLINED8 - 无操作


            mat.SetFloat(strokeWidth_Sp, strokeWidth);

            mat.SetColor(outlineColor_Sp, OutlineColor);
            mat.SetFloat(falloffDistance_Sp, FalloffDistance);

            mat.SetInt(blurType_Sp, (int)blurType);
            mat.SetFloat(blurIntensity_Sp, blurIntensity);

            mat.SetInt(transitionMode_Sp, (int)transitionMode);
            mat.SetTexture(transitionTex_Sp, transitionTexture);

            Vector2 scale = transitionTexScale;
            Vector2 offset = transitionTexOffset;

            // rect/纹理高度为 0 时跳过,避免除零产生 NaN 污染 transitionTex_ST
            if (transitionKeepAspectRatio && transitionTexture != null && rectTransform != null
                && rectTransform.rect.height > 0f && transitionTexture.height > 0)
            {
                float rectAspect = rectTransform.rect.width / rectTransform.rect.height;
                float texAspect = (float)transitionTexture.width / transitionTexture.height;

                if (texAspect > rectAspect)
                {
                    scale.y *= rectAspect / texAspect;
                    offset.y += (1 - rectAspect / texAspect) * 0.5f;
                }
                else
                {
                    scale.x *= texAspect / rectAspect;
                    offset.x += (1 - texAspect / rectAspect) * 0.5f;
                }
            }

            mat.SetVector(transitionTex_ST_Sp, new Vector4(scale.x, scale.y, offset.x, offset.y));
            mat.SetFloat(transitionTexRotation_Sp, transitionTexRotation);
            mat.SetFloat(transitionRate_Sp, transitionRate);
            mat.SetColor(transitionColor_Sp, transitionColor);
            mat.SetFloat(transitionWidth_Sp, transitionWidth);
            mat.SetFloat(transitionSoftness_Sp, transitionSoftness);
            mat.SetInt(transitionReverse_Sp, transitionReverse ? 1 : 0);
            mat.SetVector(transitionTexSpeed_Sp, transitionSpeed);
            mat.SetInt(transitionPatternReverse_Sp, transitionPatternReverse ? 1 : 0);
            mat.SetFloat(transitionAutoPlaySpeed_Sp, transitionAutoPlaySpeed);
            mat.SetInt(transitionColorFilter_Sp, (int)transitionColorFilter);
            mat.SetInt(transitionColorGlow_Sp, transitionColorGlow ? 1 : 0);
            mat.SetTexture(transitionGradientTex_Sp, transitionGradient);
            mat.SetVector(transitionRange_Sp, transitionRange);
            bool runtimeClamp = transitionClamp;
            if (transitionMode == TransitionMode.Shiny || transitionMode == TransitionMode.Mask || transitionMode == TransitionMode.Melt || transitionMode == TransitionMode.Burn)
                runtimeClamp = true;
            mat.SetFloat(transitionClamp_Sp, runtimeClamp ? 1 : 0);
            mat.SetFloat(transitionTexClampPadding_Sp, transitionTexClampPadding);
            mat.SetFloat(transitionUseUv0_Sp, transitionUseUv0 ? 1 : 0);

            // Shadow material params
            mat.SetColor(shadowColor_Sp, shadowColor);
            mat.SetFloat(shadowBlurIntensity_Sp, shadowBlurIntensity);
            mat.SetFloat(samplingWidth_Sp, samplingWidth);
            mat.SetFloat(samplingScale_Sp, samplingScale);
            mat.SetFloat(shadowScale_Sp, shadowScale);
            mat.SetFloat(allowOutOfBoundsShadow_Sp, allowOutOfBoundsShadow ? 1f : 0f);
            mat.SetInt(shadowMode_Sp, (int)shadowMode);
            mat.SetInt(shadowMirrorDirection_Sp, (int)shadowMirrorDirection);
            mat.SetFloat(shadowMirrorScale_Sp, shadowMirrorScale);
            mat.SetVector(shadowMirrorOffset_Sp, shadowMirrorOffset);
            mat.SetFloat(shadowMirrorShowSource_Sp, shadowMirrorShowSource ? 1f : 0f);
            mat.SetFloat(shadowMirrorTintMix_Sp, shadowMirrorTintMix);
            mat.SetInt(shadowColorFilter_Sp, (int)shadowColorFilter);
            mat.SetInt(shadowColorGlow_Sp, shadowColorGlow ? 1 : 0);

            if (DrawShape != DrawShape.None)
            {
                // 下限钳到 0.001 避免 FalloffDistance=0 时除零得 +∞
                float pixelSize = 1 / Mathf.Max(0.001f, FalloffDistance);
                mat.SetFloat(pixelWorldScale_Sp, Mathf.Clamp(pixelSize, 0f, 999999f));
            }

            triangle.ModifyMaterial(ref mat);
            circle.ModifyMaterial(ref mat, falloffDistance);
            rectangle.ModifyMaterial(ref mat);
            pentagon.ModifyMaterial(ref mat);
            hexagon.ModifyMaterial(ref mat);
            chamferBox.ModifyMaterial(ref mat);
            quadrilateral.ModifyMaterial(ref mat);
            nStarPolygon.ModifyMaterial(ref mat);
            heart.ModifyMaterial(ref mat);
            blobbyCross.ModifyMaterial(ref mat);
            squircle.ModifyMaterial(ref mat);
            nTriangleRounded.ModifyMaterial(ref mat);

            gradientEffect.ModifyMaterial(ref mat);


            mat.SetInt(drawShape_Sp, (int)DrawShape);
            mat.SetInt(flipHorizontal_Sp, flipHorizontal ? 1 : 0);
            mat.SetInt(flipVertical_Sp, flipVertical ? 1 : 0);

            mat.SetFloat(shapeRotation_Sp, shapeRotation);
            mat.SetInt(constrainedRotation_Sp, constrainRotation ? 1 : 0);

            // -------------------- 色调滤镜（TONE） --------------------
            mat.SetFloat(toneIntensity_Sp, m_ToneIntensity);
            mat.SetInt(toneFilter_Sp, (int)m_ToneFilter);

            // -------------------- 独立颜色滤镜（COLOR FILTER） --------------------
            mat.SetInt(colorFilter_Sp, (int)m_ColorFilterMode);
            mat.SetColor(colorValue_Sp, m_ColorValue);
            mat.SetFloat(colorIntensity_Sp, m_ColorIntensity);
            mat.SetInt(colorGlow_Sp, m_ColorGlow ? 1 : 0);

            // -------------------- 边缘效果（EDGE） --------------------
            mat.SetFloat(edgeWidth_Sp, m_EdgeWidth);
            mat.SetInt(edgeColorFilter_Sp, (int)m_EdgeColorFilterMode);
            mat.SetColor(edgeColor_Sp, m_EdgeColor);
            mat.SetInt(edgeColorGlow_Sp, m_EdgeColorGlow ? 1 : 0);
            mat.SetFloat(edgeShinyRate_Sp, m_EdgeShinyRate);
            mat.SetFloat(edgeShinyWidth_Sp, m_EdgeShinyWidth);
            mat.SetFloat(edgeShinyAutoPlaySpeed_Sp, m_EdgeShinyAutoPlaySpeed);
            mat.SetInt(edgeMode_Sp, (int)m_EdgeMode);

            // -------------------- 采样增强（SAMPLING） --------------------
            mat.SetFloat(samplingIntensity_Sp, m_SamplingIntensity);
            mat.SetInt(samplingMode_Sp, (int)m_SamplingMode);

            // -------------------- 目标模式（TARGET） --------------------
            mat.SetColor(targetColor_Sp, m_TargetColor);
            mat.SetFloat(targetRange_Sp, m_TargetRange);
            mat.SetFloat(targetSoftness_Sp, m_TargetSoftness);
            mat.SetInt(targetMode_Sp, (int)m_TargetMode);

            // -------------------- 图案区域（PATTERN AREA） --------------------
            mat.SetInt(patternArea_Sp, (int)m_PatternArea);

            // -------------------- 渐变纹理（GRADIENT TEXTURE） --------------------
            mat.SetFloat(gradientOffset_Sp, m_GradientOffset);
            mat.SetFloat(gradientScale_Sp, m_GradientScale);
            if (m_EnableGradientTex && m_GradientTex != null)
            {
                mat.SetTexture(gradientTex_Sp, m_GradientTex);
                mat.SetInt(gradientTexEnabled_Sp, 1);
            }
            else
            {
                mat.SetTexture(gradientTex_Sp, null);
                mat.SetInt(gradientTexEnabled_Sp, 0);
            }

            // -------------------- 细节纹理（DETAIL FILTER） --------------------
            mat.SetTexture(detailTex_Sp, m_DetailTex);
            mat.SetVector(detailTex_ST_Sp, new Vector4(m_DetailTexScale.x, m_DetailTexScale.y, m_DetailTexOffset.x, m_DetailTexOffset.y));
            mat.SetVector(detailTexSpeed_Sp, m_DetailTexSpeed);
            mat.SetFloat(detailIntensity_Sp, m_DetailIntensity);
            mat.SetVector(detailThreshold_Sp, m_DetailThreshold);
            mat.SetColor(detailColor_Sp, m_DetailColor);
            mat.SetInt(detailMode_Sp, (int)m_DetailMode);

            // -------------------- 混合模式（BLEND TYPE） --------------------
            // 混合由 shader 的 Blend [_SrcBlend] [_DstBlend] 驱动；
            // 旧代码额外写不存在的 "_BlendType" 属性，Shared 模式回读恒为 0 会覆盖共享材质的混合模式。
            {
                var (src, dst) = ConvertBlendType(m_BlendType);
                mat.SetInt(srcBlend_Sp, (int)src);
                mat.SetInt(dstBlend_Sp, (int)dst);
            }

            // -------------------- 兜底：无精灵、无相机画面时绑定纯白纹理 --------------------
            // 防止动态材质的 _MainTex 被历史状态/共享材质污染成透明或异常纹理，
            // 导致 ImageEx 半透明不可见、且改颜色/赋精灵都无效。
            if (cameraTexture == null && ActiveSprite == null)
            {
                mat.SetTexture(mainTex_Sp, Texture2D.whiteTexture);
            }

            return mat;
        }

        /// <summary>
        /// 应用当前状态对应的全部 Enable 关键字（须在 DisableAllMaterialKeywords 之后调用）。
        /// 与关键字掩码缓存配合：掩码与材质未变化时整体跳过，
        /// 消除动画期间每帧 ~15-30 次字符串关键字查找（None 分支的 Disable 由 DisableAll 覆盖，不重复）。
        /// </summary>
        private void ApplyActiveKeywords(Material mat)
        {
            switch (transitionMode)
            {
                case TransitionMode.Fade: mat.EnableKeyword("TRANSITION_FADE"); break;
                case TransitionMode.Cutoff: mat.EnableKeyword("TRANSITION_CUTOFF"); break;
                case TransitionMode.Dissolve: mat.EnableKeyword("TRANSITION_DISSOLVE"); break;
                case TransitionMode.Shiny: mat.EnableKeyword("TRANSITION_SHINY"); break;
                case TransitionMode.Mask: mat.EnableKeyword("TRANSITION_MASK"); break;
                case TransitionMode.Melt: mat.EnableKeyword("TRANSITION_MELT"); break;
                case TransitionMode.Burn: mat.EnableKeyword("TRANSITION_BURN"); break;
                case TransitionMode.Pattern: mat.EnableKeyword("TRANSITION_PATTERN"); break;
                case TransitionMode.Blaze: mat.EnableKeyword("TRANSITION_BLAZE"); break;
            }

            switch (blurType)
            {
                case BlurType.Fast: mat.EnableKeyword("BLUR_FAST"); break;
                case BlurType.Medium: mat.EnableKeyword("BLUR_MEDIUM"); break;
                case BlurType.Detail: mat.EnableKeyword("BLUR_DETAIL"); break;
            }

            if (strokeWidth > 0 && outlineWidth > 0)
            {
                mat.EnableKeyword("OUTLINED_STROKE");
            }
            else if (strokeWidth > 0)
            {
                mat.EnableKeyword("STROKE");
            }
            else if (outlineWidth > 0)
            {
                mat.EnableKeyword("OUTLINED");
            }

            switch (DrawShape)
            {
                case DrawShape.Circle: mat.EnableKeyword("CIRCLE"); break;
                case DrawShape.Triangle: mat.EnableKeyword("TRIANGLE"); break;
                case DrawShape.Rectangle: mat.EnableKeyword("RECTANGLE"); break;
                case DrawShape.Pentagon: mat.EnableKeyword("PENTAGON"); break;
                case DrawShape.NStarPolygon: mat.EnableKeyword("NSTAR_POLYGON"); break;
                case DrawShape.Hexagon: mat.EnableKeyword("HEXAGON"); break;
                case DrawShape.ChamferBox: mat.EnableKeyword("CHAMFERBOX"); break;
                case DrawShape.Quadrilateral: mat.EnableKeyword("QUADRILATERAL"); break;
                case DrawShape.Heart: mat.EnableKeyword("HEART"); break;
                case DrawShape.BlobbyCross: mat.EnableKeyword("BLOBBYCROSS"); break;
                case DrawShape.Squircle: mat.EnableKeyword("SQUIRCLE"); break;
                case DrawShape.NTriangleRounded: mat.EnableKeyword("NTRIANGLE_ROUNDED"); break;
                default: throw new ArgumentOutOfRangeException();
            }

            switch (m_ToneFilter)
            {
                case ToneFilter.Grayscale: mat.EnableKeyword("TONE_GRAYSCALE"); break;
                case ToneFilter.Sepia: mat.EnableKeyword("TONE_SEPIA"); break;
                case ToneFilter.Negative: mat.EnableKeyword("TONE_NEGATIVE"); break;
                case ToneFilter.Retro: mat.EnableKeyword("TONE_RETRO"); break;
                case ToneFilter.Posterize: mat.EnableKeyword("TONE_POSTERIZE"); break;
            }

            if (m_ColorFilterMode != ColorMode.None)
            {
                mat.EnableKeyword("COLOR_FILTER");
            }

            switch (m_EdgeMode)
            {
                case EdgeMode.Plain: mat.EnableKeyword("EDGE_PLAIN"); break;
                case EdgeMode.Shiny: mat.EnableKeyword("EDGE_SHINY"); break;
            }

            switch (m_SamplingMode)
            {
                case SamplingFilter.Pixelation: mat.EnableKeyword("SAMPLING_PIXELATION"); break;
                case SamplingFilter.RgbShift: mat.EnableKeyword("SAMPLING_RGB_SHIFT"); break;
                case SamplingFilter.EdgeLuminance: mat.EnableKeyword("SAMPLING_EDGE_LUMINANCE"); break;
                case SamplingFilter.EdgeAlpha: mat.EnableKeyword("SAMPLING_EDGE_ALPHA"); break;
            }

            switch (m_TargetMode)
            {
                case TargetMode.Hue: mat.EnableKeyword("TARGET_HUE"); break;
                case TargetMode.Luminance: mat.EnableKeyword("TARGET_LUMINANCE"); break;
            }

            if (m_EnableGradientTex && m_GradientTex != null)
            {
                mat.EnableKeyword("GRADIENT_TEXTURE");
            }

            switch (m_DetailMode)
            {
                case DetailFilter.Masking: mat.EnableKeyword("DETAIL_MASKING"); break;
                case DetailFilter.Multiply: mat.EnableKeyword("DETAIL_MULTIPLY"); break;
                case DetailFilter.Additive: mat.EnableKeyword("DETAIL_ADDITIVE"); break;
                case DetailFilter.Subtractive: mat.EnableKeyword("DETAIL_SUBTRACTIVE"); break;
                case DetailFilter.Replace: mat.EnableKeyword("DETAIL_REPLACE"); break;
                case DetailFilter.MultiplyAdditive: mat.EnableKeyword("DETAIL_MULTIPLY_ADDITIVE"); break;
            }
        }

        /// <summary>
        /// 禁用所有材质关键字
        /// </summary>
        /// <param name="mat"></param>
        private void DisableAllMaterialKeywords(Material mat)
        {
            // 已移除 shader 中不存在的无效关键字：PROCEDURAL / HYBRID / ROUNDED_CORNERS
            mat.DisableKeyword("CIRCLE");
            mat.DisableKeyword("TRIANGLE");
            mat.DisableKeyword("RECTANGLE");
            mat.DisableKeyword("PENTAGON");
            mat.DisableKeyword("HEXAGON");
            mat.DisableKeyword("CHAMFERBOX");
            mat.DisableKeyword("QUADRILATERAL");
            mat.DisableKeyword("NSTAR_POLYGON");
            mat.DisableKeyword("HEART");
            mat.DisableKeyword("BLOBBYCROSS");
            mat.DisableKeyword("SQUIRCLE");
            mat.DisableKeyword("NTRIANGLE_ROUNDED");

            mat.DisableKeyword("STROKE");
            mat.DisableKeyword("OUTLINED");
            mat.DisableKeyword("OUTLINED_STROKE");

            mat.DisableKeyword("GRADIENT_LINEAR");
            mat.DisableKeyword("GRADIENT_CORNER");
            mat.DisableKeyword("GRADIENT_RADIAL");

            mat.DisableKeyword("BLUR_FAST");
            mat.DisableKeyword("BLUR_MEDIUM");
            mat.DisableKeyword("BLUR_DETAIL");

            mat.DisableKeyword("TRANSITION_FADE");
            mat.DisableKeyword("TRANSITION_CUTOFF");
            mat.DisableKeyword("TRANSITION_DISSOLVE");
            mat.DisableKeyword("TRANSITION_SHINY");
            mat.DisableKeyword("TRANSITION_MASK");
            mat.DisableKeyword("TRANSITION_MELT");
            mat.DisableKeyword("TRANSITION_BURN");
            mat.DisableKeyword("TRANSITION_PATTERN");
            mat.DisableKeyword("TRANSITION_BLAZE");

            mat.DisableKeyword("TONE_GRAYSCALE");
            mat.DisableKeyword("TONE_SEPIA");
            mat.DisableKeyword("TONE_NEGATIVE");
            mat.DisableKeyword("TONE_RETRO");
            mat.DisableKeyword("TONE_POSTERIZE");

            mat.DisableKeyword("COLOR_FILTER");

            mat.DisableKeyword("EDGE_PLAIN");
            mat.DisableKeyword("EDGE_SHINY");

            mat.DisableKeyword("SAMPLING_PIXELATION");
            mat.DisableKeyword("SAMPLING_RGB_SHIFT");
            mat.DisableKeyword("SAMPLING_EDGE_LUMINANCE");
            mat.DisableKeyword("SAMPLING_EDGE_ALPHA");

            mat.DisableKeyword("TARGET_HUE");
            mat.DisableKeyword("TARGET_LUMINANCE");

            mat.DisableKeyword("GRADIENT_TEXTURE");

            mat.DisableKeyword("DETAIL_MASKING");
            mat.DisableKeyword("DETAIL_MULTIPLY");
            mat.DisableKeyword("DETAIL_ADDITIVE");
            mat.DisableKeyword("DETAIL_SUBTRACTIVE");
            mat.DisableKeyword("DETAIL_REPLACE");
            mat.DisableKeyword("DETAIL_MULTIPLY_ADDITIVE");
        }

        /// <summary>
        /// 从共享材质初始化值
        /// </summary>
        public void InitValuesFromSharedMaterial()
        {
            if (m_Material == null) return;
            Material mat = m_Material;

            // 基本设置
            drawShape = (DrawShape)mat.GetInt(drawShape_Sp);

            blurType = (BlurType)mat.GetInt(blurType_Sp);
            blurIntensity = mat.GetFloat(blurIntensity_Sp);

            transitionMode = (TransitionMode)mat.GetInt(transitionMode_Sp);
            transitionTexture = mat.GetTexture(transitionTex_Sp);
            Vector4 st = mat.GetVector(transitionTex_ST_Sp);
            transitionTexScale = new Vector2(st.x, st.y);
            transitionTexOffset = new Vector2(st.z, st.w);
            transitionTexRotation = mat.GetFloat(transitionTexRotation_Sp);
            transitionRate = mat.GetFloat(transitionRate_Sp);
            transitionColor = mat.GetColor(transitionColor_Sp);
            transitionWidth = mat.GetFloat(transitionWidth_Sp);
            transitionSoftness = mat.GetFloat(transitionSoftness_Sp);
            transitionReverse = mat.GetInt(transitionReverse_Sp) == 1;
            transitionSpeed = mat.GetVector(transitionTexSpeed_Sp);
            transitionPatternReverse = mat.GetInt(transitionPatternReverse_Sp) == 1;
            transitionAutoPlaySpeed = mat.GetFloat(transitionAutoPlaySpeed_Sp);
            transitionColorFilter = (ColorMode)mat.GetInt(transitionColorFilter_Sp);
            transitionColorGlow = mat.GetInt(transitionColorGlow_Sp) == 1;
            transitionGradient = mat.GetTexture(transitionGradientTex_Sp);
            transitionRange = mat.GetVector(transitionRange_Sp);
            transitionClamp = mat.GetFloat(transitionClamp_Sp) == 1;
            transitionTexClampPadding = mat.GetFloat(transitionTexClampPadding_Sp);
            transitionUseUv0 = mat.GetFloat(transitionUseUv0_Sp) == 1;

            strokeWidth = mat.GetFloat(strokeWidth_Sp);
            falloffDistance = mat.GetFloat(falloffDistance_Sp);

            outlineWidth = mat.GetFloat(outlineWidth_Sp);
            outlineColor = mat.GetColor(outlineColor_Sp);
            enableDashedOutline = mat.GetInt(enableDashedOutline_Sp);
            customTime = mat.GetFloat(customTime_Sp);

            // 阴影镜像方向
            shadowMirrorDirection = (ShadowDirection)mat.GetInt(shadowMirrorDirection_Sp);

            shadowMirrorScale = mat.GetFloat(shadowMirrorScale_Sp);
            shadowMirrorOffset = mat.GetVector(shadowMirrorOffset_Sp);
            shadowMirrorShowSource = mat.GetFloat(shadowMirrorShowSource_Sp) > 0.5f;
            shadowMirrorTintMix = mat.GetFloat(shadowMirrorTintMix_Sp);
            shadowColorFilter = (ColorMode)mat.GetInt(shadowColorFilter_Sp);
            shadowColorGlow = mat.GetInt(shadowColorGlow_Sp) == 1;

            flipHorizontal = mat.GetInt(flipHorizontal_Sp) == 1;
            flipVertical = mat.GetInt(flipVertical_Sp) == 1;
            constrainRotation = mat.GetInt(constrainedRotation_Sp) == 1;
            shapeRotation = mat.GetFloat(shapeRotation_Sp);

            shadowColor = mat.GetColor(shadowColor_Sp);
            shadowBlurIntensity = mat.GetFloat(shadowBlurIntensity_Sp);
            shadowMode = (ShadowMode)mat.GetInt(shadowMode_Sp);
            samplingWidth = mat.GetFloat(samplingWidth_Sp);
            samplingScale = mat.GetFloat(samplingScale_Sp);
            shadowScale = Mathf.Clamp(mat.GetFloat(shadowScale_Sp), 0.1f, 4f);
            allowOutOfBoundsShadow = mat.GetFloat(allowOutOfBoundsShadow_Sp) == 1;

            triangle.InitValuesFromMaterial(ref mat);
            circle.InitValuesFromMaterial(ref mat);
            rectangle.InitValuesFromMaterial(ref mat);
            pentagon.InitValuesFromMaterial(ref mat);
            hexagon.InitValuesFromMaterial(ref mat);
            chamferBox.InitValuesFromMaterial(ref mat);
            quadrilateral.InitValuesFromMaterial(ref mat);
            nStarPolygon.InitValuesFromMaterial(ref mat);
            heart.InitValuesFromMaterial(ref mat);
            blobbyCross.InitValuesFromMaterial(ref mat);
            squircle.InitValuesFromMaterial(ref mat);
            nTriangleRounded.InitValuesFromMaterial(ref mat);

            // GradientEffect
            gradientEffect.InitValuesFromMaterial(ref mat);

            // 色调滤镜
            m_ToneIntensity = mat.GetFloat(toneIntensity_Sp);
            m_ToneFilter = (ToneFilter)mat.GetInt(toneFilter_Sp);

            // 独立颜色滤镜
            m_ColorFilterMode = (ColorMode)mat.GetInt(colorFilter_Sp);
            m_ColorValue = mat.GetColor(colorValue_Sp);
            m_ColorIntensity = mat.GetFloat(colorIntensity_Sp);
            m_ColorGlow = mat.GetInt(colorGlow_Sp) == 1;

            // 边缘效果
            m_EdgeWidth = mat.GetFloat(edgeWidth_Sp);
            m_EdgeColorFilterMode = (ColorMode)mat.GetInt(edgeColorFilter_Sp);
            m_EdgeColor = mat.GetColor(edgeColor_Sp);
            m_EdgeColorGlow = mat.GetInt(edgeColorGlow_Sp) == 1;
            m_EdgeShinyRate = mat.GetFloat(edgeShinyRate_Sp);
            m_EdgeShinyWidth = mat.GetFloat(edgeShinyWidth_Sp);
            m_EdgeShinyAutoPlaySpeed = mat.GetFloat(edgeShinyAutoPlaySpeed_Sp);
            m_EdgeMode = (EdgeMode)mat.GetInt(edgeMode_Sp);

            // 采样增强
            m_SamplingIntensity = mat.GetFloat(samplingIntensity_Sp);
            m_SamplingMode = (SamplingFilter)mat.GetInt(samplingMode_Sp);

            // 目标模式
            m_TargetColor = mat.GetColor(targetColor_Sp);
            m_TargetRange = mat.GetFloat(targetRange_Sp);
            m_TargetSoftness = mat.GetFloat(targetSoftness_Sp);
            m_TargetMode = (TargetMode)mat.GetInt(targetMode_Sp);

            // 图案区域
            m_PatternArea = (PatternArea)mat.GetInt(patternArea_Sp);

            // 细节纹理
            m_DetailTex = mat.GetTexture(detailTex_Sp);
            m_DetailIntensity = mat.GetFloat(detailIntensity_Sp);
            m_DetailThreshold = mat.GetVector(detailThreshold_Sp);
            m_DetailColor = mat.GetColor(detailColor_Sp);
            m_DetailMode = (DetailFilter)mat.GetInt(detailMode_Sp);
            Vector4 detailST = mat.GetVector(detailTex_ST_Sp);
            m_DetailTexScale = new Vector2(detailST.x, detailST.y);
            m_DetailTexOffset = new Vector2(detailST.z, detailST.w);
            m_DetailTexSpeed = mat.GetVector(detailTexSpeed_Sp);

            // 渐变纹理
            m_GradientOffset = mat.GetFloat(gradientOffset_Sp);
            m_GradientScale = mat.GetFloat(gradientScale_Sp);
            m_EnableGradientTex = mat.GetInt(gradientTexEnabled_Sp) == 1;
            m_GradientTex = mat.GetTexture(gradientTex_Sp);
            // 由 shader 实际生效的 _SrcBlend/_DstBlend 反查混合模式（材质不存在 _BlendType 属性）
            m_BlendType = BlendTypeFromBlendModes(mat.GetInt(srcBlend_Sp), mat.GetInt(dstBlend_Sp));
        }

        private static (UnityEngine.Rendering.BlendMode, UnityEngine.Rendering.BlendMode) ConvertBlendType(BlendType type)
        {
            return type switch
            {
                BlendType.AlphaBlend => (UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha),
                BlendType.Multiply => (UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha),
                BlendType.Additive => (UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One),
                BlendType.SoftAdditive => (UnityEngine.Rendering.BlendMode.OneMinusDstColor, UnityEngine.Rendering.BlendMode.One),
                BlendType.MultiplyAdditive => (UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.One),
                _ => (UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
            };
        }

        /// <summary>
        /// 由材质的 _SrcBlend/_DstBlend 反查 BlendType（ConvertBlendType 的逆映射）。
        /// 未匹配任何预设组合时回退 AlphaBlend，避免 Shared 回读覆盖手改的混合模式。
        /// </summary>
        private static BlendType BlendTypeFromBlendModes(int src, int dst)
        {
            switch (src)
            {
                case (int)UnityEngine.Rendering.BlendMode.SrcAlpha when dst == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha:
                    return BlendType.AlphaBlend;
                case (int)UnityEngine.Rendering.BlendMode.DstColor when dst == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha:
                    return BlendType.Multiply;
                case (int)UnityEngine.Rendering.BlendMode.One when dst == (int)UnityEngine.Rendering.BlendMode.One:
                    return BlendType.Additive;
                case (int)UnityEngine.Rendering.BlendMode.OneMinusDstColor when dst == (int)UnityEngine.Rendering.BlendMode.One:
                    return BlendType.SoftAdditive;
                case (int)UnityEngine.Rendering.BlendMode.DstColor when dst == (int)UnityEngine.Rendering.BlendMode.One:
                    return BlendType.MultiplyAdditive;
                default:
                    return BlendType.AlphaBlend;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 创建材质资产
        /// </summary>
        /// <returns></returns>
        public Material CreateMaterialAssetFromComponentSettings()
        {
            Material matAsset = new Material(Shader.Find(shaderName));
            matAsset = GetModifiedMaterial(matAsset);
            string path = EditorUtility.SaveFilePanelInProject("通过ImageEx创建材质",
                "Material", "mat", "选择位置");
            AssetDatabase.CreateAsset(matAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return matAsset;
        }
#endif
    }
}
