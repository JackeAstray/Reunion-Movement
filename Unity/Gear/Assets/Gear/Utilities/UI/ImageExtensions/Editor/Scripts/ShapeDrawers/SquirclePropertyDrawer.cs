using Gear.Utilities.UI.ImageExtensions;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Gear.Common;
namespace Gear.EditorTools.ImageExtensions
{
    [CustomPropertyDrawer(typeof(SquircleImg))]
    public class SquirclePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            {
                SerializedProperty squircleTime = property.FindPropertyRelative("squircleTime");

                Rect line = position;
                line.height = EditorGUIUtility.singleLineHeight;

                EditorGUI.Slider(line, squircleTime, 1f, 1.5f, "SquircleTime");
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.EndProperty();
        }
    }
}