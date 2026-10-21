using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReunionMovement.EditorTools.Toolbox
{
    /// <summary>
    /// ReunionMovement 的统一入口与项目体检窗口。
    /// 打开窗口只执行只读检查；迁移、构建、导出等修改操作必须由用户主动点击。
    /// </summary>
    public sealed class ReunionMovementToolboxWindow : EditorWindow
    {
        private const string AssetRoot = "Assets/ReunionMovement";
        private const string GameConfigPath = AssetRoot + "/Resources/ScriptableObjects/GameConfig.asset";
        private const string BundledGuidePath = AssetRoot + "/Documentation/ReunionMovement 使用指南.md";

        private readonly List<DiagnosticItem> diagnostics = new List<DiagnosticItem>();
        private readonly List<SceneOption> exampleScenes = new List<SceneOption>();
        private Vector2 scrollPosition;
        private bool showPassedChecks;
        private bool addressablesSettingsFound;
        private string addressablesSettingsPath;
        private string addressablesCheckMessage;
        private bool addressablesStructureValid;
        private int selectedExampleSceneIndex;
        private DateTime lastRefreshAt;

        [MenuItem("ReunionMovement/工具箱", false, 0)]
        public static void Open()
        {
            var window = GetWindow<ReunionMovementToolboxWindow>();
            window.titleContent = new GUIContent("ReunionMovement", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image);
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            showPassedChecks = EditorPrefs.GetBool("ReunionMovement.Toolbox.ShowPassedChecks", true);
            EditorApplication.projectChanged += RefreshDiagnostics;
            RefreshDiagnostics();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshDiagnostics;
        }

        private void OnGUI()
        {
            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawOverview();
            DrawGettingStarted();
            DrawDiagnostics();
            DrawCommonTools();
            DrawModuleExtension();
            DrawFooter();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("ReunionMovement 工具箱", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新检查", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                {
                    RefreshDiagnostics();
                }
            }
        }

        private void DrawOverview()
        {
            int errors = diagnostics.Count(item => item.Level == DiagnosticLevel.Error);
            int warnings = diagnostics.Count(item => item.Level == DiagnosticLevel.Warning);
            int passed = diagnostics.Count(item => item.Level == DiagnosticLevel.Passed);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("项目概览", EditorStyles.largeLabel);

            MessageType messageType = errors > 0
                ? MessageType.Error
                : warnings > 0 ? MessageType.Warning : MessageType.Info;

            string summary = errors > 0
                ? $"发现 {errors} 个错误、{warnings} 个提醒。建议先处理错误再运行示例。"
                : warnings > 0
                    ? $"基础环境可用，另有 {warnings} 个可选提醒；{passed} 项检查通过。"
                    : $"项目状态良好，{passed} 项检查全部通过。";

            EditorGUILayout.HelpBox(summary, messageType);
        }

        private void DrawGettingStarted()
        {
            DrawSectionTitle("开始使用", "先跑通 Resources 模式，再按需要开启远程资源流程。");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("路径 A：最快运行（推荐首次使用）", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("保持 GameConfig.enableHotUpdate 关闭，直接运行插件内 Resources 示例；场景打开前会提示保存当前场景。", EditorStyles.wordWrappedLabel);
                if (exampleScenes.Count > 0)
                {
                    string[] sceneLabels = exampleScenes.Select(scene => scene.Label).ToArray();
                    selectedExampleSceneIndex = Mathf.Clamp(selectedExampleSceneIndex, 0, sceneLabels.Length - 1);
                    selectedExampleSceneIndex = EditorGUILayout.Popup("示例场景", selectedExampleSceneIndex, sceneLabels);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool hasConfig = AssetDatabase.LoadMainAssetAtPath(GameConfigPath) != null;
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                    {
                        if (GUILayout.Button(hasConfig ? "定位 GameConfig" : "创建 GameConfig…", GUILayout.Height(26f)))
                        {
                            if (hasConfig) SelectAsset(GameConfigPath);
                            else CreateGameConfig();
                        }
                    }
                    if (GUILayout.Button("打开快速开始", GUILayout.Height(26f))) OpenDocumentation("Addressables/快速开始.md");
                    using (new EditorGUI.DisabledScope(exampleScenes.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button(exampleScenes.Count == 0 ? "未找到示例" : "打开所选场景", GUILayout.Height(26f)))
                            OpenExampleScene();
                    }
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("路径 B：Addressables / 远程热更", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(addressablesSettingsFound
                    ? $"已发现 Settings：{addressablesSettingsPath}。配置检查见下方；修改和构建操作均需确认。"
                    : "先显式初始化 Addressables 配置；完成后再迁移资源和构建 Content。发布前另行配置 CDN 与 OSS。", EditorStyles.wordWrappedLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(addressablesSettingsFound ? "检查/补齐配置…" : "1. 初始化配置…", GUILayout.Height(26f)))
                        ExecuteMenu("ReunionMovement/Addressables/配置/初始化配置（分组+Label+Profile）",
                            "初始化或补齐 Addressables 配置？",
                            "此操作会在项目中创建或补齐 Addressables Settings、分组、Label 和 Profile。");
                    using (new EditorGUI.DisabledScope(!addressablesSettingsFound))
                    {
                        if (GUILayout.Button("迁移全部资源…", GUILayout.Height(26f)))
                            ExecuteMenu("ReunionMovement/Addressables/迁移/一键迁移全部（UI+音频+图片）",
                                "迁移 ReunionMovement 资源？",
                                "此操作会复制、重映射并将 UI、音频、图片、配置、预制体、场景和字体纳入 Addressables；源 Resources 不会删除。建议先提交或备份项目。");
                        if (GUILayout.Button("构建当前平台…", GUILayout.Height(26f)))
                            ExecuteMenu("ReunionMovement/Addressables/构建/构建 Content（当前平台）",
                                "构建 Addressables Content？",
                                "此操作会生成或覆盖当前平台的 Addressables 构建产物。请先确认构建 Profile 和目标平台。");
                    }
                }
            }
        }

        private void DrawDiagnostics()
        {
            DrawSectionTitle("项目检查", "只读取当前项目状态，不会自动修改资产或 Package 配置。");

            bool newShowPassed = EditorGUILayout.ToggleLeft("显示已通过项目", showPassedChecks);
            if (newShowPassed != showPassedChecks)
            {
                showPassedChecks = newShowPassed;
                EditorPrefs.SetBool("ReunionMovement.Toolbox.ShowPassedChecks", showPassedChecks);
            }

            foreach (var item in diagnostics)
            {
                if (!showPassedChecks && item.Level == DiagnosticLevel.Passed) continue;
                DrawDiagnosticItem(item);
            }
        }

        private static void DrawDiagnosticItem(DiagnosticItem item)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string prefix = item.Level == DiagnosticLevel.Passed ? "✓" : item.Level == DiagnosticLevel.Warning ? "!" : "×";
                EditorGUILayout.LabelField(prefix, GUILayout.Width(18f));
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(item.Title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(item.Message, EditorStyles.wordWrappedMiniLabel);
                }

                if (item.Action != null && GUILayout.Button(item.ActionLabel, GUILayout.Width(96f), GUILayout.Height(34f)))
                {
                    item.Action();
                }
            }
        }

        private void DrawCommonTools()
        {
            DrawSectionTitle("常用工具", "这些按钮调用现有菜单，不复制原工具逻辑。");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawToolRow("资源与发布",
                    new ToolButton("Addressables Groups", () => ExecuteMenu("Window/Asset Management/Addressables/Groups")),
                    new ToolButton("一键流水线", () => ExecuteMenu(
                        "ReunionMovement/Addressables/一键流水线（迁移+构建+上传 OSS）",
                        "运行 Addressables 一键流水线？",
                        "此操作会补齐配置、迁移资源、切换到 WebGL 并构建，可能启用远程 Catalog；OSS 凭据已配置时还会上传到外部服务。\n\n目标平台固定 WebGL，不是当前平台。请先保存并备份项目，确认 Profile、远程路径与 OSS 上传配置。")),
                    new ToolButton("导出 unitypackage", () => ExecuteMenu("ReunionMovement/打包/导出 ReunionMovement.unitypackage")));

                DrawToolRow("数据与辅助",
                    new ToolButton("Excel → 脚本", () => ExecuteMenu("ReunionMovement/表格处理/表格 -> 脚本")),
                    new ToolButton("Excel → JSON", () => ExecuteMenu("ReunionMovement/表格处理/表格 -> JSON")),
                    new ToolButton("小功能", () => ExecuteMenu("ReunionMovement/小功能")));

                DrawToolRow("文档",
                    new ToolButton("打包指南", () => OpenDocumentation("Packaging/打包指南.md")),
                    new ToolButton("远程部署", () => OpenDocumentation("Addressables/远程部署指南.md")),
                    new ToolButton("UI 输入系统", () => OpenDocumentation("UI/UI输入系统.md")));
            }
        }

        private static void DrawModuleExtension()
        {
            DrawSectionTitle("扩展模块", "复制模板 → 保存到 Core/System → 在 StartGame.CreateModules() 显式注册。不自动修改启动模块列表。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("模板：ReunionMovement.Core.Custom.CustomSystem", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "保存为 Core/System/CustomSystem/CustomSystem.cs，沿用 ReunionMovement.Core 程序集。不要放入无 asmdef 的普通 Assets 目录；Core 无法引用 Assembly-CSharp。",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(
                    "在 StartGame 中添加 using ReunionMovement.Core.Custom;，并在 CreateModules() 返回前加入 modules.Add(CustomSystem.Instance);。列表顺序就是初始化顺序；保留 ResourcesSystem 最前。",
                    EditorStyles.wordWrappedLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制模块模板"))
                    {
                        var template = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetRoot + "/Documentation/CustomSystem.cs.txt");
                        if (template == null)
                            EditorUtility.DisplayDialog("模板未找到", "请确认随包 Documentation 目录完整，等待导入完成后重试。", "确定");
                        else
                            EditorGUIUtility.systemCopyBuffer = template.text;
                    }
                    if (GUILayout.Button("定位注册入口")) SelectAsset(AssetRoot + "/Core/StartGame.cs");
                    if (GUILayout.Button("定位 Core 目录")) PingFolder(AssetRoot + "/Core");
                }
                EditorGUILayout.LabelField(
                    "InitProgress 范围为 0–100；Update 和 Clear 是可选接口。重命名模板类时同步修改单例字段、构造函数及注册代码。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void DrawToolRow(string label, params ToolButton[] buttons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(88f));
                foreach (var button in buttons)
                {
                    if (GUILayout.Button(button.Label, GUILayout.Height(25f))) button.Action();
                }
            }
        }

        private static void DrawSectionTitle(string title, string description)
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField(title, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(10f);
            string refreshed = lastRefreshAt == default
                ? "尚未检查"
                : $"最近检查：{lastRefreshAt:HH:mm:ss}";
            EditorGUILayout.LabelField(refreshed, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(8f);
        }

        private void CreateGameConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!EditorUtility.DisplayDialog("创建 GameConfig？",
                "将在插件 Resources/ScriptableObjects 下创建默认配置（热更新关闭）。已有资产不会被覆盖。", "创建", "取消")) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (AssetDatabase.LoadMainAssetAtPath(GameConfigPath) != null || File.Exists(GameConfigPath))
                {
                    SelectAsset(GameConfigPath);
                    return;
                }
                Type configType = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                    .FirstOrDefault(type => type.FullName == "ReunionMovement.GameConfig" && !type.IsAbstract);
                if (configType == null)
                {
                    EditorUtility.DisplayDialog("GameConfig 类型不可用", "请先解决 Console 中的运行时脚本编译错误。", "确定");
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
                RefreshDiagnostics();
            };
        }

        private void AddGameConfigCheck()
        {
            var config = AssetDatabase.LoadMainAssetAtPath(GameConfigPath) as ScriptableObject;
            if (config == null)
            {
                if (File.Exists(GameConfigPath))
                {
                    diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, "GameConfig 无法加载",
                        "路径已存在，但资产或脚本不可用。请检查 Console 和原资产；工具箱不会覆盖它。",
                        "定位目录", () => PingFolder(AssetRoot + "/Resources/ScriptableObjects")));
                    return;
                }
                diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, "GameConfig",
                    "配置缺失；框架使用静态默认值。可显式创建默认配置。", "创建…", CreateGameConfig));
                return;
            }
            var serialized = new SerializedObject(config);
            var hotUpdate = serialized.FindProperty("enableHotUpdate");
            if (hotUpdate == null)
            {
                diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, "GameConfig",
                    "配置没有 enableHotUpdate 字段，请核对脚本版本。", "定位", () => SelectAsset(GameConfigPath)));
                return;
            }
            diagnostics.Add(new DiagnosticItem(hotUpdate.boolValue ? DiagnosticLevel.Warning : DiagnosticLevel.Passed,
                "GameConfig 运行模式",
                hotUpdate.boolValue
                    ? "热更新已开启：运行时会使用远程 Addressables。首次运行建议在 Inspector 中关闭，并另行验证远程 URL 与 Catalog。"
                    : "热更新关闭：使用 Resources，符合首次运行路径。",
                "定位", () => SelectAsset(GameConfigPath)));
        }

        private void RefreshDiagnostics()
        {
            diagnostics.Clear();

            AddUnityVersionCheck();
            AddPackageCheck("com.unity.addressables", "Addressables", "2.9.1");
            AddPackageCheck("com.unity.inputsystem", "Input System", "1.18.0");
            AddAssetCheck(AssetRoot, "ReunionMovement 资产根目录", true, () => PingFolder(AssetRoot));
            AddGameConfigCheck();
            RefreshAddressablesSettingsStatus();
            RefreshExampleScenes();
            AddAddressablesSettingsCheck();
            AddAssetCheck(AssetRoot + "/Plugins/Generators/ReunionMovement.SourceGenerator.dll",
                "源码生成器", false, () => SelectAsset(AssetRoot + "/Plugins/Generators/ReunionMovement.SourceGenerator.dll"),
                "未找到源码生成器；已有生成代码仍可使用，但自动生成能力不可用。");
            AddDocumentationCheck("Addressables/快速开始.md", "快速开始文档");
            AddDocumentationCheck("Packaging/打包指南.md", "打包指南");

            lastRefreshAt = DateTime.Now;
            Repaint();
        }

        private void RefreshAddressablesSettingsStatus()
        {
            addressablesSettingsFound = false;
            addressablesSettingsPath = null;
            addressablesStructureValid = false;
            addressablesCheckMessage = "未发现注册的 Addressables Settings；可按需显式初始化。";

            try
            {
                Assembly addressablesEditor = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Unity.Addressables.Editor", StringComparison.Ordinal));
                Type defaultObjectType = addressablesEditor?.GetType(
                    "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject", false);
                MethodInfo getSettings = defaultObjectType?.GetMethod("GetSettings", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(bool) }, null);
                if (getSettings == null)
                {
                    addressablesCheckMessage = "Addressables Editor API 暂不可用；请等待包解析和脚本编译完成后刷新。";
                    return;
                }
                object settings = getSettings.Invoke(null, new object[] { false });
                if (settings == null) return;

                addressablesSettingsFound = true;
                addressablesSettingsPath = AssetDatabase.GetAssetPath(settings as UnityEngine.Object);
                Type settingsType = settings.GetType();
                var missing = new List<string>();
                MethodInfo findGroup = settingsType.GetMethod("FindGroup", new[] { typeof(string) });
                foreach (string group in new[] { "BuiltIn_Config", "BuiltIn_UI", "BuiltIn_Prefabs", "BuiltIn_Fonts", "BuiltIn_Shaders", "Remote_Sounds", "Remote_Textures", "Remote_Scenes", "Remote_AutoDatabase" })
                {
                    if (findGroup == null) throw new MissingMethodException("FindGroup");
                    if (findGroup.Invoke(settings, new object[] { group }) == null) missing.Add("分组 " + group);
                }
                var labels = (IEnumerable<string>)settingsType.GetMethod("GetLabels", Type.EmptyTypes).Invoke(settings, null);
                foreach (string label in new[] { "builtin", "remote", "ui", "sound", "texture", "scene", "data" })
                    if (!labels.Contains(label)) missing.Add("Label " + label);
                object profiles = settingsType.GetProperty("profileSettings").GetValue(settings);
                var names = (IEnumerable<string>)profiles.GetType().GetMethod("GetAllProfileNames", Type.EmptyTypes).Invoke(profiles, null);
                foreach (string profile in new[] { "DevLocal", "Publish" })
                    if (!names.Contains(profile)) missing.Add("Profile " + profile);
                addressablesStructureValid = missing.Count == 0;
                addressablesCheckMessage = missing.Count == 0
                    ? $"已注册：{addressablesSettingsPath}；9 个分组、7 个 Label 和 2 个 Profile 名称齐全（不代表远程 URL 或构建内容已验证）。"
                    : $"已注册：{addressablesSettingsPath}；缺少：{string.Join("、", missing)}。可点击“检查/补齐配置”。";
            }
            catch (Exception ex)
            {
                addressablesCheckMessage = "Addressables 配置暂时无法完整检查：" + (ex.InnerException ?? ex).Message;
            }
        }

        private void RefreshExampleScenes()
        {
            string previousPath = exampleScenes.Count > 0 && selectedExampleSceneIndex < exampleScenes.Count
                ? exampleScenes[selectedExampleSceneIndex].Path
                : null;
            exampleScenes.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { AssetRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("Example", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                exampleScenes.Add(new SceneOption(name, path));
            }

            exampleScenes.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
            int selectedIndex = exampleScenes.FindIndex(scene => string.Equals(scene.Path, previousPath, StringComparison.Ordinal));
            selectedExampleSceneIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        private void AddAddressablesSettingsCheck()
        {
            diagnostics.Add(new DiagnosticItem(
                addressablesStructureValid ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                "Addressables 项目配置",
                addressablesCheckMessage,
                addressablesStructureValid && !string.IsNullOrEmpty(addressablesSettingsPath) ? "定位" : "初始化/补齐…",
                addressablesStructureValid && !string.IsNullOrEmpty(addressablesSettingsPath)
                    ? (Action)(() => SelectAsset(addressablesSettingsPath))
                    : () => ExecuteMenu("ReunionMovement/Addressables/配置/初始化配置（分组+Label+Profile）",
                        "初始化 Addressables 配置？",
                        "此操作会在项目中创建或补齐 Addressables Settings、分组、Label 和 Profile。")));
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

        private void AddUnityVersionCheck()
        {
            string[] parts = Application.unityVersion.Split('.');
            int major = 0;
            int minor = 0;
            bool parsed = parts.Length >= 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
            bool supported = parsed && (major > 6000 || major == 6000 && minor >= 3);
            diagnostics.Add(new DiagnosticItem(
                supported ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                "Unity 版本",
                supported
                    ? $"当前版本 {Application.unityVersion}，符合项目建议的 Unity 6000.3+。"
                    : $"当前版本 {Application.unityVersion}；项目文档建议使用 Unity 6000.3+。",
                "打开 Hub",
                () => Application.OpenURL("unityhub://")));
        }

        private void AddPackageCheck(string packageName, string displayName, string testedVersion)
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()?
                    .FirstOrDefault(info => string.Equals(info.name, packageName, StringComparison.Ordinal));

                if (package == null)
                {
                    diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Error, displayName,
                        $"未检测到 {packageName}。相关程序集可能无法编译。",
                        "Package Manager",
                        () => ExecuteMenu("Window/Package Manager")));
                    return;
                }

                bool tested = string.Equals(package.version, testedVersion, StringComparison.OrdinalIgnoreCase);
                diagnostics.Add(new DiagnosticItem(
                    tested ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                    displayName,
                    tested
                        ? $"已安装经过项目验证的 {package.name}@{package.version}。"
                        : $"已安装 {package.name}@{package.version}；当前项目验证版本为 {testedVersion}，请自行确认兼容性。",
                    null,
                    null));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, displayName,
                    $"Package Manager 状态暂时不可读：{ex.Message}",
                    "Package Manager",
                    () => ExecuteMenu("Window/Package Manager")));
            }
        }

        private void AddAssetCheck(
            string path,
            string title,
            bool required,
            Action foundAction,
            string missingMessage = null,
            string missingActionLabel = null,
            Action missingAction = null)
        {
            bool exists = AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;
            diagnostics.Add(new DiagnosticItem(
                exists ? DiagnosticLevel.Passed : required ? DiagnosticLevel.Error : DiagnosticLevel.Warning,
                title,
                exists ? $"已找到：{path}" : missingMessage ?? $"未找到：{path}",
                exists ? "定位" : missingActionLabel,
                exists ? foundAction : missingAction));
        }

        private void AddDocumentationCheck(string relativePath, string title)
        {
            string fullPath = GetDocumentationPath(relativePath);
            bool exists = File.Exists(fullPath);
            bool fallback = exists && string.Equals(Path.GetFileName(fullPath), "ReunionMovement 使用指南.md", StringComparison.Ordinal);
            diagnostics.Add(new DiagnosticItem(
                exists ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                title,
                fallback ? $"Docs/{relativePath} 不在当前项目；打开随包简明指南。"
                    : exists ? $"已找到 Docs/{relativePath}" : $"未找到 Docs/{relativePath}",
                exists ? "打开" : null,
                exists ? (Action)(() => OpenDocumentation(relativePath)) : null));
        }

        private static void SelectAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("ReunionMovement", $"未找到资产：\n{path}", "确定");
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
                !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer) return true;
            EditorUtility.DisplayDialog("编辑器当前忙碌",
                "请等待编译、资产导入或 Player 构建结束，并退出 Play Mode 后重新点击。此操作不会自动重试。", "确定");
            return false;
        }

        private static void ExecuteMenu(string menuPath, string confirmationTitle = null, string confirmationMessage = null)
        {
            if (!CanExecuteToolMenu()) return;
            if (!string.IsNullOrEmpty(confirmationMessage) &&
                !EditorUtility.DisplayDialog(confirmationTitle ?? "ReunionMovement", confirmationMessage, "继续", "取消"))
            {
                return;
            }

            // 迁移/构建菜单可能刷新 AssetDatabase 或触发域重载，延迟到当前 IMGUI 事件结束后执行。
            EditorApplication.delayCall += () =>
            {
                if (!CanExecuteToolMenu()) return;
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                {
                    EditorUtility.DisplayDialog("ReunionMovement",
                        $"无法执行菜单：\n{menuPath}\n\n请确认相关依赖和编辑器脚本已成功编译。", "确定");
                }
            };
        }

        private static void OpenDocumentation(string relativePath)
        {
            string fullPath = GetDocumentationPath(relativePath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("ReunionMovement", $"未找到文档：\n{fullPath}", "确定");
                return;
            }

            Application.OpenURL(new Uri(fullPath).AbsoluteUri);
        }

        private static string GetDocumentationPath(string relativePath)
        {
            string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // 导入到普通 Unity 项目时，优先使用项目根目录下的 Docs。
            string projectDocsPath = Path.Combine(projectRoot, "Docs", normalizedPath);
            if (File.Exists(projectDocsPath)) return projectDocsPath;

            // 当前源码仓库的 Unity 工程位于 Unity/ReunionMovement，完整文档位于仓库根 Docs。
            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
            string repositoryDocsPath = Path.Combine(repositoryRoot, "Docs", normalizedPath);
            if (File.Exists(repositoryDocsPath)) return repositoryDocsPath;

            // .unitypackage 只导出 Assets/ReunionMovement；完整文档不在接收方项目时回退到随包简明指南。
            string bundledGuidePath = Path.Combine(projectRoot,
                BundledGuidePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(bundledGuidePath) ? bundledGuidePath : projectDocsPath;
        }

        private enum DiagnosticLevel
        {
            Passed,
            Warning,
            Error
        }

        private sealed class DiagnosticItem
        {
            public readonly DiagnosticLevel Level;
            public readonly string Title;
            public readonly string Message;
            public readonly string ActionLabel;
            public readonly Action Action;

            public DiagnosticItem(DiagnosticLevel level, string title, string message, string actionLabel, Action action)
            {
                Level = level;
                Title = title;
                Message = message;
                ActionLabel = actionLabel;
                Action = action;
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
