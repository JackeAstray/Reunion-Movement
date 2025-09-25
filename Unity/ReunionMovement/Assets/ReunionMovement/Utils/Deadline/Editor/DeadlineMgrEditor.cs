using ReunionMovement.Common.Util;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools
{
    [CustomEditor(typeof(DeadlineMgr))]
    public class DeadlineMgrEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);
            if (GUILayout.Button("清除截止日期本地数据 (PlayerPrefs)", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("清除本地数据", "确定要清除 DeadlineMgr 的本地 PlayerPrefs 数据吗？", "确定", "取消"))
                {
                    PlayerPrefs.DeleteKey("Deadline_LastUtcTicks_v1");
                    PlayerPrefs.DeleteKey("Deadline_LastUtcHash_v1");
                    PlayerPrefs.Save();
                    EditorUtility.DisplayDialog("完成", "已清除 DeadlineMgr 的本地数据。", "OK");
                }
            }
        }
    }
}