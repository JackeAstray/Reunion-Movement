using System;
using UnityEditor;
using UnityEngine;

namespace Astray.EditorTools.Toolbox
{
    /// <summary>工具：调用既有菜单，不复制工具逻辑。</summary>
    public sealed partial class AstrayToolboxWindow
    {
        private void DrawToolsTab()
        {
            SectionHeader("常用工具", "这些按钮调用现有菜单项；修改类操作都会二次确认。");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawToolRow("资源与发布",
                    new ToolButton("Addressables Groups", () => ExecuteMenu("Window/Asset Management/Addressables/Groups")),
                    new ToolButton("一键流水线", () => ExecuteMenu(
                        "Astray/Addressables/一键流水线（迁移+构建+上传 OSS）",
                        "运行 Addressables 一键流水线？",
                        "会补齐配置、迁移资源、切到 WebGL 并构建，可能启用远程 Catalog；OSS 凭据已配置时还会上传到外部服务。\n\n" +
                        "目标平台固定 WebGL。请先保存并备份项目。")),
                    new ToolButton("导出 unitypackage", () => ExecuteMenu("Astray/打包/导出 .unitypackage")));

                DrawToolRow("Addressables",
                    new ToolButton("初始化/补齐配置…", () => ExecuteMenu(
                        "Astray/Addressables/配置/初始化配置（分组+Label+Profile）",
                        "初始化 Addressables 配置？",
                        "会创建或补齐 Settings、分组、Label 和 Profile。")),
                    new ToolButton("迁移全部资源…", () => ExecuteMenu(
                        "Astray/Addressables/迁移/一键迁移全部（UI+音频+图片）",
                        "迁移 Astray 资源？",
                        "会复制、重映射并把 UI、音频、图片、配置、预制体、场景与字体纳入 Addressables；源 Resources 不删除。建议先提交。")),
                    new ToolButton("构建当前平台…", () => ExecuteMenu(
                        "Astray/Addressables/构建/构建 Content（当前平台）",
                        "构建 Addressables Content？",
                        "会生成或覆盖当前平台的构建产物。请先确认 Profile 与目标平台。")));

                DrawToolRow("数据与辅助",
                    new ToolButton("Excel → 脚本", () => ExecuteMenu("Astray/内容/表格 -> 脚本")),
                    new ToolButton("Excel → JSON", () => ExecuteMenu("Astray/内容/表格 -> JSON")),
                    new ToolButton("小功能", () => ExecuteMenu("Astray/开发/小功能")));

                DrawToolRow("窗口生成",
                    new ToolButton("UI 窗口生成器", () => ExecuteMenu("Astray/内容/生成 UI 窗口")),
                    new ToolButton("模块模板", () => OpenDocumentation("02-新增一个系统.md")),
                    new ToolButton("窗口文档", () => OpenDocumentation("03-新增一个窗口.md")));
            }

            SectionHeader("扩展模块", "复制模板 → 保存到 Core/System → 在 StartGame.CreateModules() 显式注册。工具箱不会自动改启动列表。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("模板：Astray.Framework.Runtime.Custom.CustomSystem", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "保存为 Core/System/CustomSystem/CustomSystem.cs，沿用 Astray.Framework.Runtime 程序集；不要放进无 asmdef 的 Assets 根目录。",
                    EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制模块模板")) CopyModuleTemplate();
                    if (GUILayout.Button("定位注册入口")) SelectAsset(AssetRoot + "/Core/StartGame.cs");
                    if (GUILayout.Button("定位 Core 目录")) PingFolder(AssetRoot + "/Core");
                    if (GUILayout.Button("打开新增系统文档")) OpenDocumentation("02-新增一个系统.md");
                }

                EditorGUILayout.LabelField("模板已含 [ModuleOrder]（守卫强制要求）。InitProgress 取 0–100；Update / Clear 是可选接口。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void DrawToolRow(string label, params ToolButton[] buttons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(92f));
                foreach (ToolButton button in buttons)
                {
                    if (GUILayout.Button(button.Label, GUILayout.Height(25f))) button.Action();
                }
            }
        }

        private static void CopyModuleTemplate()
        {
            var template = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetRoot + "/Documentation/CustomSystem.cs.txt");
            if (template == null)
            {
                EditorUtility.DisplayDialog("模板未找到", "请确认随包 Documentation 目录完整，等导入完成后重试。", "确定");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = template.text;
        }
    }
}
