using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Astray.EditorTools.Toolbox
{
    /// <summary>文档：把 Docs/ 下的 markdown 列出来，点一下就用系统默认程序打开。</summary>
    public sealed partial class AstrayToolboxWindow
    {
        private static readonly string[] PreferredDocs =
        {
            "00-快速开始.md",
            "01-架构总览.md",
            "02-新增一个系统.md",
            "03-新增一个窗口.md",
            "04-接入自己的游戏.md",
            "05-常见陷阱.md",
        };

        private static readonly string[] ReferenceDocs =
        {
            "design/重构方案.md",
            "archive/阶段3-执行清单.md",
            "archive/阶段3-第一步-程序集拆分-验证报告.md",
            "archive/阶段2-遗留-MonoBehaviour模块改造方案.md",
            "archive/UI系统-现状评估与演进方案.md",
            "features.md",
            "guides/工具箱.md",
            "guides/第三方依赖.md",
            "guides/跨平台构建指南.md",
            "design/便捷性与可移植性评估.md",
        };

        private void DrawDocsTab()
        {
            SectionHeader("文档", "按阅读顺序排列。点「打开」用系统默认 Markdown 程序打开。");

            string docsFolder = ResolveDocsFolder();
            if (docsFolder == null)
            {
                EditorGUILayout.HelpBox("未找到 Docs 目录（既不在项目根，也不在仓库根）。" +
                                        "如果是通过 .unitypackage 引入的，只能查看随包简明指南。", MessageType.Warning);
                if (GUILayout.Button("打开随包简明指南", GUILayout.Width(180f))) OpenDocumentation("Astray 使用指南.md");
                return;
            }

            EditorGUILayout.LabelField("Docs 位置：" + docsFolder, EditorStyles.miniLabel);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("上手（按顺序读）", EditorStyles.boldLabel);
            foreach (string doc in PreferredDocs)
            {
                DrawDocRow(docsFolder, doc);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("参考", EditorStyles.boldLabel);
            foreach (string doc in ReferenceDocs)
            {
                DrawDocRow(docsFolder, doc);
            }

            EditorGUILayout.Space(6f);
            List<string> others = CollectOtherDocs(docsFolder);
            if (others.Count > 0)
            {
                EditorGUILayout.LabelField("其它（" + others.Count + "）", EditorStyles.boldLabel);
                foreach (string doc in others)
                {
                    DrawDocRow(docsFolder, doc);
                }
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("在文件管理器中打开 Docs", GUILayout.Width(200f)))
                {
                    EditorUtility.RevealInFinder(docsFolder);
                }
            }
        }

        private void DrawDocRow(string docsFolder, string relativePath)
        {
            string fullPath = Path.Combine(docsFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            bool exists = File.Exists(fullPath);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(exists ? "✓" : "!", GUILayout.Width(14f));
                EditorGUILayout.LabelField(exists ? relativePath : relativePath + "（缺失）", EditorStyles.boldLabel, GUILayout.Width(300f));
                EditorGUILayout.LabelField(exists ? ShortDescription(relativePath) : "文件不存在", EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("打开", GUILayout.Width(60f))) OpenDocumentation(relativePath);
                }
            }
        }

        private static string ShortDescription(string relativePath)
        {
            switch (relativePath)
            {
                case "00-快速开始.md": return "环境、按 Play 会发生什么、第一个改动";
                case "01-架构总览.md": return "分层与一次启动的完整时序";
                case "02-新增一个系统.md": return "[ModuleOrder] → 注册 → 获取 → 测试";
                case "03-新增一个窗口.md": return "生成器 → Prefab → UINames → 打开/关闭";
                case "04-接入自己的游戏.md": return "两条接入路线、改启动场景、关掉示例";
                case "05-常见陷阱.md": return "双驱动 / 暂停 / Addressables / WebGL";
                case "design/重构方案.md": return "痛点、分阶段方案、验收指标与待验证清单";
                case "archive/阶段3-执行清单.md": return "分层归位的可勾选步骤";
                case "archive/阶段3-第一步-程序集拆分-验证报告.md": return "拆分可行性的实测证据";
                case "archive/阶段2-遗留-MonoBehaviour模块改造方案.md": return "三个 MonoBehaviour 模块的去留";
                case "archive/UI系统-现状评估与演进方案.md": return "两套 UI 栈的现状、对比与演进路线";
                case "features.md": return "框架能力清单";
                case "guides/第三方依赖.md": return "第三方版本、重名冲突与二选一";
                case "guides/跨平台构建指南.md": return "多平台构建、link.xml 与平台备注";
                case "design/便捷性与可移植性评估.md": return "调用 / 跨平台 / 插件便携性的现状与路线";
                case "guides/工具箱.md": return "本窗口的说明";
                default: return string.Empty;
            }
        }

        private static List<string> CollectOtherDocs(string docsFolder)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string doc in PreferredDocs) known.Add(doc);
            foreach (string doc in ReferenceDocs) known.Add(doc);

            var others = new List<string>();
            try
            {
                foreach (string file in Directory.GetFiles(docsFolder, "*.md", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    if (!known.Contains(name)) others.Add(name);
                }
            }
            catch (Exception)
            {
                // 目录读不到就当没有额外文档
            }

            others.Sort(StringComparer.OrdinalIgnoreCase);
            return others;
        }
    }
}
