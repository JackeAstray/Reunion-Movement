using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ReunionMovement.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 配置工具 —— 自动创建 AddressableAssetsData / 分组 / Label / Profile。
    /// 首次进入项目或配置缺失时自动执行（[InitializeOnLoad]），也可通过菜单手动执行。
    /// 设计文档：仓库 Docs/Addressables/Addressables集成设计方案.md §4、§6
    /// </summary>
    [InitializeOnLoad]
    public static class AddressablesSetup
    {
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
            ("Publish",  "https://cdn.example.com/reunion/{version}"),
        };

        static AddressablesSetup()
        {
            // 延迟到编辑器空闲执行，避免导入期 API 冲突
            EditorApplication.delayCall += EnsureSetup;
        }

        [MenuItem("ReunionMovement/Addressables/初始化配置（分组+Label+Profile）")]
        public static void EnsureSetupMenu()
        {
            EnsureSetup();
            Debug.Log("[AddressablesSetup] 手动执行完成");
        }

        /// <summary>
        /// 确保 Addressables 配置存在（幂等：已存在的分组/Label/Profile 自动跳过）。
        /// </summary>
        public static void EnsureSetup()
        {
            // 若编译中返回 null，延迟重试
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureSetup;
                return;
            }

            // 不存在时自动创建并注册为默认配置（Assets/AddressableAssetsData/AddressableAssetSettings.asset）
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogWarning("[AddressablesSetup] 无法获取/创建 AddressableAssetSettings");
                return;
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
                        Debug.LogWarning($"[AddressablesSetup] 创建分组失败: {name}");
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
                Debug.Log("[AddressablesSetup] 配置创建/补齐完成（分组 + Label + Profile + 激活 DevLocal）");
            }
        }

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

        /// <summary>
        /// 手动启用远程 Catalog。
        /// 默认保持关闭（m_BuildRemoteCatalog = false），避免无 CDN 环境下生成无用远程 catalog。
        /// </summary>
        [MenuItem("ReunionMovement/Addressables/启用远程 Catalog")]
        public static void EnableRemoteCatalog()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;
            EnsureSetup();

            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AddressablesSetup] 远程 Catalog 已启用（默认关闭，Phase 3 部署 CDN 时开启）");
        }
    }
}
