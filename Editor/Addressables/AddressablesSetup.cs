using System;
using System.IO;
using Gear.Common;
using Gear.Editor.Shared;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Gear.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 配置工具 —— 创建/补齐 AddressableAssetsData、分组、Label、Profile。
    ///
    /// <para>
    /// <b>默认什么都不做</b>：不再 <c>[InitializeOnLoad]</c> 自动执行，也不会在项目里保留
    /// <c>Assets/AddressableAssetsData</c>。仓库默认不带这份数据，运行时 <c>AddressablesMode.Off</c>
    /// 全程走 Resources —— 启动更快，也不会出现"配置缺失"的告警。
    /// </para>
    ///
    /// <para>
    /// 只有显式启用后才检查安装并初始化：菜单或工具箱里点「启用 Addressables」，
    /// 它会做三件事 —— 检查 <c>com.unity.addressables</c> 是否安装、把 <c>GameConfig.enableHotUpdate</c>
    /// 置为 true（运行时的开关）、按需创建 Settings/分组/Label/Profile。
    /// 关闭时只改开关，不删除已有数据；要清理用「删除本地 Addressables 配置」。
    /// </para>
    ///
    /// 设计文档：仓库 Docs/Addressables/Addressables集成设计方案.md §4、§6
    /// </summary>
    public static class AddressablesSetup
    {
        /// <summary>运行时开关所在的配置资产（框架相对路径）。</summary>
        private const string GameConfigRelative = "Resources/ScriptableObjects/GameConfig.asset";

        private const string SettingsAssetName = "AddressableAssetSettings.asset";

        /// <summary>仓库里默认不存在的两个数据目录（名称用于删除与状态展示）。</summary>
        private const string SettingsFolder = "Assets/AddressableAssetsData";
        private const string MigratedFolder = "Assets/AddressableAssets";

        /// <summary>分组定义（名称 → 是否远程部署）。远程分组 BuildPath/LoadPath 指向 Remote 变量。</summary>
        private static readonly (string name, bool isRemote)[] GroupDefs =
        {
            ("BuiltIn_Config",     false),
            ("BuiltIn_UI",         false),
            ("BuiltIn_Prefabs",    false),
            ("BuiltIn_Fonts",      false),
            ("BuiltIn_Shaders",    false),
            ("Remote_Sounds",      true),
            ("Remote_Textures",    true),
            ("Remote_Scenes",      true),
            ("Remote_AutoDatabase", true),
        };

        /// <summary>Label 定义（与运行时 AddressableKeys 对齐）</summary>
        private static readonly string[] LabelDefs =
        {
            "builtin", "remote", "ui", "sound", "texture", "scene", "data",
        };

        /// <summary>Profile 定义（名称 → Remote.LoadPath）。Publish 的 URL 由部署方替换为真实 CDN。</summary>
        private static readonly (string name, string remoteLoadPath)[] ProfileDefs =
        {
            ("DevLocal", "http://localhost:8080/StreamingAssets"),
            ("Publish",  "https://cdn.example.com/gear/{version}"),
        };

        // ---------------------------------------------------------------- 状态

        /// <summary>运行时是否启用 Addressables —— 唯一真相是 <c>GameConfig.enableHotUpdate</c>。</summary>
        public static bool IsEnabled
        {
            get
            {
                var config = LoadConfig();
                return config != null && config.enableHotUpdate;
            }
        }

        /// <summary>Addressables 包是否已安装（类型探测，缺包时相关菜单会灰掉而不是报异常）。</summary>
        public static bool IsPackageInstalled
        {
            get
            {
                try
                {
                    return Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables", false) != null
                        || Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor", false) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>已注册的 Settings 资产路径；没有时返回 null。</summary>
        public static string SettingsPath
        {
            get
            {
                try
                {
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    return settings == null ? null : AssetDatabase.GetAssetPath(settings);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>本地是否已有 Addressables 数据目录。</summary>
        public static bool HasLocalData
        {
            get
            {
                return AssetDatabase.IsValidFolder(SettingsFolder) || AssetDatabase.IsValidFolder(MigratedFolder);
            }
        }

        /// <summary>
        /// 其他 Addressables 菜单（迁移/构建/上传/流水线）能否使用：装了包且已启用。
        /// 未启用时这些菜单会灰掉，避免"工具自己把配置创建出来"。
        /// </summary>
        public static bool CanUseAddressables
        {
            get { return IsPackageInstalled && IsEnabled; }
        }

        // ---------------------------------------------------------------- 开关

        /// <summary>
        /// 启用 Addressables：检查安装 → 打开运行时开关 → 按需初始化配置。
        /// </summary>
        /// <param name="error">失败原因</param>
        public static bool TryEnable(out string error)
        {
            error = null;

            if (!IsPackageInstalled)
            {
                error = "未检测到 com.unity.addressables。先在 Package Manager 安装（本本见 Docs/第三方依赖.md），再启用。";
                return false;
            }

            var config = LoadConfig();
            if (config == null)
            {
                if (!TryCreateConfig(out config, out error)) return false;
            }

            if (!config.enableHotUpdate)
            {
                config.enableHotUpdate = true;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            EnsureSetup();
            Log.Debug("[AddressablesSetup] 已启用 Addressables（GameConfig.enableHotUpdate = true）", channel: LogChannel.Resource);
            return true;
        }

        /// <summary>关闭 Addressables：只改运行时开关，不删除已有数据。</summary>
        public static bool TryDisable(out string error)
        {
            error = null;
            var config = LoadConfig();
            if (config == null)
            {
                error = "找不到 GameConfig（" + GearPaths.InFramework(GameConfigRelative) + "），运行时本来就走 Resources。";
                return false;
            }

            if (config.enableHotUpdate)
            {
                config.enableHotUpdate = false;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            Log.Debug("[AddressablesSetup] 已关闭 Addressables（运行时改走 Resources）", channel: LogChannel.Resource);
            return true;
        }

        /// <summary>删除本地 Addressables 数据目录（AddressableAssetsData / AddressableAssets）。</summary>
        /// <param name="error">失败原因</param>
        public static bool DeleteLocalData(out string error)
        {
            error = null;
            if (IsEnabled)
            {
                error = "Addressables 仍处于启用状态。先关闭它，再删除本地配置。";
                return false;
            }

            bool removed = false;
            if (AssetDatabase.IsValidFolder(SettingsFolder))
            {
                removed |= AssetDatabase.DeleteAsset(SettingsFolder);
            }
            if (AssetDatabase.IsValidFolder(MigratedFolder))
            {
                removed |= AssetDatabase.DeleteAsset(MigratedFolder);
            }

            if (removed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log.Debug("[AddressablesSetup] 已删除本地 Addressables 数据（需要时可重新启用生成）", channel: LogChannel.Resource);
            }
            return true;
        }

        // ---------------------------------------------------------------- 菜单

        private const string MenuEnable = "Gear/Addressables/启用 Addressables（检查安装并初始化配置）";
        private const string MenuDisable = "Gear/Addressables/关闭 Addressables（运行时改走 Resources）";
        private const string MenuEnsure = "Gear/Addressables/配置/初始化配置（分组+Label+Profile）";
        private const string MenuRemoteCatalog = "Gear/Addressables/配置/启用远程 Catalog";
        private const string MenuDelete = "Gear/Addressables/配置/删除本地 Addressables 配置";

        [MenuItem(MenuEnable, false, 401)]
        private static void EnableMenu()
        {
            if (TryEnable(out string error)) return;

            EditorUtility.DisplayDialog("启用 Addressables", error, "好");
        }

        [MenuItem(MenuEnable, true)]
        private static bool EnableMenuValidate()
        {
            Menu.SetChecked(MenuEnable, IsEnabled);
            return !IsEnabled;
        }

        [MenuItem(MenuDisable, false, 402)]
        private static void DisableMenu()
        {
            if (TryDisable(out string error)) return;
            EditorUtility.DisplayDialog("关闭 Addressables", error, "好");
        }

        [MenuItem(MenuDisable, true)]
        private static bool DisableMenuValidate()
        {
            Menu.SetChecked(MenuDisable, !IsEnabled);
            return IsEnabled;
        }

        [MenuItem(MenuEnsure, false, 410)]
        public static void EnsureSetupMenu()
        {
            if (!CanUseAddressables) return;
            EnsureSetup();
            Log.Debug("[AddressablesSetup] 手动执行完成", channel: LogChannel.Resource);
        }

        [MenuItem(MenuEnsure, true)]
        private static bool EnsureSetupMenuValidate() => CanUseAddressables;

        [MenuItem(MenuDelete, false, 412)]
        private static void DeleteMenu()
        {
            if (!HasLocalData)
            {
                EditorUtility.DisplayDialog("删除 Addressables 配置", "当前项目没有 Addressables 数据目录，无需删除。", "好");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "删除本地 Addressables 配置",
                "将删除：\n  " + SettingsFolder + "（Settings / 分组 / Label / Profile）\n  " + MigratedFolder + "（迁移产物）\n\n" +
                "迁移过的资源（源 Resources）不受影响。删除后可在需要时重新启用生成。",
                "删除", "取消");
            if (!confirmed) return;

            if (DeleteLocalData(out string error)) return;
            EditorUtility.DisplayDialog("删除 Addressables 配置", error, "好");
        }

        [MenuItem(MenuDelete, true)]
        private static bool DeleteMenuValidate() => !IsEnabled;

        // ---------------------------------------------------------------- 配置

        /// <summary>
        /// 确保 Addressables 配置存在（幂等：已存在的分组/Label/Profile 自动跳过）。
        /// 只应在用户显式启用后调用。
        /// </summary>
        public static bool EnsureSetup()
        {
            // 若编译中返回 null，延迟重试（此时用户已明确要启用，重试是安全的）
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += () => EnsureSetup();
                return false;
            }

            // 不存在时创建并注册为默认配置（Assets/AddressableAssetsData/AddressableAssetSettings.asset）
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Log.Warning("[AddressablesSetup] 无法获取/创建 AddressableAssetSettings", channel: LogChannel.Resource);
                return false;
            }

            bool changed = false;

            // 1. Labels
            var existingLabels = settings.GetLabels();
            foreach (var label in LabelDefs)
            {
                if (!existingLabels.Contains(label))
                {
                    settings.AddLabel(label);
                    changed = true;
                }
            }

            // 2. Groups（已存在也会校验：远程分组路径必须指向 Remote 变量，修复历史错误配置）
            foreach (var (name, isRemote) in GroupDefs)
            {
                var group = settings.FindGroup(name);
                if (group == null)
                {
                    group = settings.CreateGroup(name, false, false, true, null,
                        typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                    if (group == null)
                    {
                        Log.Warning($"[AddressablesSetup] 创建分组失败: {name}", channel: LogChannel.Resource);
                        continue;
                    }
                    changed = true;
                }

                // 远程分组：BuildPath / LoadPath 必须指向 Remote 变量（部署时由 Profile 决定 URL）
                if (isRemote && EnsureRemoteGroupPaths(settings, group))
                {
                    changed = true;
                }
            }

            // 3. Profiles
            var profileSettings = settings.profileSettings;
            foreach (var (name, remoteLoadPath) in ProfileDefs)
            {
                var profileId = profileSettings.GetProfileId(name);
                if (string.IsNullOrEmpty(profileId))
                {
                    profileId = profileSettings.AddProfile(name, null);
                    changed = true;
                }
                if (!string.IsNullOrEmpty(profileId) && !string.IsNullOrEmpty(remoteLoadPath))
                {
                    profileSettings.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath, remoteLoadPath);
                }
            }

            // 4. 默认激活 DevLocal（本地模拟），避免停留在 Default（其 Remote.LoadPath 为 <undefined>）
            var devLocalId = profileSettings.GetProfileId("DevLocal");
            if (!string.IsNullOrEmpty(devLocalId) && settings.activeProfileId != devLocalId)
            {
                settings.activeProfileId = devLocalId;
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Log.Debug("[AddressablesSetup] 配置创建/补齐完成（分组 + Label + Profile + 激活 DevLocal）", channel: LogChannel.Resource);
            }
            return true;
        }

        /// <summary>
        /// 启用远程 Catalog。
        /// 默认保持关闭（m_BuildRemoteCatalog = false），避免无 CDN 环境下生成无用远程 catalog。
        /// </summary>
        /// <summary>
        /// 校验远程分组的路径变量：BuildPath / LoadPath 必须指向 Remote 变量，否则强制修正。
        /// 用于修复历史/手动创建分组遗留下的 Local 路径错误（远程热更的前提）。返回是否有改动。
        /// </summary>
        private static bool EnsureRemoteGroupPaths(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) return false;

            bool changed = false;

            // 注意：不能直接使用 profileSettings.GetVariableId（internal），改用公开的 GetProfileDataById 反查变量名。
            if (!IsProfileVariable(settings, schema.BuildPath.Id, AddressableAssetSettings.kRemoteBuildPath))
            {
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                changed = true;
            }
            if (!IsProfileVariable(settings, schema.LoadPath.Id, AddressableAssetSettings.kRemoteLoadPath))
            {
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                changed = true;
            }
            return changed;
        }

        /// <summary>判断某 profile 变量 id 是否对应目标变量名（通过公开 API 反查，id 无效时返回 false）</summary>
        private static bool IsProfileVariable(AddressableAssetSettings settings, string variableId, string variableName)
        {
            if (string.IsNullOrEmpty(variableId)) return false;
            var data = settings.profileSettings.GetProfileDataById(variableId);
            return data != null && data.ProfileName == variableName;
        }

        [MenuItem(MenuRemoteCatalog, false, 411)]
        public static void EnableRemoteCatalog()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;
            EnsureSetup();

            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            AssetDatabase.SaveAssets();
            Log.Debug("[AddressablesSetup] 远程 Catalog 已启用（默认关闭，Phase 3 部署 CDN 时开启）", channel: LogChannel.Resource);
        }

        [MenuItem(MenuRemoteCatalog, true)]
        private static bool EnableRemoteCatalogValidate() => CanUseAddressables;

        // ---------------------------------------------------------------- 配置资产

        private static GameConfig LoadConfig()
        {
            string path = GearPaths.InFramework(GameConfigRelative);
            return AssetDatabase.LoadAssetAtPath<GameConfig>(path);
        }

        /// <summary>GameConfig 缺失时按默认值创建一份（走 <see cref="GearPaths.WritableRoot"/>，包安装时落到 Assets/）。</summary>
        private static bool TryCreateConfig(out GameConfig config, out string error)
        {
            config = null;
            error = null;

            if (GearPaths.IsInstalledAsPackage)
            {
                error = "框架以包安装，包内 GameConfig 不可写。把你的 GameConfig 放进 Assets/Resources/ScriptableObjects/ 后再启用。";
                return false;
            }

            string folder = GearPaths.EnsureWritableFolder("Resources/ScriptableObjects");
            if (string.IsNullOrEmpty(folder))
            {
                error = "无法创建配置目录：" + folder;
                return false;
            }

            string path = GearPaths.InWritable(GameConfigRelative);
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Log.Debug("[AddressablesSetup] 已创建 GameConfig：" + path, channel: LogChannel.Resource);
            return true;
        }
    }
}
