using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Toolbox
{
    /// <summary>
    /// ReunionMovement 的统一入口。
    /// <para>
    /// 设计原则：
    /// 1. 打开窗口只做只读检查；迁移、构建、导出等修改操作必须用户主动点击并二次确认。
    /// 2. 分区展示（概览 / 检查 / 模块 / 守卫 / 工具 / 文档），不再一条长滚动到底。
    /// 3. 每个问题都要带"怎么修"，没有可修动作的问题就不该出现在列表里。
    /// </para>
    /// </summary>
    public sealed partial class ReunionMovementToolboxWindow : EditorWindow
    {
        private enum Tab
        {
            Overview = 0,
            Diagnostics = 1,
            Modules = 2,
            Guards = 3,
            Tools = 4,
            Addressables = 5,
            Docs = 6,
        }

        private static readonly string[] TabLabels = { "概览", "检查", "模块", "守卫", "工具", "Addressables", "文档" };

        private readonly List<DiagnosticItem> diagnostics = new List<DiagnosticItem>();
        private readonly List<ModuleInfo> modules = new List<ModuleInfo>();
        private readonly List<SceneOption> exampleScenes = new List<SceneOption>();

        private Tab currentTab;
        private Vector2 scroll;
        private string search = string.Empty;
        private bool onlyProblems;
        private int selectedExampleSceneIndex;
        private DateTime lastRefreshAt;

        [MenuItem("ReunionMovement/工具箱", false, 0)]
        public static void Open()
        {
            ReunionMovementToolboxWindow window = GetWindow<ReunionMovementToolboxWindow>();
            window.titleContent = new GUIContent("ReunionMovement", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image);
            window.minSize = new Vector2(680f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            currentTab = (Tab)Mathf.Clamp(EditorPrefs.GetInt("ReunionMovement.Toolbox.Tab", 0), 0, TabLabels.Length - 1);
            onlyProblems = EditorPrefs.GetBool("ReunionMovement.Toolbox.OnlyProblems", true);
            EditorApplication.projectChanged += RefreshAll;
            RefreshAll();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshAll;
        }

        private void OnGUI()
        {
            DrawToolbar();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (currentTab)
            {
                case Tab.Overview: DrawOverviewTab(); break;
                case Tab.Diagnostics: DrawDiagnosticsTab(); break;
                case Tab.Modules: DrawModulesTab(); break;
                case Tab.Guards: DrawGuardsTab(); break;
                case Tab.Tools: DrawToolsTab(); break;
                case Tab.Addressables: DrawAddressablesTab(); break;
                case Tab.Docs: DrawDocsTab(); break;
            }
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int index = GUILayout.Toolbar((int)currentTab, TabLabels, EditorStyles.toolbarButton, GUILayout.Height(20f));
                if (index != (int)currentTab)
                {
                    currentTab = (Tab)index;
                    EditorPrefs.SetInt("ReunionMovement.Toolbox.Tab", index);
                    scroll = Vector2.zero;
                }

                GUILayout.FlexibleSpace();

                int errors = CountLevel(DiagnosticLevel.Error);
                int warnings = CountLevel(DiagnosticLevel.Warning);
                if (GUILayout.Button(StatusChip(errors, warnings), EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    currentTab = Tab.Diagnostics;
                    EditorPrefs.SetInt("ReunionMovement.Toolbox.Tab", (int)currentTab);
                }

                using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isUpdating))
                {
                    if (GUILayout.Button("重新检查", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        RefreshAll();
                    }
                }
            }
        }

        private string StatusChip(int errors, int warnings)
        {
            if (errors > 0) return "× 错误 " + errors;
            if (warnings > 0) return "! 提醒 " + warnings;
            return "✓ 检查通过";
        }

        private int CountLevel(DiagnosticLevel level)
        {
            int count = 0;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Level == level) count++;
            }
            return count;
        }

        private void RefreshAll()
        {
            CollectDiagnostics();
            CollectModules();
            RefreshExampleScenes();

            lastRefreshAt = DateTime.Now;
            Repaint();
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string refreshed = lastRefreshAt == default ? "尚未检查" : "最近检查 " + lastRefreshAt.ToString("HH:mm:ss");
                EditorGUILayout.LabelField(refreshed, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(160f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("文档 Docs/ · 守卫 ReunionMovement/开发/守卫 · 案案 Docs/重构案案.md",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}
