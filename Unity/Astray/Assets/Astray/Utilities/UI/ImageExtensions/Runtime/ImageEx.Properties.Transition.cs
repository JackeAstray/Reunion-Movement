using System;
using Astray.Common;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Astray.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：过渡与细节属性（过渡 / 细节 / 混合 / 模糊 / 阴影）。
    /// </summary>
    public partial class ImageEx
    {
        /// <summary>
        /// 图案区域：All / Inner / Edge
        /// </summary>
        public PatternArea TransitionPatternArea
        {
            get => m_PatternArea;
            set
            {
                m_PatternArea = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 细节滤镜模式：None / Masking / Multiply / Additive / Subtractive / Replace / MultiplyAdditive
        /// </summary>
        public DetailFilter Detail
        {
            get => m_DetailMode;
            set
            {
                m_DetailMode = value;
                SetMaterialDirty();
            }
        }

        public Texture DetailTex
        {
            get => m_DetailTex;
            set { m_DetailTex = value; SetMaterialDirty(); }
        }

        public Vector2 DetailTexScale
        {
            get => m_DetailTexScale;
            set { m_DetailTexScale = value; SetMaterialDirty(); }
        }

        public Vector2 DetailTexOffset
        {
            get => m_DetailTexOffset;
            set { m_DetailTexOffset = value; SetMaterialDirty(); }
        }

        public Vector2 DetailTexSpeed
        {
            get => m_DetailTexSpeed;
            set { m_DetailTexSpeed = value; SetMaterialDirty(); }
        }

        public float DetailIntensity
        {
            get => m_DetailIntensity;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_DetailIntensity, clamped)) return;
                m_DetailIntensity = clamped;
                SetMaterialDirty();
            }
        }

        public Vector2 DetailThreshold
        {
            get => m_DetailThreshold;
            set { m_DetailThreshold = value; SetMaterialDirty(); }
        }

        public Color DetailColor
        {
            get => m_DetailColor;
            set { m_DetailColor = value; SetMaterialDirty(); }
        }

        /// <summary>
        /// 渐变纹理模式
        /// </summary>
        public bool EnableGradientTex
        {
            get => m_EnableGradientTex;
            set { m_EnableGradientTex = value; SetMaterialDirty(); }
        }

        public Texture GradientTex
        {
            get => m_GradientTex;
            set { m_GradientTex = value; SetMaterialDirty(); }
        }

        public float GradientOffset
        {
            get => m_GradientOffset;
            set
            {
                float clamped = Mathf.Clamp(value, -1f, 1f);
                if (Mathf.Approximately(m_GradientOffset, clamped)) return;
                m_GradientOffset = clamped;
                SetMaterialDirty();
            }
        }

        public float GradientScale
        {
            get => m_GradientScale;
            set { m_GradientScale = Mathf.Clamp(value, 0.1f, 5f); SetMaterialDirty(); }
        }

        /// <summary>
        /// 混合模式：AlphaBlend / Multiply / Additive / SoftAdditive / MultiplyAdditive
        /// </summary>
        public BlendType Blend
        {
            get => m_BlendType;
            set
            {
                m_BlendType = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 模糊类型
        /// </summary>
        public BlurType Blur
        {
            get => blurType;
            set
            {
                blurType = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 模糊强度
        /// </summary>
        public float BlurIntensity
        {
            get => blurIntensity;
            set
            {
                if (Mathf.Approximately(blurIntensity, value)) return;
                blurIntensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡模式
        /// </summary>
        public TransitionMode Transition
        {
            get => transitionMode;
            set
            {
                transitionMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理
        /// </summary>
        public Texture TransitionTexture
        {
            get => transitionTexture;
            set
            {
                transitionTexture = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的缩放
        /// </summary>
        public Vector2 TransitionTexScale
        {
            get => transitionTexScale;
            set
            {
                transitionTexScale = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的缩放
        /// </summary>
        public Vector2 TransitionTexOffset
        {
            get => transitionTexOffset;
            set
            {
                transitionTexOffset = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的旋转角度
        /// </summary>
        public float TransitionTexRotation
        {
            get => transitionTexRotation;
            set
            {
                transitionTexRotation = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否保持过渡纹理的宽高比
        /// </summary>
        public bool TransitionKeepAspectRatio
        {
            get => transitionKeepAspectRatio;
            set
            {
                transitionKeepAspectRatio = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡的进度
        /// </summary>
        public float TransitionRate
        {
            get => transitionRate;
            set
            {
                if (Mathf.Approximately(transitionRate, value)) return;
                transitionRate = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡颜色
        /// </summary>
        public Color TransitionColor
        {
            get => transitionColor;
            set
            {
                transitionColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡 HSV 色相偏移 (-0.5~0.5)，对应 TransitionColor.r（HsvModifier 模式）
        /// </summary>
        public float TransitionColorHueShift
        {
            get => transitionColor.r;
            set { var c = transitionColor; c.r = Mathf.Clamp(value, -0.5f, 0.5f); TransitionColor = c; }
        }

        public float TransitionColorSaturationShift
        {
            get => transitionColor.g;
            set { var c = transitionColor; c.g = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        public float TransitionColorValueShift
        {
            get => transitionColor.b;
            set { var c = transitionColor; c.b = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        public float TransitionColorContrastShift
        {
            get => transitionColor.r;
            set { var c = transitionColor; c.r = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        public float TransitionColorBrightnessShift
        {
            get => transitionColor.g;
            set { var c = transitionColor; c.g = Mathf.Clamp(value, -1f, 1f); TransitionColor = c; }
        }

        /// <summary>
        /// 过渡的宽度
        /// </summary>
        public float TransitionWidth
        {
            get => transitionWidth;
            set
            {
                transitionWidth = value;
                SetMaterialDirty();
            }
        }

        public float TransitionSoftness
        {
            get => transitionSoftness;
            set
            {
                transitionSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否反转过渡效果
        /// </summary>
        public bool TransitionReverse
        {
            get => transitionReverse;
            set
            {
                transitionReverse = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理的移动速度
        /// </summary>
        public Vector2 TransitionSpeed
        {
            get => transitionSpeed;
            set
            {
                transitionSpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否反转过渡纹理的图案
        /// </summary>
        public bool TransitionPatternReverse
        {
            get => transitionPatternReverse;
            set
            {
                transitionPatternReverse = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡自动播放速度
        /// </summary>
        public float TransitionAutoPlaySpeed
        {
            get => transitionAutoPlaySpeed;
            set
            {
                transitionAutoPlaySpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡颜色滤镜模式
        /// </summary>
        public ColorMode TransitionColorFilter
        {
            get => transitionColorFilter;
            set
            {
                transitionColorFilter = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否启用过渡颜色发光效果
        /// </summary>
        public bool TransitionColorGlow
        {
            get => transitionColorGlow;
            set
            {
                transitionColorGlow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡渐变纹理
        /// </summary>
        public Texture TransitionGradient
        {
            get => transitionGradient;
            set
            {
                transitionGradient = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡渐变的值
        /// </summary>
        public Gradient TransitionGradientValue
        {
            get => transitionGradientValue;
            set
            {
                transitionGradientValue = value;
                RefreshTransitionGradient();
            }
        }

        /// <summary>
        /// 过渡渐变的范围
        /// </summary>
        public Vector2 TransitionRange
        {
            get => transitionRange;
            set
            {
                transitionRange = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否在过渡过程中夹紧纹理坐标
        /// </summary>
        public bool TransitionClamp
        {
            get => transitionClamp;
            set
            {
                transitionClamp = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 过渡纹理夹紧的填充量
        /// </summary>
        public float TransitionTexClampPadding
        {
            get => transitionTexClampPadding;
            set
            {
                transitionTexClampPadding = Mathf.Clamp(value, 0, 4);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否使用UV0坐标进行过渡效果
        /// </summary>
        public bool TransitionUseUv0
        {
            get => transitionUseUv0;
            set
            {
                transitionUseUv0 = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影颜色
        /// </summary>
        public Color ShadowColor
        {
            get => shadowColor;
            set
            {
                shadowColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影模糊强度
        /// </summary>
        public float ShadowBlurIntensity
        {
            get => shadowBlurIntensity;
            set
            {
                // 对齐两处既有声明：字段上的 [Range(0, 100)]（ImageEx.cs）与着色器 _ShadowBlurIntensity
                // 声明的 Range(0,100)（ImageEx.shader:99）。此前此处夹到 8 属孤例 —— 面板可设 0~100 且
                // 着色器照单执行，而代码赋值却被静默压到 8，同一个设置因入口不同而结果不同。
                shadowBlurIntensity = Mathf.Clamp(value, 0f, 100f);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影采样宽度
        /// </summary>
        public float SamplingWidth
        {
            get => samplingWidth;
            set
            {
                samplingWidth = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影采样缩放比例
        /// </summary>
        public float SamplingScale
        {
            get => samplingScale;
            set
            {
                samplingScale = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影缩放比例。1 为原始大小，>1 放大，<1 缩小。
        /// </summary>
        public float ShadowScale
        {
            get => shadowScale;
            set
            {
                shadowScale = Mathf.Clamp(value, 0.1f, 4f);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否允许阴影超出边界绘制
        /// </summary>
        public bool AllowOutOfBoundsShadow
        {
            get => allowOutOfBoundsShadow;
            set
            {
                allowOutOfBoundsShadow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影模式
        /// </summary>
        public ShadowMode Shadow
        {
            get => shadowMode;
            set
            {
                shadowMode = value;
                SetMaterialDirty();
            }
        }
    }
}
