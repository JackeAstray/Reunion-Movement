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
    /// ImageEx partial part：字段、着色器解析与生命周期（Awake / Init / OnDestroy / 材质重建）。
    /// </summary>
    public partial class ImageEx
    {
        #region 私有变量

        private Material dynamicMaterial;

        // ===== Shared 模式串扰诊断 =====
        // 同一共享材质被特效设置不同的多个 ImageEx 使用时，渲染由最后写入者决定，
        // 形状/特效会互相覆盖。以下静态注册表用于检测并告警一次（不改变渲染行为）。
        private static readonly System.Collections.Generic.Dictionary<Material, int> s_sharedMatLastMask
            = new System.Collections.Generic.Dictionary<Material, int>(8);
        private static readonly System.Collections.Generic.HashSet<Material> s_sharedMatWarned
            = new System.Collections.Generic.HashSet<Material>();

        /// <summary>Shared 模式是否已回读共享材质（避免每帧回读）</summary>
        private bool m_sharedValuesLoaded;
        /// <summary>上次回读的共享材质引用（引用变化时重新回读）</summary>
        private Material m_loadedSharedMaterial;

        /// <summary>
        /// ImageEx 着色器在 Resources 下的相对路径 —— 与 <see cref="shaderName"/> 指向同一个资产
        /// （<c>Resources/Shader/UI/ImageEx/ImageEx.shader</c>）。用于按名字查不到时兜底。
        /// </summary>
        private const string kShaderResourcePath = "Shader/UI/ImageEx/ImageEx";

        /// <summary>已解析的着色器（进程内缓存；资产被销毁时 Unity 的重载 == 会判为 null，从而重新解析）</summary>
        private static Shader s_resolvedShader;

        /// <summary>
        /// 解析 ImageEx 着色器：先按名字 <see cref="Shader.Find"/>，再按 Resources 路径取。
        /// <para>
        /// 两步都在的理由：删掉 <c>Library/</c> 后首次导入时，<c>OnValidate</c> 可能早于着色器导入完成，
        /// 此时 <c>Shader.Find</c> 会返回 null 并打出"着色器未找到"的假警告；着色器本身就在 Resources 下，
        /// 按路径取是稳定的。真正的着色器编译错误仍会在 Console 里单独报出来。
        /// </para>
        /// </summary>
        private static Shader ResolveShader()
        {
            if (s_resolvedShader != null) return s_resolvedShader;

            s_resolvedShader = Shader.Find(shaderName);
            if (s_resolvedShader != null) return s_resolvedShader;

            s_resolvedShader = Resources.Load<Shader>(kShaderResourcePath);
            return s_resolvedShader;
        }

        private Material DynamicMaterial
        {
            get
            {
                if (dynamicMaterial == null)
                {
                    Shader shader = ResolveShader();
                    if (shader == null)
                    {
                        Log.Warning("着色器 '{0}' 未找到（已尝试 Shader.Find 与 Resources/{1}），已回退到 'Sprites/Default'。请确认项目中存在对应着色器。",
                            shaderName, kShaderResourcePath);
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

        private void ReleaseDynamicMaterial()
        {
            if (dynamicMaterial == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(dynamicMaterial);
            else
                UnityEngine.Object.DestroyImmediate(dynamicMaterial);
            dynamicMaterial = null;
        }

        private void RemoveSharedMaterialDiagnostics(Material material)
        {
            if (material == null) return;
            s_sharedMatLastMask.Remove(material);
            s_sharedMatWarned.Remove(material);
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
            if (tex != null && transitionGradientIsRuntime)
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
#endif
        // 玩家构建不再定义 Reset()：它是编辑器专用 Unity 消息，player 中永不被调用；
        // 原 #else 分支会隐藏 UIBehaviour.Reset() 并让每个 player 构建都产生 CS0114 警告
        // （已实证全项目无 SendMessage("Reset") 形式的调用，删除后行为不变）

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
            ReleaseDynamicMaterial();
            RemoveSharedMaterialDiagnostics(m_Material);

            // 销毁运行时生成的过渡渐变纹理（用户资产不销毁），防止每组件泄漏一张原生纹理
            if (transitionGradient != null && transitionGradientIsRuntime)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(transitionGradient);
                else
                    UnityEngine.Object.DestroyImmediate(transitionGradient);
                transitionGradient = null;
                transitionGradientIsRuntime = false;
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
    }
}
