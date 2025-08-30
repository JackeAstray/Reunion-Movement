using UnityEditor;
using UnityEngine;
using ReunionMovement.UI.ButtonAnimated;

namespace ReunionMovement.EditorTools
{
    [CustomEditor(typeof(ButtonAni))]
    public class ButtonAniEditor : UnityEditor.UI.ButtonEditor
    {
        SerializedProperty normal;
        SerializedProperty highlighted;
        SerializedProperty pressed;
        SerializedProperty selected;
        SerializedProperty disabled;
        SerializedProperty transitionDuration;

        SerializedProperty enableInput;
        SerializedProperty enableKeyboard;
        SerializedProperty enableGamepad;

        SerializedProperty keyboardTriggerKeys;
        SerializedProperty gamepadTriggerButtons;

        protected override void OnEnable()
        {
            base.OnEnable();
            normal = serializedObject.FindProperty("normal");
            highlighted = serializedObject.FindProperty("highlighted");
            pressed = serializedObject.FindProperty("pressed");
            selected = serializedObject.FindProperty("selected");
            disabled = serializedObject.FindProperty("disabled");
            transitionDuration = serializedObject.FindProperty("transitionDuration");

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
            EditorGUILayout.LabelField("ButtonAni 动画设置", EditorStyles.boldLabel);
            var interactableProp = serializedObject.FindProperty("m_Interactable");
            bool interactable = interactableProp == null || interactableProp.boolValue;
            EditorGUI.BeginDisabledGroup(!interactable);
            EditorGUILayout.PropertyField(transitionDuration);
            EditorGUILayout.PropertyField(normal, true);
            EditorGUILayout.PropertyField(highlighted, true);
            EditorGUILayout.PropertyField(pressed, true);
            EditorGUILayout.PropertyField(selected, true);
            EditorGUILayout.PropertyField(disabled, true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(enableInput, true);
            EditorGUILayout.PropertyField(enableKeyboard, true);
            EditorGUILayout.PropertyField(enableGamepad, true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(keyboardTriggerKeys, true);
            EditorGUILayout.PropertyField(gamepadTriggerButtons, true);
            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}