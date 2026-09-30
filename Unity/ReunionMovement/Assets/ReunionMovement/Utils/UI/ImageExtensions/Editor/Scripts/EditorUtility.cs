using ReunionMovement.Core.Languages;
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ReunionMovement.UI.ImageExtensions;

namespace ReunionMovement.EditorTools.ImageExtensions
{
    public static class EditorUtility
    {
        [MenuItem("GameObject/UI/ImageEx")]
        public static void AddImageExObject()
        {
            GameObject g = new GameObject { name = "ImageEx" };

            Transform parent = GetParentTransform();
            g.transform.SetParent(parent, false);
            g.AddComponent<ImageEx>();

            // 新物体默认 RectTransform 尺寸为 (0,0)，会导致 ImageEx 没有任何绘制面积，
            // 在场景/游戏视图中看起来就像“透明”，此时修改 Color 也毫无效果。
            // 这里补一个默认尺寸（与 Unity 内置 UI/Image 的 100x100 一致）。
            RectTransform rt = g.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(100f, 100f);
            }

            Selection.activeGameObject = g;

            Undo.RegisterCreatedObjectUndo(g, "ImageEx Created");
            UnityEditor.EditorUtility.SetDirty(g);
        }

        /// <summary>
        /// 获取当前选中的物体的父物体
        /// </summary>
        /// <returns></returns>
        private static Transform GetParentTransform()
        {
            Transform parent;
            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.GetComponentInParent<Canvas>() != null)
            {
                parent = Selection.activeGameObject.transform;
            }
            else
            {
                Canvas c = GetCanvas();
                // GetCanvas 在"场景无 Canvas 且菜单创建未产出"时可能返回 null（预制体编辑阶段、菜单路径异常等），
                // 此时原代码会对 c 连续解引用两次（AddAdditionalShaderChannelsToCanvas 内部读 c.additionalShaderChannels
                // 与 c.transform）而抛 NRE —— 且发生在 AddImageExObject 已 new GameObject 之后，会留下"孤儿对象 + 异常"。
                // 改为报错并返回 null：调用方用 SetParent(null, false)（合法）把对象留于场景根，不再中途崩溃。
                if (c == null)
                {
                    UnityEngine.Debug.LogError("GetParentTransform: 未找到 Canvas 且自动创建失败，ImageEx 将置于场景根");
                    return null;
                }
                AddAdditionalShaderChannelsToCanvas(c);
                parent = c.transform;
            }

            return parent;
        }

        /// <summary>
        /// 获取当前场景的画布
        /// </summary>
        /// <returns></returns>
        private static Canvas GetCanvas()
        {
            StageHandle handle = StageUtility.GetCurrentStageHandle();
            if (!handle.FindComponentOfType<Canvas>())
            {
                EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
            }

            Canvas c = handle.FindComponentOfType<Canvas>();
            return c;
        }

        [MenuItem("CONTEXT/Image/替换为ImageEx")]
        public static void ReplaceWithImageEx(MenuCommand command)
        {
            if (command.context is ImageEx)
            {
                return;
            }

            Image img = (Image)command.context;
            GameObject obj = img.gameObject;
            // 改用 Undo.* 版本：销毁与添加的最终状态与原来完全一致，唯一差别是动作变为可撤销。
            // 原写法（DestroyImmediate + AddComponent）是不可撤销的破坏性操作，误点后无法 Ctrl+Z 恢复。
            Undo.DestroyObjectImmediate(img);
            Undo.AddComponent<ImageEx>(obj);
            UnityEditor.EditorUtility.SetDirty(obj);

        }

        internal static void AddAdditionalShaderChannelsToCanvas(Canvas c)
        {
            AdditionalCanvasShaderChannels additionalShaderChannels = c.additionalShaderChannels;
            additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
            additionalShaderChannels |= AdditionalCanvasShaderChannels.Tangent;
            c.additionalShaderChannels = additionalShaderChannels;
        }

        internal static bool HasAdditionalShaderChannels(Canvas c)
        {
            AdditionalCanvasShaderChannels asc = c.additionalShaderChannels;
            // 必须与 AddAdditionalShaderChannelsToCanvas 添加的通道集合完全一致：
            // 该方法会开启 TexCoord1、TexCoord2 与 Tangent 三项，此前这里漏检 Tangent，
            // 导致"只有 TexCoord1/2 而缺 Tangent"的 Canvas 被误判为已就绪，从而永远不会补上 Tangent
            return (asc & AdditionalCanvasShaderChannels.TexCoord1) != 0 &&
                   (asc & AdditionalCanvasShaderChannels.TexCoord2) != 0 &&
                   (asc & AdditionalCanvasShaderChannels.Tangent) != 0;
        }

        public static void CornerRadiusModeGUI(Rect rect, ref SerializedProperty property, string[] toolBarHeading, string label = "圆角半径")
        {
            bool boolVal = property.boolValue;
            Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            Rect toolBarRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y,
                rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                EditorGUI.LabelField(labelRect, label);

                boolVal = GUI.Toolbar(toolBarRect, boolVal ? 1 : 0, toolBarHeading) == 1;
                EditorGUI.showMixedValue = false;
            }
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = boolVal;
            }
        }

        private static Sprite emptySprite;

        internal static Sprite EmptySprite
        {
            get
            {
                if (emptySprite == null)
                {
                    emptySprite = Resources.Load<Sprite>("UI/Sprites/default_empty_sprite");
                }

                return emptySprite;
            }
        }

        /// <summary>
        /// 查找图像扩展根目录
        /// </summary>
        /// <returns></returns>
        internal static string FindImageExtensionsRootDirectory()
        {
            string path = "Assets/ReunionMovement/Resources/UI";

            if (string.IsNullOrEmpty(path))
            {
                return String.Empty;
            }

            string[] directories = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < directories.Length; i++)
            {
                sb.Append(directories[i]);
                sb.Append(Path.DirectorySeparatorChar);
                if (directories[i].Equals("ImageExtensions", StringComparison.OrdinalIgnoreCase))
                    break;
            }
            return sb.ToString();
        }

        [MenuItem("CONTEXT/Text/添加UIText脚本")]
        private static void AddUITextToText(MenuCommand command)
        {
            var text = command.context as Text;
            if (text != null && text.gameObject.GetComponent<UIText>() == null)
            {
                text.gameObject.AddComponent<UIText>();
                UnityEditor.EditorUtility.SetDirty(text.gameObject);
            }
        }

        [MenuItem("CONTEXT/TMP_Text/添加UIText脚本")]
        private static void AddUITextToTMPText(MenuCommand command)
        {
            var tmpText = command.context as TMP_Text;
            if (tmpText != null && tmpText.gameObject.GetComponent<UIText>() == null)
            {
                tmpText.gameObject.AddComponent<UIText>();
                UnityEditor.EditorUtility.SetDirty(tmpText.gameObject);
            }
        }
    }
}