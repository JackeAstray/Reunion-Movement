using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Toolbox
{
    /// <summary>检查：可筛选、可分组、可复制的一页式体检报告。</summary>
    public sealed partial class ReunionMovementToolboxWindow
    {
        private static readonly DiagnosticCategory[] CategoryOrder =
        {
            DiagnosticCategory.Environment,
            DiagnosticCategory.Package,
            DiagnosticCategory.Project,
            DiagnosticCategory.Config,
            DiagnosticCategory.Addressables,
            DiagnosticCategory.Docs,
        };

        private void DrawDiagnosticsTab()
        {
            SectionHeader("项目检查", "只读：不会修改任何资产或 Package 配置。");

            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                bool onlyProblemsNow = EditorGUILayout.ToggleLeft("只看问题", onlyProblems, GUILayout.Width(80f));
                if (onlyProblemsNow != onlyProblems)
                {
                    onlyProblems = onlyProblemsNow;
                    EditorPrefs.SetBool("ReunionMovement.Toolbox.OnlyProblems", onlyProblems);
                }

                if (GUILayout.Button("复制报告", GUILayout.Width(84f))) CopyReport();
            }

            int shown = 0;
            foreach (DiagnosticCategory category in CategoryOrder)
            {
                List<DiagnosticItem> items = Filter(category);
                if (items.Count == 0) continue;

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(CategoryTitle(category) + "（" + items.Count + "）", EditorStyles.boldLabel);
                foreach (DiagnosticItem item in items)
                {
                    DrawDiagnosticRow(item, false);
                    shown++;
                }
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox(onlyProblems ? "没有匹配的问题项。取消「只看问题」可以查看全部检查项。"
                    : "没有匹配项，试试清空搜索框。", MessageType.Info);
            }
        }

        private List<DiagnosticItem> Filter(DiagnosticCategory category)
        {
            var result = new List<DiagnosticItem>();
            foreach (DiagnosticItem item in diagnostics)
            {
                if (item.Category != category) continue;
                if (onlyProblems && item.Level == DiagnosticLevel.Passed) continue;
                if (!Matches(item, search)) continue;
                result.Add(item);
            }
            return result;
        }

        private static bool Matches(DiagnosticItem item, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return true;
            return Contains(item.Title, keyword) || Contains(item.Message, keyword) || Contains(item.ActionLabel, keyword);
        }

        private static bool Contains(string text, string keyword)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawDiagnosticRow(DiagnosticItem item, bool compact)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(LevelGlyph(item.Level), GUILayout.Width(16f));
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(item.Title, EditorStyles.boldLabel);
                    if (!compact)
                    {
                        EditorGUILayout.LabelField(item.Message, EditorStyles.wordWrappedMiniLabel);
                    }
                }

                if (item.Action != null && !string.IsNullOrEmpty(item.ActionLabel) &&
                    GUILayout.Button(item.ActionLabel, GUILayout.Width(96f), GUILayout.Height(32f)))
                {
                    item.Action();
                }
            }
        }

        private void CopyReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine("ReunionMovement 项目检查 " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("Unity " + Application.unityVersion + "  错误 " + CountLevel(DiagnosticLevel.Error) +
                               " / 提醒 " + CountLevel(DiagnosticLevel.Warning) + " / 通过 " + CountLevel(DiagnosticLevel.Passed));

            foreach (DiagnosticCategory category in CategoryOrder)
            {
                foreach (DiagnosticItem item in diagnostics)
                {
                    if (item.Category != category) continue;
                    builder.AppendLine("[" + LevelGlyph(item.Level) + "] " + CategoryTitle(category) + " · " + item.Title);
                    builder.AppendLine("      " + item.Message);
                }
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
            ShowNotification(new GUIContent("报告已复制到剪贴板"));
        }

        private static string CategoryTitle(DiagnosticCategory category)
        {
            switch (category)
            {
                case DiagnosticCategory.Environment: return "环境";
                case DiagnosticCategory.Package: return "依赖包";
                case DiagnosticCategory.Project: return "工程资产";
                case DiagnosticCategory.Config: return "配置";
                case DiagnosticCategory.Addressables: return "Addressables";
                case DiagnosticCategory.Docs: return "文档";
                default: return category.ToString();
            }
        }
    }
}
