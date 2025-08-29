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

        protected override void OnEnable()
        {
            base.OnEnable();

            progressBar = serializedObject.FindProperty("progressBar");
            longPressDuration = serializedObject.FindProperty("longPressDuration");
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}