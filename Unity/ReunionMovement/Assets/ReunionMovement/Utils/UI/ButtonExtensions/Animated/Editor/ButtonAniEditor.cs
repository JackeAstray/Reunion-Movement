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

        protected override void OnEnable()
        {
            base.OnEnable();
            normal = serializedObject.FindProperty("normal");
            highlighted = serializedObject.FindProperty("highlighted");
            pressed = serializedObject.FindProperty("pressed");
            selected = serializedObject.FindProperty("selected");
            disabled = serializedObject.FindProperty("disabled");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
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
            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}