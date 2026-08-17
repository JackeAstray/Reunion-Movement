using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ReunionMovement.UI.ButtonClick;

namespace ReunionMovement.EditorTools
{
    [CustomEditor(typeof(LongClickButton))]
    public class LongClickButtonEditor : UnityEditor.UI.ButtonEditor
    {
        SerializedProperty progressBar;
        SerializedProperty longPressDuration;

        SerializedProperty enableInput;
        SerializedProperty enableKeyboard;
        SerializedProperty enableGamepad;

        SerializedProperty keyboardTriggerKeys;
        SerializedProperty gamepadTriggerButtons;

        protected override void OnEnable()
        {
            base.OnEnable();

            progressBar = serializedObject.FindProperty("progressBar");
            longPressDuration = serializedObject.FindProperty("longPressDuration");

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
            EditorGUILayout.LabelField("进度条", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(progressBar);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("长按判定时长 (秒)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(longPressDuration);
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