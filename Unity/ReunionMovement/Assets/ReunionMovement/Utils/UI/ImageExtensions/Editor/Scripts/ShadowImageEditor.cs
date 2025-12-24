using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions.Editor
{
    [CustomEditor(typeof(ReunionMovement.UI.ImageExtensions.ShadowImage), true)]
    [CanEditMultipleObjects]
    public class ShadowImageEditor : ImageEditor
    {
        private SerializedProperty spAppendShadow;
        private SerializedProperty spShadowOffsetLocal;

        protected override void OnEnable()
        {
            base.OnEnable();
            spAppendShadow = serializedObject.FindProperty("appendShadow");
            spShadowOffsetLocal = serializedObject.FindProperty("shadowOffsetLocal");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // draw default Image inspector
            base.OnInspectorGUI();

            // draw ShadowImage specific properties
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ShadowImage", EditorStyles.boldLabel);
            if (spAppendShadow != null) EditorGUILayout.PropertyField(spAppendShadow);
            if (spShadowOffsetLocal != null) EditorGUILayout.PropertyField(spShadowOffsetLocal);

            serializedObject.ApplyModifiedProperties();
            Repaint();
        }
    }
}
