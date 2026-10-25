using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ReunionMovement.Editor.Shared;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Toolbox
{
    /// <summary>工具箱的数据采集部分：只读，不改工程。</summary>
    public sealed partial class ReunionMovementToolboxWindow
    {
        private static readonly string AssetRoot = ReunionPaths.Root;
        private static readonly string GameConfigPath =
            ReunionPaths.InFramework("Resources/ScriptableObjects/GameConfig.asset");
        private static readonly string BundledGuidePath =
            ReunionPaths.InFramework("Documentation/ReunionMovement 使用指南.md");

        private static readonly string[] AddressablesGroups =
        {
            "BuiltIn_Config", "BuiltIn_UI", "BuiltIn_Prefabs", "BuiltIn_Fonts", "BuiltIn_Shaders",
            "Remote_Sounds", "Remote_Textures", "Remote_Scenes", "Remote_AutoDatabase",
        };

        private static readonly string[] AddressablesLabels =
        {
            "builtin", "remote", "ui", "sound", "texture", "scene", "data",
        };

        private static readonly string[] AddressablesProfiles = { "DevLocal", "Publish" };

        private bool addressablesSettingsFound;
        private string addressablesSettingsPath;
        private string addressablesCheckMessage;
        private bool addressablesStructureValid;

        /// <summary>GameConfig.enableHotUpdate —— Addressables 的唯一开关（默认关闭）。</summary>
        private bool hotUpdateEnabled;

        // ---------------------------------------------------------------- 诊断收集

        private void CollectDiagnostics()
        {
            diagnostics.Clear();

            AddUnityVersionCheck();
            AddPackageCheck("com.unity.addressables", "Addressables", "2.9.1");
            AddPackageCheck("com.unity.inputsystem", "Input System", "1.18.0");
            AddAssetCheck(AssetRoot, "框架资产根目录", DiagnosticCategory.Project, true,
                () => PingFolder(AssetRoot));
            AddGameConfigCheck();
            RefreshAddressablesStatus();
            AddAddressablesSettingsCheck();
            AddAssetCheck(AssetRoot + "/Plugins/Generators/ReunionMovement.SourceGenerator.dll",
                "源码生成器", DiagnosticCategory.Project, false,
                () => SelectAsset(AssetRoot + "/Plugins/Generators/ReunionMovement.SourceGenerator.dll"),
                "未找到源码生成器；已生成的代码仍可用，但自动生成能力不可用。");
            AddDocumentationCheck("00-快速开始.md", "快速开始");
            AddDocumentationCheck("05-常见陷阱.md", "常见陷阱");
            AddDocumentationCheck("Addressables/快速开始.md", "Addressables 快速开始");
            AddDocumentationCheck("Packaging/打包指南.md", "打包指南");
        }

        private void AddUnityVersionCheck()
        {
            string[] parts = Application.unityVersion.Split('.');
            int major = 0;
            int minor = 0;
            bool parsed = parts.Length >= 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
            bool supported = parsed && (major > 6000 || (major == 6000 && minor >= 3));

            diagnostics.Add(new DiagnosticItem(
                supported ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                DiagnosticCategory.Environment,
                "Unity 版本",
                supported
                    ? "当前 " + Application.unityVersion + "，符合项目建议的 6000.3+。"
                    : "当前 " + Application.unityVersion + "；项目建议 Unity 6000.3+。",
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
                    diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Error, DiagnosticCategory.Package, displayName,
                        "未检测到 " + packageName + "；相关程序集可能无法编译。",
                        "Package Manager",
                        () => ExecuteMenu("Window/Package Manager")));
                    return;
                }

                bool tested = string.Equals(package.version, testedVersion, StringComparison.OrdinalIgnoreCase);
                diagnostics.Add(new DiagnosticItem(
                    tested ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                    DiagnosticCategory.Package,
                    displayName,
                    tested
                        ? "已安装经项目验证的 " + package.name + "@" + package.version + "。"
                        : "已安装 " + package.name + "@" + package.version + "；验证版本为 " + testedVersion + "，请自行确认兼容性。",
                    null,
                    null));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, DiagnosticCategory.Package, displayName,
                    "Package Manager 状态暂时不可读：" + ex.Message,
                    "Package Manager",
                    () => ExecuteMenu("Window/Package Manager")));
            }
        }

        private void AddAssetCheck(string path, string title, DiagnosticCategory category, bool required, Action foundAction,
            string missingMessage = null)
        {
            bool exists = AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;
            diagnostics.Add(new DiagnosticItem(
                exists ? DiagnosticLevel.Passed : required ? DiagnosticLevel.Error : DiagnosticLevel.Warning,
                category,
                title,
                exists ? "已找到：" + path : missingMessage ?? ("未找到：" + path),
                exists ? "定位" : null,
                exists ? foundAction : null));
        }

        private void AddGameConfigCheck()
        {
            UnityEngine.Object config = AssetDatabase.LoadMainAssetAtPath(GameConfigPath);
            if (config == null)
            {
                if (File.Exists(GameConfigPath))
                {
                    diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, DiagnosticCategory.Config, "GameConfig 无法加载",
                        "路径已存在，但资产或脚本不可用。请检查 Console；工具箱不会覆盖它。",
                        "定位目录", () => PingFolder(AssetRoot + "/Resources/ScriptableObjects")));
                    return;
                }

                diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, DiagnosticCategory.Config, "GameConfig",
                    "配置缺失，框架使用静态默认值。可显式创建默认配置（热更新关闭）。",
                    "创建…", CreateGameConfig));
                return;
            }

            var serialized = new SerializedObject(config);
            SerializedProperty hotUpdate = serialized.FindProperty("enableHotUpdate");
            if (hotUpdate == null)
            {
                diagnostics.Add(new DiagnosticItem(DiagnosticLevel.Warning, DiagnosticCategory.Config, "GameConfig",
                    "配置缺少 enableHotUpdate 字段，请核对脚本版本。",
                    "定位", () => SelectAsset(GameConfigPath)));
                return;
            }

            hotUpdateEnabled = hotUpdate.boolValue;

            diagnostics.Add(new DiagnosticItem(
                hotUpdate.boolValue ? DiagnosticLevel.Warning : DiagnosticLevel.Passed,
                DiagnosticCategory.Config,
                "运行模式",
                hotUpdate.boolValue
                    ? "热更新开启：运行时会走远程 Addressables。首次运行建议关闭，并先验证远程 URL 与 Catalog。"
                    : "热更新关闭：走 Resources，符合首次运行路径。",
                "定位", () => SelectAsset(GameConfigPath)));
        }

        private void RefreshAddressablesStatus()
        {
            addressablesSettingsFound = false;
            addressablesSettingsPath = null;
            addressablesStructureValid = false;
            addressablesCheckMessage = "未发现注册的 Addressables Settings；可按需显式初始化。";

            try
            {
                Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Unity.Addressables.Editor", StringComparison.Ordinal));
                Type defaultObjectType = editorAssembly?.GetType(
                    "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject", false);
                MethodInfo getSettings = defaultObjectType?.GetMethod("GetSettings", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(bool) }, null);
                if (getSettings == null)
                {
                    addressablesCheckMessage = "Addressables Editor API 暂不可用；等包解析与脚本编译完成后重新检查。";
                    return;
                }

                object settings = getSettings.Invoke(null, new object[] { false });
                if (settings == null) return;

                addressablesSettingsFound = true;
                addressablesSettingsPath = AssetDatabase.GetAssetPath(settings as UnityEngine.Object);

                Type settingsType = settings.GetType();
                var missing = new List<string>();

                MethodInfo findGroup = settingsType.GetMethod("FindGroup", new[] { typeof(string) });
                if (findGroup != null)
                {
                    foreach (string group in AddressablesGroups)
                    {
                        if (findGroup.Invoke(settings, new object[] { group }) == null) missing.Add("分组 " + group);
                    }
                }

                var labels = settingsType.GetMethod("GetLabels", Type.EmptyTypes)?.Invoke(settings, null) as IEnumerable<string>;
                if (labels != null)
                {
                    string[] labelArray = labels.ToArray();
                    foreach (string label in AddressablesLabels)
                    {
                        if (!labelArray.Contains(label)) missing.Add("Label " + label);
                    }
                }

                object profiles = settingsType.GetProperty("profileSettings")?.GetValue(settings);
                var names = profiles?.GetType().GetMethod("GetAllProfileNames", Type.EmptyTypes)?.Invoke(profiles, null) as IEnumerable<string>;
                if (names != null)
                {
                    string[] nameArray = names.ToArray();
                    foreach (string profile in AddressablesProfiles)
                    {
                        if (!nameArray.Contains(profile)) missing.Add("Profile " + profile);
                    }
                }

                addressablesStructureValid = missing.Count == 0;
                addressablesCheckMessage = missing.Count == 0
                    ? "已注册：" + addressablesSettingsPath + "；9 个分组 / 7 个 Label / 2 个 Profile 齐全（不代表远程 URL 与内容已验证）。"
                    : "已注册：" + addressablesSettingsPath + "；缺少 " + string.Join("、", missing) + "。可点击「补齐配置」。";
            }
            catch (Exception ex)
            {
                addressablesCheckMessage = "Addressables 配置暂时无法完整检查：" + (ex.InnerException ?? ex).Message;
            }
        }

        private void AddAddressablesSettingsCheck()
        {
            // 关闭状态下不检查配置：运行时压根不走 Addressables，缺 AddressableAssetsData 是预期状态。
            if (!hotUpdateEnabled)
            {
                diagnostics.Add(new DiagnosticItem(
                    DiagnosticLevel.Passed,
                    DiagnosticCategory.Addressables,
                    "Addressables 配置",
                    addressablesSettingsFound
                        ? "Addressables 已关闭（默认走 Resources）。检测到本地已有配置 " + addressablesSettingsPath + "，关闭状态下不会被使用。"
                        : "Addressables 已关闭（默认走 Resources）：不需要 AddressableAssetsData，也不会自动创建。需要热更时到「Addressables」页启用。",
                    null, null));
                return;
            }

            bool ok = addressablesStructureValid && !string.IsNullOrEmpty(addressablesSettingsPath);
            diagnostics.Add(new DiagnosticItem(
                ok ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                DiagnosticCategory.Addressables,
                "Addressables 配置",
                addressablesCheckMessage,
                ok ? "定位" : "补齐配置…",
                ok
                    ? (Action)(() => SelectAsset(addressablesSettingsPath))
                    : () => ExecuteMenu("ReunionMovement/Addressables/配置/初始化配置（分组+Label+Profile）",
                        "初始化 Addressables 配置？",
                        "此操作会在项目中创建或补齐 Settings、分组、Label 和 Profile。")));
        }

        private void AddDocumentationCheck(string relativePath, string title)
        {
            string fullPath = GetDocumentationPath(relativePath);
            bool exists = File.Exists(fullPath);
            bool fallback = exists && string.Equals(Path.GetFileName(fullPath), "ReunionMovement 使用指南.md", StringComparison.Ordinal);

            diagnostics.Add(new DiagnosticItem(
                exists ? DiagnosticLevel.Passed : DiagnosticLevel.Warning,
                DiagnosticCategory.Docs,
                title,
                fallback ? "Docs/" + relativePath + " 不在当前项目，已回退到随包简明指南。"
                    : exists ? "已找到 Docs/" + relativePath
                    : "未找到 Docs/" + relativePath,
                exists ? "打开" : null,
                exists ? (Action)(() => OpenDocumentation(relativePath)) : null));
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
                exampleScenes.Add(new SceneOption(Path.GetFileNameWithoutExtension(path), path));
            }

            exampleScenes.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
            int selectedIndex = exampleScenes.FindIndex(scene => string.Equals(scene.Path, previousPath, StringComparison.Ordinal));
            selectedExampleSceneIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        // ---------------------------------------------------------------- 模块

        /// <summary>
        /// 反射收集 [ModuleOrder] 模块。不引用 Core 程序集（Toolbox.asmdef 的 references 为空），
        /// 因此按特性类型名匹配，属性值也走反射。
        /// </summary>
        private void CollectModules()
        {
            modules.Clear();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assemblyName == null) continue;
                if (!assemblyName.StartsWith("ReunionMovement.", StringComparison.Ordinal)) continue;
                if (assemblyName.EndsWith(".Tests", StringComparison.Ordinal)) continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface) continue;

                    object attribute = null;
                    foreach (object candidate in type.GetCustomAttributes(false))
                    {
                        if (candidate.GetType().Name == "ModuleOrderAttribute")
                        {
                            attribute = candidate;
                            break;
                        }
                    }
                    if (attribute == null) continue;

                    int order = Convert.ToInt32(attribute.GetType().GetProperty("Order")?.GetValue(attribute) ?? 0);
                    var dependencies = attribute.GetType().GetProperty("DependsOn")?.GetValue(attribute) as Type[];
                    modules.Add(new ModuleInfo(type, order, dependencies ?? Type.EmptyTypes));
                }
            }

            modules.Sort((left, right) =>
                left.Order != right.Order ? left.Order.CompareTo(right.Order) : string.CompareOrdinal(left.Name, right.Name));
        }
    }
}
