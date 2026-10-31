using System.Collections.Generic;
using Gear.Editor.Guards;
using UnityEditor;
using UnityEngine;

namespace Gear.EditorTools.Toolbox
{
    /// <summary>
    /// 守卫：把 CI 里跑的六条规则搬到编辑器里，就地看结果。
    /// 规则实现只有一份（ArchitectureGuard），这里不复制逻辑，只负责展示。
    /// </summary>
    public sealed partial class GearToolboxWindow
    {
        private readonly List<GuardResult> guardResults = new List<GuardResult>();
        private bool guardsRan;
        private bool guardAutoRunRequested;

        private void ResetGuards()
        {
            guardsRan = false;
            guardAutoRunRequested = false;
            guardResults.Clear();
        }

        private void DrawGuardsTab()
        {
            SectionHeader("架构守卫",
                "六条规则与 CI / EditMode 测试里跑的是同一份实现（ArchitectureGuard）。这里只是换个地方显示。");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(guardsRan ? "重新运行" : "运行全部检查", GUILayout.Height(26f), GUILayout.Width(140f)))
                {
                    RunAllGuards();
                }

                if (GUILayout.Button("复制结果", GUILayout.Width(90f), GUILayout.Height(26f))) CopyGuardReport();
                GUILayout.FlexibleSpace();
                if (guardsRan)
                {
                    int failed = 0;
                    for (int i = 0; i < guardResults.Count; i++)
                    {
                        if (!guardResults[i].Passed) failed++;
                    }
                    EditorGUILayout.LabelField(failed == 0 ? "✓ 全部通过" : "× " + failed + " 条未通过", EditorStyles.boldLabel, GUILayout.Width(110f));
                }
            }

            if (!guardsRan)
            {
                // 首次进入自动跑一次；用 delayCall 而不是在 Layout 事件里直接跑，
                // 否则会在 IMGUI 布局过程中改动状态，容易触发 GUI 栈不配对。
                if (!guardAutoRunRequested)
                {
                    guardAutoRunRequested = true;
                    EditorApplication.delayCall += RunAllGuards;
                }

                EditorGUILayout.HelpBox("正在运行首次检查…（守卫会扫描 Assets/Gear 下的源码，通常几百毫秒）", MessageType.Info);
                return;
            }

            foreach (GuardResult result in guardResults)
            {
                DrawGuardRow(result);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "同一条规则在三个地方生效：这里、Unity Test Runner 的 ArchitectureGuardTests、以及 CI 的 guard-only 作业。" +
                "命令行：Unity -batchmode -quit -executeMethod Gear.Editor.Guards.ArchitectureGuard.RunAllBatch",
                MessageType.Info);
        }

        private void DrawGuardRow(GuardResult result)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(result.Passed ? "✓" : "×", GUILayout.Width(16f));
                    EditorGUILayout.LabelField(result.Name, EditorStyles.boldLabel, GUILayout.Width(150f));
                    EditorGUILayout.LabelField(result.Description, EditorStyles.wordWrappedMiniLabel);
                }

                if (result.Passed || result.Offenders == null || result.Offenders.Count == 0) return;

                int limit = Mathf.Min(result.Offenders.Count, 8);
                for (int i = 0; i < limit; i++)
                {
                    EditorGUILayout.LabelField("    " + result.Offenders[i], EditorStyles.wordWrappedMiniLabel);
                }
                if (result.Offenders.Count > limit)
                {
                    EditorGUILayout.LabelField("    …另有 " + (result.Offenders.Count - limit) + " 条，详见 Console", EditorStyles.miniLabel);
                }
            }
        }

        private void RunAllGuards()
        {
            guardResults.Clear();

            IList<string> fileSizeOffenders = new List<string>();
            IList<string> metaOffenders = new List<string>();
            IList<string> instanceOffenders = new List<string>();
            IList<string> legacyOffenders = new List<string>();

            guardResults.Add(new GuardResult("单文件行数",
                "新文件 ≤ " + ArchitectureGuard.MaxLines + " 行；已登记基线的文件只能变小。",
                ArchitectureGuard.CheckFileSizes(out fileSizeOffenders), fileSizeOffenders));

            guardResults.Add(new GuardResult(".meta 完整性",
                "每个 .cs / .asmdef 都要有 .meta，否则 GUID 会重新生成、引用断开。",
                ArchitectureGuard.CheckMetaFiles(out metaOffenders), metaOffenders));

            guardResults.Add(new GuardResult("asmdef 依赖方向",
                "Base → Common → Utilities → Modules → Runtime 单向，上层不能被下层引用。",
                ArchitectureGuard.CheckAssemblyLayers(), null));

            guardResults.Add(new GuardResult(".Instance 使用点",
                "冻结存量：新增调用点应改用 Modules.Get<T>()。",
                ArchitectureGuard.CheckInstanceUsage(out instanceOffenders), instanceOffenders));

            guardResults.Add(new GuardResult("模块顺序声明",
                "每个 ICustomSystem 实现都要写 [ModuleOrder]，否则初始化顺序退化成书写顺序。",
                ArchitectureGuard.CheckModuleOrder(), null));

            guardResults.Add(new GuardResult("旧命名残留",
                "代码、模板与文档里不能再出现改名前的程序集名或目录路径。",
                ArchitectureGuard.CheckLegacyNames(out legacyOffenders), legacyOffenders));

            guardsRan = true;
            Repaint();
        }

        private void CopyGuardReport()
        {
            if (!guardsRan || guardResults.Count == 0)
            {
                ShowNotification(new GUIContent("先运行一次检查"));
                return;
            }

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Gear 架构守卫");
            foreach (GuardResult result in guardResults)
            {
                builder.AppendLine((result.Passed ? "[PASS] " : "[FAIL] ") + result.Name + " —— " + result.Description);
                if (result.Offenders == null) continue;

                for (int i = 0; i < result.Offenders.Count; i++)
                {
                    builder.AppendLine("        " + result.Offenders[i]);
                }
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
            ShowNotification(new GUIContent("结果已复制到剪贴板"));
        }

        private sealed class GuardResult
        {
            public readonly string Name;
            public readonly string Description;
            public readonly bool Passed;
            public readonly IList<string> Offenders;

            public GuardResult(string name, string description, bool passed, IList<string> offenders)
            {
                Name = name;
                Description = description;
                Passed = passed;
                Offenders = offenders;
            }
        }
    }
}
