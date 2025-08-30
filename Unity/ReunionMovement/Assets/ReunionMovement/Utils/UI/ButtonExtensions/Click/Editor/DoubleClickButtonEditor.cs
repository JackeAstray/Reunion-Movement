using ReunionMovement.UI.ButtonClick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace ReunionMovement.EditorTools
{
    [CustomEditor(typeof(DoubleClickButton))]
    public class DoubleClickButtonEditor : UnityEditor.UI.ButtonEditor
    {
        SerializedProperty enableInput;
        SerializedProperty enableKeyboard;
        SerializedProperty enableGamepad;

        SerializedProperty keyboardTriggerKeys;
        SerializedProperty gamepadTriggerButtons;

        protected override void OnEnable()
        {
            base.OnEnable();

            enableInput = serializedObject.FindProperty("enableInput");
            enableKeyboard = serializedObject.FindProperty("enableKeyboard");
            enableGamepad = serializedObject.FindProperty("enableGamepad");

            keyboardTriggerKeys = serializedObject.FindProperty("keyboardTriggerKeys");
            gamepadTriggerButtons = serializedObject.FindProperty("gamepadTriggerButtons");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(enableInput, true);
            EditorGUILayout.PropertyField(enableKeyboard, true);
            EditorGUILayout.PropertyField(enableGamepad, true);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(keyboardTriggerKeys, true);
            EditorGUILayout.PropertyField(gamepadTriggerButtons, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
