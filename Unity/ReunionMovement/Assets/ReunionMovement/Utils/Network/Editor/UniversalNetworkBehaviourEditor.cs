using UnityEditor;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    [CustomEditor(typeof(UniversalNetworkBehaviour))]
    public class UniversalNetworkBehaviourEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var t = target as UniversalNetworkBehaviour;
            DrawDefaultInspector();

            GUILayout.Space(8);
            GUILayout.Label("快速控制", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("启动"))
            {
                if (!Application.isPlaying) { Debug.LogWarning("启动操作需要进入播放模式。"); }
                else
                {
                    t.StartAsConfigured();
                }
            }
            if (GUILayout.Button("停止"))
            {
                if (!Application.isPlaying) { Debug.LogWarning("停止操作需要进入播放模式。"); }
                else
                {
                    t.StopAll();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("发送文本:", GUILayout.Width(70));
            t.inspectorSendText = GUILayout.TextField(t.inspectorSendText);
            if (GUILayout.Button("发送"))
            {
                if (!Application.isPlaying) { Debug.LogWarning("发送操作需要进入播放模式。"); }
                else
                {
                    t.SendClientString(t.inspectorSendText);
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
