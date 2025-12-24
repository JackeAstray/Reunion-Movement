using Codice.CM.Client.Differences.Graphic;
using System;
using System.Security.Policy;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ReunionMovement.UI.ImageExtensions.Editor
{
    [CustomEditor(typeof(ImageEx), true)]
    [CanEditMultipleObjects]
    public class ImageExEditor : ImageEditor
    {
        private SerializedProperty spSprite;
        private SerializedProperty spAppendShadow;
        private SerializedProperty spShadowOffsetLocal;
        private SerializedProperty spCircle, spTriangle, spRectangle, spPentagon, spHexagon, spChamferBox, spParallelogram, spNStarPolygon, spHeart, spBlobbyCross, spSquircle, spNTriangleRounded;
        private SerializedProperty spPreserveAspect;
        private SerializedProperty spFillMethod, spFillOrigin, spFillAmount, spFillClockwise;
        private SerializedProperty spAlphaThreshold;
        private SerializedProperty spShape;
        private SerializedProperty spStrokeWidth, spOutlineWidth, spOutlineColor, spFalloffDistance, spEnableDashedOutline, spCustomTime;
        private SerializedProperty spConstrainRotation, spShapeRotation, spFlipHorizontal, spFlipVertical;
        private SerializedProperty spMaterialSettings, spMaterial, spImageType;

        private SerializedProperty spGradient;
        private SerializedProperty spBlurType, spBlurIntensity;
        private SerializedProperty spTransitionMode, spTransitionTex, spTransitionRate, spTransitionColor, spTransitionWidth, spTransitionSoftness, spTransitionReverse;
        private SerializedProperty spTransitionTexScale, spTransitionTexOffset, spTransitionTexRotation, spTransitionKeepAspectRatio;
        private SerializedProperty spTransitionSpeed, spTransitionPatternReverse, spTransitionAutoPlaySpeed, spTransitionColorFilter, spTransitionColorGlow, spTransitionGradient, spTransitionGradientValue, spTransitionRange;

        private bool gsInitialized, shaderChannelsNeedUpdate;

        protected override void OnEnable()
        {
            foreach (Object obj in serializedObject.targetObjects)
            {
                ((ImageEx)obj).UpdateSerializedValuesFromSharedMaterial();
            }

            base.OnEnable();

            spAppendShadow = serializedObject.FindProperty("appendShadow");
            spShadowOffsetLocal = serializedObject.FindProperty("shadowOffsetLocal");

            spSprite = serializedObject.FindProperty("m_Sprite");

            spShape = serializedObject.FindProperty("drawShape");

            spStrokeWidth = serializedObject.FindProperty("strokeWidth");
            spOutlineWidth = serializedObject.FindProperty("outlineWidth");
            spOutlineColor = serializedObject.FindProperty("outlineColor");
            spFalloffDistance = serializedObject.FindProperty("falloffDistance");
            spEnableDashedOutline = serializedObject.FindProperty("enableDashedOutline");
            spCustomTime = serializedObject.FindProperty("customTime");

            spMaterialSettings = serializedObject.FindProperty("materialMode");
            spMaterial = serializedObject.FindProperty("m_Material");
            spImageType = serializedObject.FindProperty("imageType");

            spFillMethod = serializedObject.FindProperty("m_FillMethod");
            spFillOrigin = serializedObject.FindProperty("m_FillOrigin");
            spFillAmount = serializedObject.FindProperty("m_FillAmount");
            spFillClockwise = serializedObject.FindProperty("m_FillClockwise");

            spConstrainRotation = serializedObject.FindProperty("constrainRotation");
            spShapeRotation = serializedObject.FindProperty("shapeRotation");
            spFlipHorizontal = serializedObject.FindProperty("flipHorizontal");
            spFlipVertical = serializedObject.FindProperty("flipVertical");

            spAlphaThreshold = serializedObject.FindProperty("alphaThreshold");

            spCircle = serializedObject.FindProperty("circle");
            spRectangle = serializedObject.FindProperty("rectangle");
            spTriangle = serializedObject.FindProperty("triangle");
            spPentagon = serializedObject.FindProperty("pentagon");
            spHexagon = serializedObject.FindProperty("hexagon");
            spChamferBox = serializedObject.FindProperty("chamferBox");
            spParallelogram = serializedObject.FindProperty("parallelogram");
            spNStarPolygon = serializedObject.FindProperty("nStarPolygon");
            spHeart = serializedObject.FindProperty("heart");
            spBlobbyCross = serializedObject.FindProperty("blobbyCross");
            spSquircle = serializedObject.FindProperty("squircle");
            spNTriangleRounded = serializedObject.FindProperty("nTriangleRounded");

            spPreserveAspect = serializedObject.FindProperty("m_PreserveAspect");

            spGradient = serializedObject.FindProperty("gradientEffect");
            spBlurType = serializedObject.FindProperty("blurType");
            spBlurIntensity = serializedObject.FindProperty("blurIntensity");

            spTransitionMode = serializedObject.FindProperty("transitionMode");
            spTransitionTex = serializedObject.FindProperty("transitionTexture");
            spTransitionTexScale = serializedObject.FindProperty("transitionTexScale");
            spTransitionTexOffset = serializedObject.FindProperty("transitionTexOffset");
            spTransitionTexRotation = serializedObject.FindProperty("transitionTexRotation");
            spTransitionKeepAspectRatio = serializedObject.FindProperty("transitionKeepAspectRatio");
            spTransitionRate = serializedObject.FindProperty("transitionRate");
            spTransitionColor = serializedObject.FindProperty("transitionColor");
            spTransitionWidth = serializedObject.FindProperty("transitionWidth");
            spTransitionSoftness = serializedObject.FindProperty("transitionSoftness");
            spTransitionReverse = serializedObject.FindProperty("transitionReverse");
            spTransitionSpeed = serializedObject.FindProperty("transitionSpeed");
            spTransitionPatternReverse = serializedObject.FindProperty("transitionPatternReverse");
            spTransitionAutoPlaySpeed = serializedObject.FindProperty("transitionAutoPlaySpeed");
            spTransitionColorFilter = serializedObject.FindProperty("transitionColorFilter");
            spTransitionColorGlow = serializedObject.FindProperty("transitionColorGlow");
            spTransitionGradient = serializedObject.FindProperty("transitionGradient");
            spTransitionGradientValue = serializedObject.FindProperty("transitionGradientValue");
            spTransitionRange = serializedObject.FindProperty("transitionRange");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FixShaderChannelGUI();

            RaycastControlsGUI();
            EditorGUILayout.PropertyField(m_Color);
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
                        case DrawShape.Parallelogram:
                            EditorGUILayout.PropertyField(spParallelogram);
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
                            throw new ArgumentOutOfRangeException();
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
                    }

                    if (mode != ImageEx.TransitionMode.Fade && mode != ImageEx.TransitionMode.Cutoff && mode != ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUILayout.PropertyField(spTransitionColorFilter, new GUIContent("颜色滤镜"));
                        EditorGUILayout.PropertyField(spTransitionColor, new GUIContent("过渡颜色"));
                        EditorGUILayout.PropertyField(spTransitionColorGlow, new GUIContent("发光"));
                    }

                    EditorGUILayout.PropertyField(spTransitionAutoPlaySpeed, new GUIContent("自动播放速度"));
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("阴影", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(spAppendShadow, new GUIContent("启用阴影"));
                if (spAppendShadow.boolValue)
                {
                    EditorGUILayout.PropertyField(spShadowOffsetLocal, new GUIContent("阴影偏移 (本地)"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowColor"), new GUIContent("阴影颜色"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowBlurIntensity"), new GUIContent("阴影模糊强度"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("samplingWidth"), new GUIContent("采样宽度"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("samplingScale"), new GUIContent("采样缩放"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("allowOutOfBoundsShadow"), new GUIContent("允许超出边界阴影"));
                }
            }
            EditorGUILayout.EndVertical();

            //刷新
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            Repaint();
        }

        private void AdditionalShapeDataGUI()
        {
            EditorGUILayout.Space();

            float strokeWidth = spStrokeWidth.floatValue;
            float outlineWidth = spOutlineWidth.floatValue;
            float falloff = spFalloffDistance.floatValue;
            Color outlineColor = spOutlineColor.colorValue;

            float customTime = spCustomTime.floatValue;

            float h = 2;

            if (!spShape.hasMultipleDifferentValues && (spShape.enumValueIndex == (int)DrawShape.Circle || spShape.enumValueIndex == (int)DrawShape.Rectangle))
            {
                h = 3;
            }

            //EditorGUILayout.PropertyField(spStrokeWidth, new GUIContent("线条"));
            //EditorGUILayout.PropertyField(spFalloffDistance, new GUIContent("衰减"));

            // 合并为一个分组区域：多行、多列布局（第一列较宽）
            Rect groupRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight * h + EditorGUIUtility.standardVerticalSpacing);
            float gap = 6f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float vSpacing = EditorGUIUtility.standardVerticalSpacing;
            // 左右列比例，左侧更宽
            float leftWidth = (groupRect.width - gap) * 0.5f;
            float rightWidth = groupRect.width - leftWidth - gap;

            // 临时缩短标签宽度以让数值输入获得更多空间
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 100f;

            // 第一行：线条 / 衰减
            Rect left0 = new Rect(groupRect.x, groupRect.y, leftWidth, lineHeight);
            Rect right0 = new Rect(groupRect.x + leftWidth + gap, groupRect.y, rightWidth, lineHeight);
            EditorGUI.PropertyField(left0, spStrokeWidth, new GUIContent("线条"));
            EditorGUI.PropertyField(right0, spFalloffDistance, new GUIContent("衰减"));

            // 第二行：轮廓宽度 / 轮廓颜色
            float row1Y = groupRect.y + lineHeight + vSpacing;
            Rect left1 = new Rect(groupRect.x, row1Y, leftWidth, lineHeight);
            Rect right1 = new Rect(groupRect.x + leftWidth + gap, row1Y, rightWidth, lineHeight);

            // 轮廓宽度（使用 SerializedProperty）
            EditorGUI.PropertyField(left1, spOutlineWidth, new GUIContent("轮廓宽度"));

            // 轮廓颜色（使用 SerializedProperty）
            EditorGUI.PropertyField(right1, spOutlineColor, new GUIContent("轮廓颜色"));

            // 第三行（仅在 Circle/Rectangle 的情况下显示）：是否开启虚线 / 自定义时间
            if (!spShape.hasMultipleDifferentValues && (spShape.enumValueIndex == (int)DrawShape.Circle || spShape.enumValueIndex == (int)DrawShape.Rectangle))
            {
                float row2Y = groupRect.y + 2 * (lineHeight + vSpacing);
                Rect left2 = new Rect(groupRect.x, row2Y, leftWidth, lineHeight);
                Rect right2 = new Rect(groupRect.x + leftWidth + gap, row2Y, rightWidth, lineHeight);

                // 本地标签宽度，用于左列标签对齐
                float localLabelW = Mathf.Min(80f, EditorGUIUtility.labelWidth);

                // 是否开启虚线（使用 SerializedProperty）
                EditorGUI.PropertyField(left2, spEnableDashedOutline, new GUIContent("是否开启虚线"));

                // 自定义时间（右列，使用 SerializedProperty）
                EditorGUI.PropertyField(right2, spCustomTime, new GUIContent("自定义时间"));
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;

            EditorGUILayout.Space();

            RotationGUI();
        }

        private void RotationGUI()
        {
            Rect r = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 24 + EditorGUIUtility.standardVerticalSpacing);
            Rect line = r;
            line.height = EditorGUIUtility.singleLineHeight;
            float x = (line.width - 10f) / 2;

            float fieldWidth = x / 2 - 10f;
            float labelWidth = x - fieldWidth;

            line.width = labelWidth;
            EditorGUI.LabelField(line, "旋转");
            line.x += labelWidth;
            line.width = r.width - labelWidth - 78;

            string[] options = spConstrainRotation.hasMultipleDifferentValues ? new[] { "---", "---" } : new[] { "自由", "限制" };
            bool boolVal = spConstrainRotation.boolValue;
            EditorGUI.BeginChangeCheck();
            {
                boolVal = GUI.Toolbar(line, boolVal ? 1 : 0, options) == 1;
            }
            if (EditorGUI.EndChangeCheck())
            {
                spConstrainRotation.boolValue = boolVal;
                GUI.FocusControl(null);
            }

            line.x += line.width + 14;
            line.width = 64;
            EditorGUI.LabelField(line, "图像翻转");

            line.y += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            line.x = r.x + 10;
            line.height = EditorGUIUtility.singleLineHeight;
            line.width = labelWidth - 10;
            EditorGUI.BeginDisabledGroup(spConstrainRotation.boolValue);
            {
                Rect dragZone = line;
                EditorGUI.LabelField(line, "角度");
                line.x = r.x + labelWidth;
                line.width = r.width - labelWidth - 148;

                float rotationValue = spShapeRotation.floatValue;
                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.showMixedValue = spShapeRotation.hasMultipleDifferentValues;
                    rotationValue = FloatFieldDraggable.DraggableFloatField(line, spShapeRotation.floatValue);
                    EditorGUI.showMixedValue = false;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    spShapeRotation.floatValue = rotationValue;
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!spConstrainRotation.boolValue);
            {
                line.x += line.width + 4;
                line.width = 30;
                line.height = 24;
                if (GUI.Button(line, EditorContents.RotateLeftNormal))
                {
                    float rotation = spShapeRotation.floatValue;
                    float remainder = rotation % 90;
                    if (Mathf.Abs(remainder) <= 0)
                    {
                        rotation += 90;
                    }
                    else
                    {
                        rotation = rotation - remainder + 90;
                    }
                    if (Math.Abs(rotation) >= 360) rotation = 0;
                    spShapeRotation.floatValue = rotation;
                }

                line.x += 34;
                if (GUI.Button(line, EditorContents.RotateRightNormal))
                {
                    float rotation = spShapeRotation.floatValue;
                    float remainder = rotation % 90;
                    if (Mathf.Abs(remainder) <= 0)
                    {
                        rotation -= 90;
                    }
                    else
                    {
                        rotation -= remainder;
                    }

                    if (Math.Abs(rotation) >= 360) rotation = 0;
                    spShapeRotation.floatValue = rotation;
                }
            }
            EditorGUI.EndDisabledGroup();

            line.x += 46;
            bool flipH = spFlipHorizontal.boolValue;
            bool flipV = spFlipVertical.boolValue;
            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.BeginDisabledGroup(spFlipHorizontal.hasMultipleDifferentValues || spFlipVertical.hasMultipleDifferentValues);
                flipH = GUI.Toggle(line, spFlipHorizontal.boolValue, spFlipHorizontal.boolValue ? EditorContents.FlipHorizontalActive : EditorContents.FlipHorizontalNormal, "button");
                line.x += 34;
                flipV = GUI.Toggle(line, spFlipVertical.boolValue, spFlipVertical.boolValue ? EditorContents.FlipVerticalActive : EditorContents.FlipVerticalNormal, "button");
                EditorGUI.EndDisabledGroup();
            }
            if (EditorGUI.EndChangeCheck())
            {
                spFlipHorizontal.boolValue = flipH;
                spFlipVertical.boolValue = flipV;
            }

        }

        private void FixShaderChannelGUI()
        {
            if (!shaderChannelsNeedUpdate) return;
            EditorGUILayout.HelpBox("父画布需要具有以下附加着色器通道：Texcord1、Texcord2", MessageType.Error);
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fix", GUILayout.Width(100)))
                {
                    Canvas canvas = (target as ImageEx)?.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        EditorUtility.AddAdditionalShaderChannelsToCanvas(canvas);
                        shaderChannelsNeedUpdate = false;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private new void SpriteGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(spSprite, new GUIContent("精灵"));
            if (EditorGUI.EndChangeCheck())
            {
                Sprite newSprite = spSprite.objectReferenceValue as Sprite;
                if (newSprite)
                {
                    Image.Type oldType = (Image.Type)spImageType.enumValueIndex;
                    if (newSprite.border.SqrMagnitude() > 0)
                    {
                        spImageType.enumValueIndex = (int)Image.Type.Sliced;
                    }
                    else if (oldType == Image.Type.Sliced)
                    {
                        spImageType.enumValueIndex = (int)Image.Type.Simple;
                    }
                }

                (serializedObject.targetObject as Image)?.DisableSpriteOptimizations();
            }

            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.Slider(rect, spAlphaThreshold, 0f, 1f, "Alpha阈值");
        }

        private void ImageTypeGUI()
        {
            int selectedIndex = spImageType.enumValueIndex == (int)Image.Type.Simple ? 0 : 1;
            Rect imageTypeRect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.LabelField(
                    new Rect(imageTypeRect.x, imageTypeRect.y, EditorGUIUtility.labelWidth, imageTypeRect.height),
                    "类型");
                imageTypeRect.x += EditorGUIUtility.labelWidth + 2;
                imageTypeRect.width -= EditorGUIUtility.labelWidth + 2;
                selectedIndex = EditorGUI.Popup(imageTypeRect, selectedIndex, new[] { "简单", "填充" });
            }
            if (EditorGUI.EndChangeCheck())
            {
                spImageType.enumValueIndex = (int)(selectedIndex == 0 ? Image.Type.Simple : Image.Type.Filled);
            }

            if (!spImageType.hasMultipleDifferentValues && spImageType.enumValueIndex == (int)Image.Type.Filled)
            {
                ++EditorGUI.indentLevel;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(spFillMethod);
                if (EditorGUI.EndChangeCheck())
                {
                    spFillOrigin.intValue = 0;
                }

                switch ((Image.FillMethod)spFillMethod.enumValueIndex)
                {
                    case Image.FillMethod.Horizontal:
                        spFillOrigin.intValue = (int)(Image.OriginHorizontal)EditorGUILayout.EnumPopup("填充原点",
                            (Image.OriginHorizontal)spFillOrigin.intValue);
                        break;
                    case Image.FillMethod.Vertical:
                        spFillOrigin.intValue = (int)(Image.OriginVertical)EditorGUILayout.EnumPopup("填充原点",
                            (Image.OriginVertical)spFillOrigin.intValue);
                        break;
                    case Image.FillMethod.Radial90:
                        spFillOrigin.intValue =
                            (int)(Image.Origin90)EditorGUILayout.EnumPopup("填充原点",
                                (Image.Origin90)spFillOrigin.intValue);
                        break;
                    case Image.FillMethod.Radial180:
                        spFillOrigin.intValue =
                            (int)(Image.Origin180)EditorGUILayout.EnumPopup("填充原点",
                                (Image.Origin180)spFillOrigin.intValue);
                        break;
                    case Image.FillMethod.Radial360:
                        spFillOrigin.intValue =
                            (int)(Image.Origin360)EditorGUILayout.EnumPopup("填充原点",
                                (Image.Origin360)spFillOrigin.intValue);
                        break;
                }

                EditorGUILayout.PropertyField(spFillAmount);
                if ((Image.FillMethod)spFillMethod.enumValueIndex > Image.FillMethod.Vertical)
                {
                    EditorGUILayout.PropertyField(spFillClockwise, new GUIContent("顺时针"));
                }

                --EditorGUI.indentLevel;
            }
        }

        private void SharedMaterialGUI()
        {
            Rect rect = EditorGUILayout.GetControlRect(true,
                EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
            int matSett = spMaterialSettings.enumValueIndex;
            Rect labelRect = rect;
            labelRect.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(labelRect, "材质模式");
            rect.x += labelRect.width;
            rect.width -= labelRect.width;

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = spMaterialSettings.hasMultipleDifferentValues;
            string[] options = new[] { "动态", "共享" };
            if (EditorGUI.showMixedValue) options = new[] { "---", "---" };
            matSett = GUI.Toolbar(rect, matSett, options);

            if (EditorGUI.EndChangeCheck())
            {
                spMaterialSettings.enumValueIndex = matSett;
                foreach (Object obj in targets)
                {
                    ((ImageEx)obj).MaterialMode = (MaterialMode)matSett;
                    UnityEditor.EditorUtility.SetDirty(obj);
                }
            }

            EditorGUI.showMixedValue = false;


            EditorGUI.BeginDisabledGroup(spMaterialSettings.enumValueIndex == (int)MaterialMode.Dynamic);
            {
                rect = EditorGUILayout.GetControlRect(true,
                    EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

                Object matObj = spMaterial.objectReferenceValue;

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.showMixedValue = spMaterialSettings.hasMultipleDifferentValues;
                    matObj = (Material)EditorGUI.ObjectField(
                        new Rect(rect.x, rect.y, rect.width - 60, EditorGUIUtility.singleLineHeight),
                        matObj, typeof(Material), false);
                    EditorGUI.showMixedValue = false;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    spMaterial.objectReferenceValue = matObj;
                    foreach (Object obj in targets)
                    {
                        ((ImageEx)obj).material = (Material)matObj;
                        UnityEditor.EditorUtility.SetDirty(obj);
                    }
                }

                EditorGUI.BeginDisabledGroup(spMaterial.objectReferenceValue != null);
                {
                    if (GUI.Button(new Rect(rect.x + rect.width - 55, rect.y, 55, EditorGUIUtility.singleLineHeight), "创建"))
                    {
                        Material mat = ((ImageEx)target).CreateMaterialAssetFromComponentSettings();
                        spMaterial.objectReferenceValue = mat;
                        foreach (Object obj in targets)
                        {
                            ((ImageEx)obj).material = mat;
                            UnityEditor.EditorUtility.SetDirty(obj);
                        }
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}