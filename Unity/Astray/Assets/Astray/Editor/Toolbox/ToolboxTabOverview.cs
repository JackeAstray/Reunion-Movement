using UnityEditor;
using UnityEngine;

namespace Astray.EditorTools.Toolbox
{
    /// <summary>概览：一眼看到项目状态与"下一步做什么"。</summary>
    public sealed partial class AstrayToolboxWindow
    {
        private void DrawOverviewTab()
        {
            DrawStatusSummary();
            DrawNextSteps();
            DrawQuickStart();
        }

        private void DrawStatusSummary()
        {
            int errors = CountLevel(DiagnosticLevel.Error);
            int warnings = CountLevel(DiagnosticLevel.Warning);
            int passed = CountLevel(DiagnosticLevel.Passed);

            DiagnosticLevel worst = errors > 0 ? DiagnosticLevel.Error : warnings > 0 ? DiagnosticLevel.Warning : DiagnosticLevel.Passed;
            string summary = errors > 0
                ? "发现 " + errors + " 个错误、" + warnings + " 个提醒。先处理错误，再运行示例。"
                : warnings > 0
                    ? "基础环境可用，另有 " + warnings + " 个提醒；" + passed + " 项检查通过。"
                    : "项目状态良好，" + passed + " 项检查全部通过。";

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(summary, LevelMessageType(worst));

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatCard("错误", errors, errors > 0 ? DiagnosticLevel.Error : DiagnosticLevel.Passed);
                DrawStatCard("提醒", warnings, warnings > 0 ? DiagnosticLevel.Warning : DiagnosticLevel.Passed);
                DrawStatCard("通过", passed, DiagnosticLevel.Passed);
            }
        }

        private static void DrawStatCard(string label, int value, DiagnosticLevel level)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(52f)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField(LevelGlyph(level) + " " + value, EditorStyles.largeLabel);
            }
        }

        private void DrawNextSteps()
        {
            SectionHeader("下一步", "按严重程度排的前 3 条；每条都可以就地处理。");

            int shown = 0;
            for (int i = 0; i < diagnostics.Count && shown < 3; i++)
            {
                DiagnosticItem item = diagnostics[i];
                if (item.Level == DiagnosticLevel.Passed) continue;
                if (item.Action == null && string.IsNullOrEmpty(item.ActionLabel)) continue;

                DrawDiagnosticRow(item, true);
                shown++;
            }

            if (shown == 0)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("✓ 没有需要处理的问题。", EditorStyles.boldLabel);
                    if (GUILayout.Button("跑一遍守卫", GUILayout.Width(96f)))
                    {
                        currentTab = Tab.Guards;
                        RunAllGuards();
                    }
                }
            }
        }

        private void DrawQuickStart()
        {
            SectionHeader("快速开始", "先用 Resources 模式跑通，再按需开启 Addressables / 热更新。");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1 运行示例", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (exampleScenes.Count > 0)
                    {
                        string[] labels = new string[exampleScenes.Count];
                        for (int i = 0; i < exampleScenes.Count; i++) labels[i] = exampleScenes[i].Label;
                        selectedExampleSceneIndex = EditorGUILayout.Popup("示例场景", Mathf.Clamp(selectedExampleSceneIndex, 0, labels.Length - 1), labels);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("未找到含 Example 的场景", EditorStyles.miniLabel);
                    }

                    using (new EditorGUI.DisabledScope(exampleScenes.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("打开所选场景", GUILayout.Width(110f))) OpenExampleScene();
                    }
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("2 准备配置", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool hasConfig = AssetDatabase.LoadMainAssetAtPath(GameConfigPath) != null;
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                    {
                        if (GUILayout.Button(hasConfig ? "定位 GameConfig" : "创建 GameConfig…", GUILayout.Height(24f)))
                        {
                            if (hasConfig) SelectAsset(GameConfigPath);
                            else CreateGameConfig();
                        }
                    }

                    if (GUILayout.Button("打开快速开始", GUILayout.Height(24f))) OpenDocumentation("00-快速开始.md");
                    if (GUILayout.Button("常见陷阱", GUILayout.Height(24f))) OpenDocumentation("05-常见陷阱.md");
                }
            }
        }
    }
}
