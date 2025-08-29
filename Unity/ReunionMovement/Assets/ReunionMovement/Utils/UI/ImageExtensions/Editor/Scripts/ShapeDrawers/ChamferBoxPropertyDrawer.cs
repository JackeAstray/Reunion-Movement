using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions.Editor
{
    [CustomPropertyDrawer(typeof(ChamferBoxImg))]
    public class ChamferBoxPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            {
                SerializedProperty chamferBoxSize = property.FindPropertyRelative("chamferBoxSize");
                SerializedProperty chamferBoxRadius = property.FindPropertyRelative("chamferBoxRadius");

                Vector2 chamferBoxSizeVectorValue = chamferBoxSize.vector2Value;
                Vector4 chamferBoxRadiusValue = chamferBoxRadius.vector4Value;

                Rect line = position;
                line.height = EditorGUIUtility.singleLineHeight;

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.showMixedValue = chamferBoxSize.hasMultipleDifferentValues;
                    chamferBoxSizeVectorValue = EditorGUI.Vector2Field(line, "宽高", chamferBoxSizeVectorValue);
                    EditorGUI.showMixedValue = false;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    chamferBoxSize.vector2Value = chamferBoxSizeVectorValue;
                }

                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.showMixedValue = chamferBoxRadius.hasMultipleDifferentValues;
                    chamferBoxRadiusValue = EditorGUI.Vector4Field(line, "四角倒角", chamferBoxRadiusValue);
                    EditorGUI.showMixedValue = false;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    chamferBoxRadius.vector4Value = chamferBoxRadiusValue;
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing * 1;
        }
    }
}
