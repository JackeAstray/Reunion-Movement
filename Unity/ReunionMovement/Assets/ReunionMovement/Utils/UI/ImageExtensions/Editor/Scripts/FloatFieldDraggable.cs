using UnityEditor;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions.Editor
{
    /// <summary>
    /// float字段可拖动
    /// </summary>
    public class FloatFieldDraggable
    {
        public static float DraggableFloatField(Rect fieldRect, float value, float dragSensitivity = 0.02f)
        {
            // 拖拽逻辑（仅在输入框左侧小区域）
            Rect dragRect = new Rect(fieldRect.x - 200, fieldRect.y, 200, fieldRect.height);
            int id = GUIUtility.GetControlID(FocusType.Passive, dragRect);
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && dragRect.Contains(evt.mousePosition))
            {
                GUIUtility.hotControl = id;
                EditorGUIUtility.SetWantsMouseJumping(1);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                value += evt.delta.x * dragSensitivity;
                GUI.changed = true;
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                EditorGUIUtility.SetWantsMouseJumping(0);
                evt.Use();
            }

            // 绘制 float 输入框
            value = EditorGUI.FloatField(fieldRect, value);

            return value;
        }
    }
}