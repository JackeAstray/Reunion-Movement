using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Astray.EditorTools.Toolbox
{
    /// <summary>工具箱的公共动作与绘制工具。</summary>
    public sealed partial class AstrayToolboxWindow
    {
        // ---------------------------------------------------------------- 绘制

        private static void SectionHeader(string title, string description)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.largeLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(2f);
        }

        private static string LevelGlyph(DiagnosticLevel level)
        {
            switch (level)
            {
                case DiagnosticLevel.Error: return "×";
                case DiagnosticLevel.Warning: return "!";
                default: return "✓";
            }
        }

        private static MessageType LevelMessageType(DiagnosticLevel level)
        {
            switch (level)
            {
                case DiagnosticLevel.Error: return MessageType.Error;
                case DiagnosticLevel.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        // ---------------------------------------------------------------- 资产与文档

        private static void SelectAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Astray", "未找到资产：\n" + path, "确定");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void PingFolder(string path)
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (folder == null) return;

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static bool CanExecuteToolMenu()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer)
            {
                return true;
            }

            EditorUtility.DisplayDialog("编辑器当前忙碌",
                "请等待编译、资产导入或 Player 构建结束，并退出 Play Mode 后重新点击。此操作不会自动重试。", "确定");
            return false;
        }

        private static void ExecuteMenu(string menuPath, string confirmationTitle = null, string confirmationMessage = null)
        {
            if (!CanExecuteToolMenu()) return;
            if (!string.IsNullOrEmpty(confirmationMessage) &&
                !EditorUtility.DisplayDialog(confirmationTitle ?? "Astray", confirmationMessage, "继续", "取消"))
            {
                return;
            }

            // 迁移/构建菜单可能刷新 AssetDatabase 或触发域重载，延迟到当前 IMGUI 事件之后执行。
            EditorApplication.delayCall += () =>
            {
                if (!CanExecuteToolMenu()) return;
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                {
                    EditorUtility.DisplayDialog("Astray",
                        "无法执行菜单：\n" + menuPath + "\n\n请确认相关依赖和编辑器脚本已成功编译。", "确定");
                }
            };
        }

        private static void OpenDocumentation(string relativePath)
        {
            string fullPath = GetDocumentationPath(relativePath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Astray", "未找到文档：\n" + fullPath, "确定");
                return;
            }

            Application.OpenURL(new Uri(fullPath).AbsoluteUri);
        }

        /// <summary>
        /// 文档查找顺序：项目根 Docs → 仓库根 Docs → 随包简明指南。
        /// 兼容三种场景：源码仓库、导入了 .unitypackage 的普通项目、被裁剪过的项目。
        /// </summary>
        private static string GetDocumentationPath(string relativePath)
        {
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            string projectDocs = Path.Combine(projectRoot, "Docs", normalized);
            if (File.Exists(projectDocs)) return projectDocs;

            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
            string repositoryDocs = Path.Combine(repositoryRoot, "Docs", normalized);
            if (File.Exists(repositoryDocs)) return repositoryDocs;

            string bundled = Path.Combine(projectRoot, BundledGuidePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(bundled) ? bundled : projectDocs;
        }

        /// <summary>返回当前可用的 Docs 目录（不存在则返回 null）。</summary>
        private static string ResolveDocsFolder()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectDocs = Path.Combine(projectRoot, "Docs");
            if (Directory.Exists(projectDocs)) return projectDocs;

            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
            string repositoryDocs = Path.Combine(repositoryRoot, "Docs");
            return Directory.Exists(repositoryDocs) ? repositoryDocs : null;
        }

        private void CreateGameConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!EditorUtility.DisplayDialog("创建 GameConfig？",
                "将在 Resources/ScriptableObjects 下创建默认配置（热更新关闭）。已有资产不会被覆盖。", "创建", "取消"))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (AssetDatabase.LoadMainAssetAtPath(GameConfigPath) != null || File.Exists(GameConfigPath))
                {
                    SelectAsset(GameConfigPath);
                    return;
                }

                Type configType = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                    .FirstOrDefault(type => type.FullName == "Astray.GameConfig" && !type.IsAbstract);
                if (configType == null)
                {
                    EditorUtility.DisplayDialog("GameConfig 类型不可用", "请先解决 Console 中的编译错误。", "确定");
                    return;
                }

                string folder = AssetRoot + "/Resources/ScriptableObjects";
                string current = "Assets";
                foreach (string part in folder.Split('/').Skip(1))
                {
                    string next = current + "/" + part;
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                    current = next;
                }

                ScriptableObject config = ScriptableObject.CreateInstance(configType);
                AssetDatabase.CreateAsset(config, GameConfigPath);
                AssetDatabase.SaveAssets();
                SelectAsset(GameConfigPath);
                RefreshAll();
            };
        }

        private void OpenExampleScene()
        {
            if (exampleScenes.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SceneOption scene = exampleScenes[Mathf.Clamp(selectedExampleSceneIndex, 0, exampleScenes.Count - 1)];

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.Path) == null) return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(scene.Path, OpenSceneMode.Single);
            };
        }

        // ---------------------------------------------------------------- 类型

        private enum DiagnosticLevel
        {
            Passed,
            Warning,
            Error,
        }

        private enum DiagnosticCategory
        {
            Environment,
            Package,
            Project,
            Config,
            Addressables,
            Docs,
        }

        private sealed class DiagnosticItem
        {
            public readonly DiagnosticLevel Level;
            public readonly DiagnosticCategory Category;
            public readonly string Title;
            public readonly string Message;
            public readonly string ActionLabel;
            public readonly Action Action;

            public DiagnosticItem(DiagnosticLevel level, DiagnosticCategory category, string title, string message,
                string actionLabel, Action action)
            {
                Level = level;
                Category = category;
                Title = title;
                Message = message;
                ActionLabel = actionLabel;
                Action = action;
            }
        }

        private sealed class ModuleInfo
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly string Namespace;
            public readonly string AssemblyName;
            public readonly int Order;
            public readonly Type[] DependsOn;

            public ModuleInfo(Type type, int order, Type[] dependsOn)
            {
                Type = type;
                Name = type.Name;
                Namespace = type.Namespace ?? string.Empty;
                AssemblyName = type.Assembly.GetName().Name;
                Order = order;
                DependsOn = dependsOn;
            }

            public string DependencyText
            {
                get
                {
                    if (DependsOn == null || DependsOn.Length == 0) return "—";

                    string[] names = new string[DependsOn.Length];
                    for (int i = 0; i < DependsOn.Length; i++)
                    {
                        names[i] = DependsOn[i] == null ? "null" : DependsOn[i].Name;
                    }
                    return string.Join(", ", names);
                }
            }
        }

        private sealed class SceneOption
        {
            public readonly string Label;
            public readonly string Path;

            public SceneOption(string label, string path)
            {
                Label = label;
                Path = path;
            }
        }

        private readonly struct ToolButton
        {
            public readonly string Label;
            public readonly Action Action;

            public ToolButton(string label, Action action)
            {
                Label = label;
                Action = action;
            }
        }
    }
}
