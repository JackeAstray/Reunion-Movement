using System.Collections.Generic;
using System.IO;
using ReunionMovement.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ReunionMovement.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 资源迁移工具 —— 将 Resources 下的资源"复制"到 AddressableAssets 并重映射 GUID 依赖，
    /// 再标记 Addressable（分组 + Label + Address）。
    /// 仅复制、不删除源资源：同步 Resources 路径零破坏，异步 Addressables 路径独立可用（双轨过渡期安全）。
    /// 设计文档：仓库 Docs/Addressables/Addressables集成设计方案.md §4、§8 Phase1
    /// 推荐入口：ReunionMovement → Addressables → 迁移 → 一键迁移全部（UI+音频+图片）
    /// 细分入口：迁移 → UI 资源 / 音频 / 图片（可单独补迁某类）
    /// </summary>
    public static class AddressablesMigrator
    {
        /// <summary>源 Resources 根（仅处理该目录内的资源；Resources 内依赖会被一起复制）</summary>
        private const string SrcResourcesRoot = "Assets/ReunionMovement/Resources";

        /// <summary>目标 AddressableAssets 根（镜像 Resources 相对结构）</summary>
        private const string DstAddressableRoot = "Assets/AddressableAssets/BuiltIn/UI";

        /// <summary>UI Prefab 所在源目录</summary>
        private const string SrcUIPrefabFolder = "Assets/ReunionMovement/Resources/Prefabs/UIs";

        /// <summary>UI 归入的分组</summary>
        private const string GroupName = "BuiltIn_UI";

        /// <summary>UI Addressable 逻辑地址前缀（与运行时 AddressableKeys.UIRoot 对齐）</summary>
        private const string AddressPrefix = "BuiltIn/UI/";

        /// <summary>
        /// 一键迁移全部资源（UI + 音频 + 图片）。幂等：目标已存在自动跳过复制，只补齐标记，可重复执行。
        /// 推荐入口 —— 流水线与日常发版统一调用，避免漏迁某一类资源。
        /// </summary>
        [MenuItem("ReunionMovement/Addressables/迁移/一键迁移全部（UI+音频+图片）", priority = 30)]
        public static void MigrateAll()
        {
            MigrateUI();
            MigrateSounds();
            MigrateTextures();
            Log.Debug("[AddressablesMigrator] 全部迁移完成（UI + 音频 + 图片）", channel: LogChannel.Resource);
        }

        [MenuItem("ReunionMovement/Addressables/迁移/UI 资源（复制+重映射）", priority = 31)]
        public static void MigrateUI()
        {
            // 确保配置与分组存在
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddressablesSetup.EnsureSetup();
            var group = settings.FindGroup(GroupName);
            if (group == null)
            {
                Log.Error($"[AddressablesMigrator] 分组 {GroupName} 不存在，请先执行「初始化配置」", channel: LogChannel.Resource);
                return;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SrcUIPrefabFolder });
            if (prefabGuids.Length == 0)
            {
                Log.Warning($"[AddressablesMigrator] 未在 {SrcUIPrefabFolder} 找到 UI Prefab，跳过", channel: LogChannel.Resource);
                return;
            }

            int migrated = 0;
            // 全部待复制源路径（依赖 + prefab）
            var allSrc = new List<string>();
            foreach (var pg in prefabGuids)
            {
                string srcPrefab = AssetDatabase.GUIDToAssetPath(pg);
                allSrc.Add(srcPrefab);

                // prefab 的全部依赖中，仅复制 Resources 内的（非 Resources 依赖如脚本/内置 Shader 直接引用原 GUID 即可）
                foreach (var dep in AssetDatabase.GetDependencies(srcPrefab, true))
                {
                    if (string.IsNullOrEmpty(dep)) continue;
                    if (!dep.StartsWith(SrcResourcesRoot + "/", System.StringComparison.Ordinal)) continue;
                    if (dep == srcPrefab) continue;
                    if (AssetDatabase.IsValidFolder(dep)) continue;
                    if (!allSrc.Contains(dep)) allSrc.Add(dep);
                }
            }

            // 阶段一：全部复制并建立 旧GUID → 新GUID 映射
            var guidMap = new Dictionary<string, string>();
            foreach (var src in allSrc)
            {
                CopyAssetWithGuid(guidMap, src);
            }

            // 阶段二：对所有复制出的 YAML 资源重映射 GUID 引用（prefab/mat/asset/anim 等）
            foreach (var src in allSrc)
            {
                string dst = ToDstPath(src);
                if (dst != null && File.Exists(dst) && IsYamlAsset(dst))
                {
                    RemapGuids(dst, guidMap);
                }
            }

            // 阶段三：标记 UI Prefab 为 Addressable（依赖作为隐含依赖自动打进 Bundle，不单独标记）
            foreach (var pg in prefabGuids)
            {
                string srcPrefab = AssetDatabase.GUIDToAssetPath(pg);
                string dstPrefab = ToDstPath(srcPrefab);
                if (dstPrefab == null || !File.Exists(dstPrefab)) continue;

                string dstGuid = AssetDatabase.AssetPathToGUID(dstPrefab);
                var entry = settings.CreateOrMoveEntry(dstGuid, group, false, true);
                if (entry == null) continue;

                string uiName = Path.GetFileNameWithoutExtension(dstPrefab);
                entry.SetAddress(AddressPrefix + uiName, false);
                entry.SetLabel("builtin", true, false, false);
                entry.SetLabel("ui", true, false, false);
                migrated++;
                Log.Debug($"[AddressablesMigrator] 已迁移 UI: {dstPrefab}  address={AddressPrefix + uiName}", channel: LogChannel.Resource);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log.Debug($"[AddressablesMigrator] 完成：迁移 {migrated} 个 UI Prefab（含 Resources 内依赖 {allSrc.Count - prefabGuids.Length} 个）到 {DstAddressableRoot}", channel: LogChannel.Resource);
        }

        /// <summary>复制单个资源到目标路径并登记 GUID 映射；已存在则仅登记映射（幂等）</summary>
        private static void CopyAssetWithGuid(Dictionary<string, string> guidMap, string srcPath)
        {
            string dstPath = ToDstPath(srcPath);
            if (dstPath == null) return;

            string srcGuid = AssetDatabase.AssetPathToGUID(srcPath);
            if (string.IsNullOrEmpty(srcGuid)) return;

            if (!File.Exists(dstPath))
            {
                EnsureFolder(Path.GetDirectoryName(dstPath).Replace('\\', '/'));
                if (!AssetDatabase.CopyAsset(srcPath, dstPath))
                {
                    Log.Warning($"[AddressablesMigrator] 复制失败: {srcPath}", channel: LogChannel.Resource);
                    return;
                }
                AssetDatabase.ImportAsset(dstPath);

                // 同步导入设置（Texture 的 Sprite 模式、pixelsPerUnit 等）
                CopyImportSettings(srcPath, dstPath);
                AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceUpdate);
            }

            string dstGuid = AssetDatabase.AssetPathToGUID(dstPath);
            if (!string.IsNullOrEmpty(dstGuid) && srcGuid != dstGuid)
            {
                guidMap[srcGuid] = dstGuid;
            }
        }

        /// <summary>
        /// 手动复制关键导入设置（不依赖 SetImportSettings —— 该 API 在部分 Unity 版本不可用）。
        /// 目前覆盖 TextureImporter（png 的 Sprite 模式最关键）；其他类型按需扩展。
        /// </summary>
        private static void CopyImportSettings(string srcPath, string dstPath)
        {
            var srcImporter = AssetImporter.GetAtPath(srcPath);
            var dstImporter = AssetImporter.GetAtPath(dstPath);
            if (srcImporter == null || dstImporter == null) return;

            if (srcImporter is TextureImporter srcTex && dstImporter is TextureImporter dstTex)
            {
                // 注：spriteMeshType/spriteExtrude 在 Unity 6000.3 已移除，spritePackingTag 已过时（改用 SpriteAtlas），故不复制
                dstTex.textureType = srcTex.textureType;
                dstTex.spriteImportMode = srcTex.spriteImportMode;
                dstTex.spritePixelsPerUnit = srcTex.spritePixelsPerUnit;
                dstTex.spritePivot = srcTex.spritePivot;
                dstTex.spriteBorder = srcTex.spriteBorder;
                dstTex.alphaIsTransparency = srcTex.alphaIsTransparency;
                dstTex.alphaSource = srcTex.alphaSource;
                dstTex.mipmapEnabled = srcTex.mipmapEnabled;
                dstTex.filterMode = srcTex.filterMode;
                dstTex.wrapMode = srcTex.wrapMode;
                dstTex.textureCompression = srcTex.textureCompression;
                dstTex.sRGBTexture = srcTex.sRGBTexture;
                dstTex.maxTextureSize = srcTex.maxTextureSize;
                dstTex.SaveAndReimport();
            }
            // 其他导入器类型（prefab/mat 等）无关键导入设置，跳过
        }

        /// <summary>把 YAML 资源内的旧 GUID 引用替换为新 GUID（仅替换 "guid: xxx" 精确形式）</summary>
        private static void RemapGuids(string assetPath, Dictionary<string, string> guidMap)
        {
            if (guidMap.Count == 0) return;
            string content;
            try { content = File.ReadAllText(assetPath); }
            catch { return; }

            bool changed = false;
            foreach (var kv in guidMap)
            {
                string oldToken = "guid: " + kv.Key;
                if (content.IndexOf(oldToken, System.StringComparison.Ordinal) >= 0)
                {
                    content = content.Replace(oldToken, "guid: " + kv.Value);
                    changed = true;
                }
            }
            if (changed)
            {
                File.WriteAllText(assetPath, content);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>源路径 → 目标路径（仅处理 Resources 内资源）</summary>
        private static string ToDstPath(string srcPath)
        {
            if (!srcPath.StartsWith(SrcResourcesRoot + "/", System.StringComparison.Ordinal)) return null;
            string rel = srcPath.Substring(SrcResourcesRoot.Length + 1);
            return DstAddressableRoot + "/" + rel;
        }

        /// <summary>判断是否为 YAML 文本资源（需要 GUID 重映射的类型）</summary>
        private static bool IsYamlAsset(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".prefab":
                case ".mat":
                case ".asset":
                case ".anim":
                case ".controller":
                case ".spriteatlas":
                case ".playable":
                case ".mixer":
                case ".overridecontroller":
                case ".physicmaterial":
                case ".physicsmaterial2d":
                    return true;
                default:
                    return false;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        // =====================================================================
        //  Remote 分组迁移（音频 / 纹理 —— 叶子资源，无 GUID 依赖，仅复制+标记）
        // =====================================================================

        /// <summary>迁移音频：Resources/Sounds → AddressableAssets/Remote/Sounds（Remote_Sounds 分组）</summary>
        [MenuItem("ReunionMovement/Addressables/迁移/音频（Sounds → Remote_Sounds）", priority = 32)]
        public static void MigrateSounds()
        {
            MigrateLeafAssets(
                srcRoot: "Assets/ReunionMovement/Resources/Sounds",
                dstRoot: "Assets/AddressableAssets/Remote/Sounds",
                groupName: "Remote_Sounds",
                label: "sound",
                addressPrefix: "Remote/Sounds/");
        }

        /// <summary>迁移纹理：Resources/UI/Sprites → AddressableAssets/Remote/Textures（Remote_Textures 分组）</summary>
        [MenuItem("ReunionMovement/Addressables/迁移/图片（UI Sprites → Remote_Textures）", priority = 33)]
        public static void MigrateTextures()
        {
            MigrateLeafAssets(
                srcRoot: "Assets/ReunionMovement/Resources/UI/Sprites",
                dstRoot: "Assets/AddressableAssets/Remote/Textures",
                groupName: "Remote_Textures",
                label: "texture",
                addressPrefix: "Remote/Textures/");
        }

        /// <summary>
        /// 通用迁移：把源目录下全部资源复制到目标目录（镜像相对结构）、同步导入设置、
        /// 标记 Addressable（分组 + remote label + 功能 label + 地址）。幂等：目标已存在则跳过复制，只补齐标记。
        /// </summary>
        private static void MigrateLeafAssets(string srcRoot, string dstRoot, string groupName, string label, string addressPrefix)
        {
            if (!Directory.Exists(srcRoot))
            {
                Log.Warning($"[AddressablesMigrator] 源目录不存在: {srcRoot}", channel: LogChannel.Resource);
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddressablesSetup.EnsureSetup();
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                Log.Error($"[AddressablesMigrator] 分组 {groupName} 不存在，请先执行「初始化配置」", channel: LogChannel.Resource);
                return;
            }

            // 源文件清单（不含 .meta）
            var srcFiles = new List<string>();
            foreach (var f in Directory.GetFiles(srcRoot, "*.*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(".renderTexture", System.StringComparison.OrdinalIgnoreCase)) continue; // 临时渲染纹理不迁移
                srcFiles.Add(f.Replace('\\', '/'));
            }
            if (srcFiles.Count == 0)
            {
                Log.Warning($"[AddressablesMigrator] {srcRoot} 下无资源文件，跳过", channel: LogChannel.Resource);
                return;
            }

            int migrated = 0, skipped = 0;
            foreach (var src in srcFiles)
            {
                // 镜像相对路径
                string rel = src.Substring(srcRoot.Length + 1);
                string dst = (dstRoot + "/" + rel).Replace('\\', '/');

                // 复制（幂等）
                if (!File.Exists(dst))
                {
                    EnsureFolder(Path.GetDirectoryName(dst).Replace('\\', '/'));
                    if (!AssetDatabase.CopyAsset(src, dst))
                    {
                        Log.Warning($"[AddressablesMigrator] 复制失败: {src}", channel: LogChannel.Resource);
                        continue;
                    }
                    AssetDatabase.ImportAsset(dst);
                    CopyImportSettings(src, dst);
                    AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    skipped++;
                }

                // 标记 Addressable（分组 + Label + 地址）
                string dstGuid = AssetDatabase.AssetPathToGUID(dst);
                if (string.IsNullOrEmpty(dstGuid)) continue;

                var entry = settings.CreateOrMoveEntry(dstGuid, group, false, true);
                if (entry == null) continue;

                string relNoExt = rel;
                int dot = relNoExt.LastIndexOf('.');
                if (dot >= 0) relNoExt = relNoExt.Substring(0, dot);
                entry.SetAddress(addressPrefix + relNoExt, false);
                entry.SetLabel("remote", true, false, false);
                entry.SetLabel(label, true, false, false);
                migrated++;
                Log.Debug($"[AddressablesMigrator] 已迁移: {dst}  address={addressPrefix + relNoExt}", channel: LogChannel.Resource);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log.Debug($"[AddressablesMigrator] 完成：{groupName} 迁移 {migrated} 个（跳过已存在 {skipped} 个）→ {dstRoot}", channel: LogChannel.Resource);
        }
    }
}
