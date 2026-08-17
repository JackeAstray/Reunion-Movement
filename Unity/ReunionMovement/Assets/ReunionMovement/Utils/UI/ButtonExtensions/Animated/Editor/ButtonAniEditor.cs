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
        SerializedProperty easeCurve;
        SerializedProperty usePunch;
        SerializedProperty punchStrength;
        SerializedProperty audioSource;
        SerializedProperty audioVolume;
        SerializedProperty onStateChanged;

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
            easeCurve = serializedObject.FindProperty("easeCurve");
            usePunch = serializedObject.FindProperty("usePunch");
            punchStrength = serializedObject.FindProperty("punchStrength");
            audioSource = serializedObject.FindProperty("audioSource");
            audioVolume = serializedObject.FindProperty("audioVolume");
            onStateChanged = serializedObject.FindProperty("onStateChanged");

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
            EditorGUILayout.PropertyField(easeCurve);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(normal, true);
            EditorGUILayout.PropertyField(highlighted, true);
            EditorGUILayout.PropertyField(pressed, true);
            EditorGUILayout.PropertyField(selected, true);
            EditorGUILayout.PropertyField(disabled, true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(usePunch);
            if (usePunch.boolValue)
            {
                EditorGUILayout.PropertyField(punchStrength);
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(audioSource);
            EditorGUILayout.PropertyField(audioVolume);
            EditorGUILayout.PropertyField(onStateChanged);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(enableInput, true);
            EditorGUILayout.PropertyField(enableKeyboard, true);
            EditorGUILayout.PropertyField(enableGamepad, true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(keyboardTriggerKeys, true);
            EditorGUILayout.PropertyField(gamepadTriggerButtons, true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("状态预览（编辑器即时生效，仅修改运行时视觉）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("普通")) Preview(ButtonAniState.Normal);
            if (GUILayout.Button("高亮")) Preview(ButtonAniState.Highlighted);
            if (GUILayout.Button("按下")) Preview(ButtonAniState.Pressed);
            if (GUILayout.Button("选中")) Preview(ButtonAniState.Selected);
            if (GUILayout.Button("禁用")) Preview(ButtonAniState.Disabled);
            EditorGUILayout.EndHorizontal();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 编辑器预览：ApplyState 在非播放模式下走同步路径（无动画）。
        /// force 确保重复点击同一状态也会重新应用。
        /// </summary>
        private void Preview(ButtonAniState state)
        {
            var btn = (ButtonAni)target;
            btn.ApplyState(state, instant: true, force: true);
            EditorUtility.SetDirty(btn);
        }
    }
}