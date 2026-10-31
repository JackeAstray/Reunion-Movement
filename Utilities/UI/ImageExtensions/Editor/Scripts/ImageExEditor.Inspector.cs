using System;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Common;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageExEditor partial part：OnInspectorGUI 主面板。
    /// </summary>
    public partial class ImageExEditor : ImageEditor
    {

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FixShaderChannelGUI();

            RaycastControlsGUI();
            EditorGUILayout.PropertyField(m_Color);
            EditorGUILayout.Space();

            CameraTextureGUI();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(spShape, new GUIContent("绘制形状"));

            if (spShape.enumValueIndex != (int)DrawShape.None)
            {
                EditorGUILayout.BeginVertical("Box");
                if (!spShape.hasMultipleDifferentValues)
                {
                    switch ((DrawShape)spShape.enumValueIndex)
                    {
                        case DrawShape.Circle:
                            EditorGUILayout.PropertyField(spCircle);
                            break;
                        case DrawShape.Rectangle:
                            EditorGUILayout.PropertyField(spRectangle);
                            break;
                        case DrawShape.Pentagon:
                            EditorGUILayout.PropertyField(spPentagon);
                            break;
                        case DrawShape.Triangle:
                            EditorGUILayout.PropertyField(spTriangle);
                            break;
                        case DrawShape.Hexagon:
                            EditorGUILayout.PropertyField(spHexagon);
                            break;
                        case DrawShape.ChamferBox:
                            EditorGUILayout.PropertyField(spChamferBox);
                            break;
                        case DrawShape.Quadrilateral:
                            EditorGUILayout.PropertyField(spQuadrilateral);
                            break;
                        case DrawShape.NStarPolygon:
                            EditorGUILayout.PropertyField(spNStarPolygon);
                            break;
                        case DrawShape.Heart:
                            EditorGUILayout.PropertyField(spHeart);
                            break;
                        case DrawShape.BlobbyCross:
                            EditorGUILayout.PropertyField(spBlobbyCross);
                            break;
                        case DrawShape.Squircle:
                            EditorGUILayout.PropertyField(spSquircle);
                            break;
                        case DrawShape.NTriangleRounded:
                            EditorGUILayout.PropertyField(spNTriangleRounded);
                            break;

                        default:
                            // 非法枚举值（旧序列化残留）：跳过绘制而非抛异常，避免 Inspector 中断；限频告警
                            if (!m_invalidShapeLogged)
                            {
                                m_invalidShapeLogged = true;
                                Log.Warning("ImageEx.DrawShape 存在非法值 {0}，已跳过形状参数绘制", spShape.enumValueIndex);
                            }
                            break;
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            AdditionalShapeDataGUI();
            EditorGUILayout.Space();

            ImageTypeGUI();

            SpriteGUI();

            if (!spSprite.hasMultipleDifferentValues && spSprite.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(spPreserveAspect);
            }

            SetShowNativeSize(spSprite.objectReferenceValue != null, true);
            NativeSizeButtonGUI();

            EditorGUILayout.Space();
            SharedMaterialGUI();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spGradient);
                // Phase 3: 渐变纹理子选项
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(spEnableGradientTex, new GUIContent("渐变纹理"));
                if (spEnableGradientTex.boolValue)
                {
                    EditorGUILayout.PropertyField(spGradientTex, new GUIContent("纹理"));
                }
                EditorGUILayout.PropertyField(spGradientOffset, new GUIContent("偏移"));
                EditorGUILayout.PropertyField(spGradientScale, new GUIContent("缩放"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spBlurType, new GUIContent("模糊类型"));
                if (spBlurType.enumValueIndex != (int)ImageEx.BlurType.None)
                {
                    EditorGUILayout.PropertyField(spBlurIntensity, new GUIContent("模糊强度"));
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 采样增强（SAMPLING） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spSamplingMode, new GUIContent("采样模式"));
                if (spSamplingMode.enumValueIndex != (int)ImageEx.SamplingFilter.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spSamplingIntensity, new GUIContent("采样强度"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 目标模式（TARGET MODE） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spTargetMode, new GUIContent("目标模式"));
                if (spTargetMode.enumValueIndex != (int)ImageEx.TargetMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spTargetColor, new GUIContent("目标颜色"));
                    EditorGUILayout.PropertyField(spTargetRange, new GUIContent("目标范围"));
                    EditorGUILayout.PropertyField(spTargetSoftness, new GUIContent("目标柔和度"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 细节纹理（DETAIL FILTER） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spDetailMode, new GUIContent("细节滤镜"));
                if (spDetailMode.enumValueIndex != (int)ImageEx.DetailFilter.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spDetailTex, new GUIContent("纹理"));
                    EditorGUILayout.PropertyField(spDetailTexScale, new GUIContent("缩放"));
                    EditorGUILayout.PropertyField(spDetailTexOffset, new GUIContent("偏移"));
                    EditorGUILayout.PropertyField(spDetailTexSpeed, new GUIContent("速度"));
                    EditorGUILayout.PropertyField(spDetailIntensity, new GUIContent("强度"));
                    EditorGUILayout.PropertyField(spDetailThreshold, new GUIContent("阈值"));

                    // 细节颜色（HDR）
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = spDetailColor.hasMultipleDifferentValues;
                    Color newDetailColor = EditorGUILayout.ColorField(new GUIContent("颜色"), spDetailColor.colorValue, true, true, true);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck()) spDetailColor.colorValue = newDetailColor;
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 混合模式（BLEND TYPE） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spBlendType, new GUIContent("混合模式"));
            }
            EditorGUILayout.EndVertical();

            // ==================== 色调滤镜（TONE FILTER） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spToneFilter, new GUIContent("色调滤镜"));
                if (spToneFilter.enumValueIndex != (int)ImageEx.ToneFilter.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spToneIntensity, new GUIContent("滤镜强度"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 独立颜色滤镜（COLOR FILTER） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spColorFilterMode, new GUIContent("颜色滤镜"));
                if (spColorFilterMode.enumValueIndex != (int)ImageEx.ColorMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spColorValue, new GUIContent("滤镜值"));
                    EditorGUILayout.PropertyField(spColorIntensity, new GUIContent("滤镜强度"));
                    EditorGUILayout.PropertyField(spColorGlow, new GUIContent("发光"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spTransitionMode, new GUIContent("过渡模式"));
                if (spTransitionMode.enumValueIndex != (int)ImageEx.TransitionMode.None)
                {
                    ImageEx.TransitionMode mode = (ImageEx.TransitionMode)spTransitionMode.enumValueIndex;

                    EditorGUILayout.PropertyField(spTransitionRate, new GUIContent("过渡进度"));

                    if (mode == ImageEx.TransitionMode.Cutoff || mode == ImageEx.TransitionMode.Dissolve ||
                        mode == ImageEx.TransitionMode.Mask || mode == ImageEx.TransitionMode.Melt ||
                        mode == ImageEx.TransitionMode.Burn || mode == ImageEx.TransitionMode.Pattern ||
                        mode == ImageEx.TransitionMode.Fade || mode == ImageEx.TransitionMode.Shiny ||
                        mode == ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUILayout.LabelField("过渡纹理", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(spTransitionTex, new GUIContent("纹理"));
                        EditorGUILayout.PropertyField(spTransitionTexScale, new GUIContent("缩放"));
                        EditorGUILayout.PropertyField(spTransitionTexOffset, new GUIContent("偏移"));
                        EditorGUILayout.PropertyField(spTransitionSpeed, new GUIContent("速度"));
                        EditorGUILayout.PropertyField(spTransitionTexRotation, new GUIContent("旋转"));
                        EditorGUILayout.PropertyField(spTransitionKeepAspectRatio, new GUIContent("保持纵横比"));
                        EditorGUILayout.PropertyField(spTransitionReverse, new GUIContent("反向"));

                        // 过渡高级参数
                        EditorGUILayout.PropertyField(spTransitionClamp, new GUIContent("强制钳制", "对过渡 UV 做钳制，防止纹理采样越界"));
                        EditorGUILayout.PropertyField(spTransitionTexClampPadding, new GUIContent("钳制边距", "纹理钳制的额外边距（0~4）"));
                        EditorGUILayout.PropertyField(spTransitionUseUv0, new GUIContent("使用 UV0", "过渡采样使用 UV0 通道"));
                        EditorGUI.indentLevel--;
                    }

                    if (mode == ImageEx.TransitionMode.Dissolve || mode == ImageEx.TransitionMode.Shiny ||
                        mode == ImageEx.TransitionMode.Melt || mode == ImageEx.TransitionMode.Burn ||
                        mode == ImageEx.TransitionMode.Blaze || mode == ImageEx.TransitionMode.Mask)
                    {
                        EditorGUILayout.PropertyField(spTransitionWidth, new GUIContent("过渡宽度"));
                    }
                    else if (mode == ImageEx.TransitionMode.Pattern)
                    {
                        EditorGUILayout.PropertyField(spTransitionWidth, new GUIContent("图案大小"));
                    }

                    if (mode == ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(spTransitionGradientValue, new GUIContent("过渡渐变"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            serializedObject.ApplyModifiedProperties();
                            foreach (Object obj in targets)
                            {
                                ((ImageEx)obj).RefreshTransitionGradient();
                            }
                        }
                    }

                    if (mode == ImageEx.TransitionMode.Dissolve || mode == ImageEx.TransitionMode.Shiny ||
                        mode == ImageEx.TransitionMode.Melt || mode == ImageEx.TransitionMode.Burn ||
                        mode == ImageEx.TransitionMode.Mask)
                    {
                        EditorGUILayout.PropertyField(spTransitionSoftness, new GUIContent("过渡柔和度"));
                    }

                    if (mode == ImageEx.TransitionMode.Pattern)
                    {
                        Vector2 range = spTransitionRange.vector2Value;
                        float min = range.x;
                        float max = range.y;
                        EditorGUILayout.MinMaxSlider(new GUIContent("图案范围"), ref min, ref max, 0f, 1f);
                        spTransitionRange.vector2Value = new Vector2(min, max);
                    }

                    if (mode == ImageEx.TransitionMode.Pattern)
                    {
                        EditorGUILayout.PropertyField(spTransitionPatternReverse, new GUIContent("图案反向"));
                        EditorGUILayout.PropertyField(spPatternArea, new GUIContent("图案区域"));
                    }

                    if (mode != ImageEx.TransitionMode.Fade && mode != ImageEx.TransitionMode.Cutoff && mode != ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUILayout.PropertyField(spTransitionColorFilter, new GUIContent("颜色滤镜"));

                        // 过渡颜色（HDR）
                        EditorGUI.BeginChangeCheck();
                        EditorGUI.showMixedValue = spTransitionColor.hasMultipleDifferentValues;
                        Color newTransitionColor = EditorGUILayout.ColorField(new GUIContent("过渡颜色"), spTransitionColor.colorValue, true, true, true);
                        EditorGUI.showMixedValue = false;
                        if (EditorGUI.EndChangeCheck()) spTransitionColor.colorValue = newTransitionColor;

                        EditorGUILayout.PropertyField(spTransitionColorGlow, new GUIContent("发光"));
                    }

                    EditorGUILayout.PropertyField(spTransitionAutoPlaySpeed, new GUIContent("自动播放速度"));
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("Box");
            {
                // Shadow mode styled similarly to Transition Mode: a single field then indented settings.
                EditorGUILayout.PropertyField(spAppendShadow, new GUIContent("阴影模式"));
                if (spAppendShadow.boolValue)
                {
                    EditorGUILayout.LabelField("阴影设置", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(spShadowOffsetLocal, new GUIContent("阴影偏移 (本地)"));

                    // Shadow type dropdown
                    EditorGUILayout.PropertyField(spShadowMode, new GUIContent("阴影类型"));

                    // 阴影颜色（HDR）
                    SerializedProperty spShadowColor = serializedObject.FindProperty("shadowColor");
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = spShadowColor.hasMultipleDifferentValues;
                    Color newShadowColor = EditorGUILayout.ColorField(new GUIContent("阴影颜色"), spShadowColor.colorValue, true, true, true);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck()) spShadowColor.colorValue = newShadowColor;

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowBlurIntensity"), new GUIContent("阴影衰减距离"));
                    if (spShadowScale != null)
                        EditorGUILayout.PropertyField(spShadowScale, new GUIContent("阴影缩放"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("samplingWidth"), new GUIContent("采样宽度"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("samplingScale"), new GUIContent("采样缩放"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("allowOutOfBoundsShadow"), new GUIContent("允许超出边界阴影"));

                    // Show 'show source' only when ShadowMode is Mirror
                    if (!spShadowMode.hasMultipleDifferentValues && spShadowMode.intValue == (int)ImageEx.ShadowMode.Mirror)
                    {
                        EditorGUILayout.PropertyField(spShadowMirrorDirection, new GUIContent("镜像方向"));
                        EditorGUILayout.PropertyField(spShadowMirrorScale, new GUIContent("镜像缩放"));
                        EditorGUILayout.PropertyField(spShadowMirrorOffset, new GUIContent("镜像偏移"));
                        if (spShadowMirrorShowSource != null)
                            EditorGUILayout.PropertyField(spShadowMirrorShowSource, new GUIContent("显示原图"));
                        if (spShadowMirrorTintMix != null)
                            EditorGUILayout.PropertyField(spShadowMirrorTintMix, new GUIContent("镜像混合"));
                    }

                    EditorGUILayout.PropertyField(spShadowColorFilter, new GUIContent("阴影颜色滤镜"));
                    EditorGUILayout.PropertyField(spShadowColorGlow, new GUIContent("阴影发光"));

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            // ==================== 边缘效果（EDGE MODE） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spEdgeMode, new GUIContent("边缘效果"));
                if (spEdgeMode.enumValueIndex != (int)ImageEx.EdgeMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spEdgeWidth, new GUIContent("边缘宽度"));

                    EditorGUILayout.PropertyField(spEdgeColorFilterMode, new GUIContent("颜色滤镜"));

                    // 边缘颜色（HDR）
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = spEdgeColor.hasMultipleDifferentValues;
                    Color newEdgeColor = EditorGUILayout.ColorField(new GUIContent("边缘颜色"), spEdgeColor.colorValue, true, true, true);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck()) spEdgeColor.colorValue = newEdgeColor;

                    EditorGUILayout.PropertyField(spEdgeColorGlow, new GUIContent("发光"));

                    // Shiny 模式特有参数
                    if (!spEdgeMode.hasMultipleDifferentValues && spEdgeMode.enumValueIndex == (int)ImageEx.EdgeMode.Shiny)
                    {
                        EditorGUILayout.LabelField("高光设置", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(spEdgeShinyRate, new GUIContent("高光位置"));
                        EditorGUILayout.PropertyField(spEdgeShinyWidth, new GUIContent("高光宽度"));
                        EditorGUILayout.PropertyField(spEdgeShinyAutoPlaySpeed, new GUIContent("自动速度"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            if (spShape.enumValueIndex == (int)DrawShape.None)
            {
                // 翻转控件（放置在“阴影”设置下，仅在DrawShape==None时启用）
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("Box");
                {
                    EditorGUILayout.LabelField("翻转", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spFlipHorizontal, new GUIContent("水平翻转"));
                    EditorGUILayout.PropertyField(spFlipVertical, new GUIContent("垂直翻转"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            //刷新
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            Repaint();
        }
    }
}
