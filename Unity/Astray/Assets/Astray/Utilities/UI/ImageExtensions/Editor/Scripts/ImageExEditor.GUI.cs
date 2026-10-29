using System;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Astray.Utilities.UI.ImageExtensions;
using Astray.Common;

namespace Astray.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageExEditor partial part：各子面板绘制（相机画面 / 形状数据 / 旋转 / 通道 / Sprite / 图像类型 / 共享材质）。
    /// </summary>
    public partial class ImageExEditor : ImageEditor
    {

        private void CameraTextureGUI()
        {
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spCameraTexture,
                    new GUIContent("相机画面", "RenderTexture / WebCamTexture 等动态纹理，作为 _MainTex 显示，形状与特效仍然生效。"));

                if (spCameraTexture.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "留空则显示 Sprite 纹理。\n挂载 CameraToImageEx 可自动把相机画面赋给此字段。",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "警告：此字段已设置，它会完全覆盖下方的 Sprite 纹理。\n" +
                        "若该 RenderTexture 从未渲染/内容透明，ImageEx 会整体半透明不可见，且改颜色、赋精灵都无效。\n" +
                        "如果不需要相机画面，请清空此字段。",
                        MessageType.Warning);
                }
            }
            EditorGUILayout.EndVertical();
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

            // 轮廓颜色（使用 HDR ColorField）
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = spOutlineColor.hasMultipleDifferentValues;
            Color newOutlineColor = EditorGUI.ColorField(right1, new GUIContent("轮廓颜色"), spOutlineColor.colorValue, true, true, true);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) spOutlineColor.colorValue = newOutlineColor;

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
            line.x = r.x;
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

        /// <summary>
        /// 检测父 Canvas 是否缺少 ImageEx 着色器所需的附加着色器通道（TexCoord1/TexCoord2/Tangent）。
        /// 与运行时 FixAdditionalShaderChannelsInCanvas 保持同一判定逻辑。
        /// </summary>
        private void DetectMissingShaderChannels()
        {
            shaderChannelsNeedUpdate = false;
            foreach (Object obj in targets)
            {
                if (obj == null) continue;
                var img = obj as ImageEx;
                if (img == null) continue;
                var canvas = img.canvas != null ? img.canvas : img.GetComponentInParent<Canvas>();
                if (canvas == null) continue;

                AdditionalCanvasShaderChannels channels = canvas.additionalShaderChannels;
                AdditionalCanvasShaderChannels needed = channels
                    | AdditionalCanvasShaderChannels.TexCoord1
                    | AdditionalCanvasShaderChannels.TexCoord2
                    | AdditionalCanvasShaderChannels.Tangent;
                if (channels != needed)
                {
                    shaderChannelsNeedUpdate = true;
                    break;
                }
            }
        }

        private void FixShaderChannelGUI()
        {
            if (!shaderChannelsNeedUpdate) return;
            EditorGUILayout.HelpBox("父画布需要具有以下附加着色器通道：Texcoord1、Texcoord2、Tangent（缺失时主图可能透明/异常）", MessageType.Error);
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
                    // 与 DetectMissingShaderChannels 的守卫保持一致：targets 可能瞬时含 null（多选编辑中被销毁的对象），
                    // 硬转换 (ImageEx)obj 会抛 NullReferenceException，SetDirty(null) 同样无效
                    if (obj == null) continue;
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
                        // 与 DetectMissingShaderChannels 及其上方的 MaterialMode 循环保持一致（targets 可能瞬时含 null）
                        if (obj == null) continue;
                        ((ImageEx)obj).material = (Material)matObj;
                        UnityEditor.EditorUtility.SetDirty(obj);
                    }
                }

                // 反转修正：本按钮的作用是"清空自定义材质、回落到精灵材质"，因此应在**已有材质**时可用、
                // 材质已为空时禁用。原条件 `!= null` 恰好相反 ⇒ 有材质时按钮被置灰，永远点不到（功能完全不可用）。
                EditorGUI.BeginDisabledGroup(spMaterial.objectReferenceValue == null);
                {
                    if (GUILayout.Button("使用精灵材质"))
                    {
                        spMaterial.objectReferenceValue = null;

                        foreach (Object obj in targets)
                        {
                            // 与本方法前两处 targets 循环保持一致（targets 可能瞬时含 null）
                            if (obj == null) continue;
                            ((ImageEx)obj).material = null;
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
