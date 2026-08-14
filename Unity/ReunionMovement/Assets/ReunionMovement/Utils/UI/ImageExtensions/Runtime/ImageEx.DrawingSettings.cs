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
    /// ImageEx partial part: Drawing Settings (same class, no behavior/serialization change)
    /// </summary>
    public partial class ImageEx
    {
        #region 绘图设置

        /// <summary>
        /// 要绘制形状的类型
        /// </summary>
        public DrawShape DrawShape
        {
            get => drawShape;
            set
            {
                drawShape = value;
                if (material == m_Material)
                {
                    m_Material.SetInt(drawShape_Sp, (int)drawShape);
                }

                // 形状完全由 shader SDF 决定，网格不依赖 drawShape，无需重建顶点
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 绘制形状的线条宽度。0不是线条
        /// </summary>
        public float StrokeWidth
        {
            get => strokeWidth;
            set
            {
                strokeWidth = value;
                strokeWidth = strokeWidth < 0 ? 0 : strokeWidth;
                if (material == m_Material)
                {
                    m_Material.SetFloat(strokeWidth_Sp, strokeWidth);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 绘制形状的轮廓宽度。0不是轮廓。
        /// </summary>
        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                outlineWidth = outlineWidth < 0 ? 0 : outlineWidth;
                if (m_Material == material)
                {
                    m_Material.SetFloat(outlineWidth_Sp, outlineWidth);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 轮廓的颜色。如果“轮廓宽度”的值为0，则没有效果
        /// </summary>
        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                if (m_Material == material)
                {
                    m_Material.SetColor(outlineColor_Sp, outlineColor);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 是否启用虚线轮廓
        /// </summary>
        public int EnableDashedOutline
        {
            get => enableDashedOutline;
            set
            {
                enableDashedOutline = value;
                if (m_Material == material)
                {
                    m_Material.SetInt(enableDashedOutline_Sp, enableDashedOutline);
                }
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 自定义时间参数，可用于动画效果
        /// </summary>
        public float CustomTime
        {
            get => customTime;
            set
            {
                // 值相等守卫：动画每帧写同值时不再置脏（停止的动画自动归零重写开销）
                if (Mathf.Approximately(customTime, value)) return;
                customTime = value;
                if (m_Material == material)
                {
                    m_Material.SetFloat(customTime_Sp, customTime);
                }
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影镜像的方向
        /// </summary>
        public ShadowDirection ShadowMirrorDirection
        {
            get => shadowMirrorDirection;
            set
            {
                shadowMirrorDirection = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影镜像的缩放比例
        /// </summary>
        public float ShadowMirrorScale
        {
            get => shadowMirrorScale;
            set
            {
                shadowMirrorScale = Mathf.Clamp(value, 0f, 2f);
                SetMaterialDirty();
            }
        }

        public bool ShadowMirrorShowSource
        {
            get => shadowMirrorShowSource;
            set
            {
                shadowMirrorShowSource = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影镜像的偏移量
        /// </summary>
        public Vector2 ShadowMirrorOffset
        {
            get => shadowMirrorOffset;
            set
            {
                shadowMirrorOffset = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影镜像的颜色混合比例
        /// </summary>
        public float ShadowMirrorTintMix
        {
            get => shadowMirrorTintMix;
            set
            {
                shadowMirrorTintMix = Mathf.Clamp01(value);
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影颜色滤镜模式
        /// </summary>
        public ColorMode ShadowColorFilter
        {
            get => shadowColorFilter;
            set
            {
                shadowColorFilter = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 阴影颜色发光
        /// </summary>
        public bool ShadowColorGlow
        {
            get => shadowColorGlow;
            set
            {
                shadowColorGlow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 形状的边缘衰减距离
        /// </summary>
        public float FalloffDistance
        {
            get { return falloffDistance; }
            set
            {
                falloffDistance = Mathf.Max(value, 0f);
                if (material == m_Material)
                {
                    m_Material.SetFloat(falloffDistance_Sp, falloffDistance);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 如果设置为true，则将旋转约束为0、90、270度角。
        /// 但是形状的宽度和高度根据需要进行更换以避免剪裁。
        /// 如果设置为false，则任何形状都可以以任意角度旋转，但通常会导致形状的剪裁。
        /// </summary>
        public bool ConstrainRotation
        {
            get { return constrainRotation; }
            set
            {
                constrainRotation = value;

                if (m_Material == material)
                {
                    m_Material.SetInt(constrainedRotation_Sp, value ? 1 : 0);
                }
                if (value)
                {
                    shapeRotation = ConstrainRotationValue(shapeRotation);
                }

                // 约束旋转由 shader 处理，网格不依赖，无需重建顶点
                base.SetMaterialDirty();
            }
        }

        private float ConstrainRotationValue(float val)
        {
            float finalRotation = val - val % 90;
            if (Mathf.Abs(finalRotation) >= 360) finalRotation = 0;
            return finalRotation;
        }

        /// <summary>
        /// 形状的旋转
        /// </summary>
        public float ShapeRotation
        {
            get { return shapeRotation; }
            set
            {
                shapeRotation = constrainRotation ? ConstrainRotationValue(value) : value;
                if (m_Material == material)
                {
                    m_Material.SetFloat(shapeRotation_Sp, shapeRotation);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 水平翻转形状
        /// </summary>
        public bool FlipHorizontal
        {
            get { return flipHorizontal; }
            set
            {
                flipHorizontal = value;
                if (m_Material == material)
                {
                    m_Material.SetInt(flipHorizontal_Sp, flipHorizontal ? 1 : 0);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 垂直翻转形状
        /// </summary>
        public bool FlipVertical
        {
            get { return flipVertical; }
            set
            {
                flipVertical = value;
                if (m_Material == material)
                {
                    m_Material.SetInt(flipVertical_Sp, flipVertical ? 1 : 0);
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// Alpha阈值
        /// </summary>
        public float AlphaThreshold
        {
            get { return alphaThreshold; }
            set
            {
                alphaThreshold = value;
                try
                {
                    alphaHitTestMinimumThreshold = alphaThreshold;
                }
                catch (InvalidOperationException)
                {
                    // Unity 文档化行为：当前材质/精灵不支持 alpha 命中测试时抛出，属预期，静默忽略
                }
            }
        }

        /// <summary>
        /// 定义用于渲染形状的材质类型。可选 Dynamic 或 Shared。
        /// 默认值为 Dynamic，每个 image 对象会产生一个绘制调用。
        /// 如果设置为 Shared，则会使用组件中分配的共享材质来渲染；
        /// 如果材质槽没有分配材质，则回退为动态材质。
        /// </summary>
        public MaterialMode MaterialMode
        {
            get { return materialMode; }
            set
            {
                if (materialMode == value) return;
                materialMode = value;
                InitializeComponents();
                if (material == m_Material)
                {
                    InitValuesFromSharedMaterial();
#if UNITY_EDITOR
                    parseAgainOnValidate = true;
#endif
                }

                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 用于渲染形状的共享材质。材质必须使用“ReunionMovement/UI/ImageEx”着色器
        /// </summary>
        public override Material material
        {
            get
            {
                if (m_Material && materialMode == MaterialMode.Shared)
                {
                    return m_Material;
                }

                return DynamicMaterial;
            }
            set
            {
                m_Material = value;

                if (m_Material && materialMode == MaterialMode.Shared && m_Material.shader.name == shaderName)
                {
                    InitValuesFromSharedMaterial();
#if UNITY_EDITOR
                    parseAgainOnValidate = true;
#endif
                }

                InitializeComponents();
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 图像的类型。仅支持两种类型。简单和填充。
        /// 默认值和回退值为“简单”。
        /// 注意：Image.type 在 Unity 中非 virtual，只能 new 隐藏；
        /// 渲染统一以 imageType 为准，OnPopulateMesh 的 case Type.Sliced 与 Simple 同路径。
        /// </summary>
        public new Type type
        {
            get => imageType;
            set
            {
                // 不支持的类型（Tiled/Sliced）归一化为 Simple，避免被静默接受后
                // imageType 与 base.type 不一致造成双态
                Type newType = value;
                switch (value)
                {
                    case Type.Simple:
                    case Type.Filled:
                        break;
                    case Type.Tiled:
                    case Type.Sliced:
                        newType = Type.Simple;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(value.ToString(), value, null);
                }

                if (imageType != newType)
                {
                    imageType = newType;
                }
                // 始终同步基类字段，消除双态
                if (base.type != newType)
                {
                    base.type = newType;
                }
            }
        }

        #endregion
    }
}
