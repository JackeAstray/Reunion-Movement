using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions.Editor
{
    [CustomPropertyDrawer(typeof(QuadrilateralImg))]
    public class QuadrilateralPropertyDrawer : PropertyDrawer
    {
        private const float LineHeight = 18f;
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            {
                SerializedProperty topLeft = property.FindPropertyRelative("topLeftOffset");
                SerializedProperty topRight = property.FindPropertyRelative("topRightOffset");
                SerializedProperty bottomLeft = property.FindPropertyRelative("bottomLeftOffset");
                SerializedProperty bottomRight = property.FindPropertyRelative("bottomRightOffset");

                Rect line = position;
                line.height = EditorGUIUtility.singleLineHeight;

                EditorGUI.LabelField(line, label);
                line.y += LineHeight + Spacing;

                DrawOffsetField(line, "左上偏移", topLeft);
                line.y += LineHeight + Spacing;

                DrawOffsetField(line, "右上偏移", topRight);
                line.y += LineHeight + Spacing;

                DrawOffsetField(line, "左下偏移", bottomLeft);
                line.y += LineHeight + Spacing;

                DrawOffsetField(line, "右下偏移", bottomRight);
            }
            EditorGUI.EndProperty();
        }

        private void DrawOffsetField(Rect rect, string fieldLabel, SerializedProperty offset)
        {
            Rect labelRect = new Rect(rect.x, rect.y, 70f, rect.height);
            Rect fieldRect = new Rect(rect.x + 74f, rect.y, rect.width - 74f, rect.height);

            EditorGUI.LabelField(labelRect, fieldLabel);
            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.showMixedValue = offset.hasMultipleDifferentValues;
                offset.vector2Value = EditorGUI.Vector2Field(fieldRect, GUIContent.none, offset.vector2Value);
                EditorGUI.showMixedValue = false;
            }
            EditorGUI.EndChangeCheck();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return LineHeight * 5 + Spacing * 4;
        }
    }
}
