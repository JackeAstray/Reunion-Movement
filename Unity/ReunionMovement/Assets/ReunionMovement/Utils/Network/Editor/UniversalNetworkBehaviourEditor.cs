using UnityEditor;
using UnityEngine;

namespace ReunionMovement.Common.Util.EditorTools
{
    [CustomEditor(typeof(UniversalNetworkBehaviour))]
    public class UniversalNetworkBehaviourEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var t = target as UniversalNetworkBehaviour;
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(8);
            GUILayout.Label("快速控制", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                if (t.Client != null)
                {
                    EditorGUILayout.LabelField("客户端状态", t.Client.State.ToString());
                }
                if (t.Server != null)
                {
                    EditorGUILayout.LabelField("服务端状态",
                        t.Server.IsActive ? $"运行中（{t.Server.ClientCount} 个连接）" : "未运行");
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("启动"))
            {
                if (!Application.isPlaying) { Log.Warning("启动操作需要进入播放模式"); }
                else
                {
                    t.StartAsConfigured();
                }
            }
            if (GUILayout.Button("停止"))
            {
                if (!Application.isPlaying) { Log.Warning("停止操作需要进入播放模式"); }
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
                if (!Application.isPlaying) { Log.Warning("发送操作需要进入播放模式"); }
                else
                {
                    t.SendClientString(t.inspectorSendText);
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
