using System;
using UnityEditor;
using UnityEngine;

namespace Gear.EditorTools.Toolbox
{
    /// <summary>
    /// Addressables 页：默认关闭，只有在这里启用后才检查安装并初始化配置。
    /// <para>
    /// 本页只调用已有菜单项，不复制工具逻辑（Toolbox 程序集不引用 Addressables 相关程序集，
    /// 这样即使宿主没装 Addressables 也能正常编译）。
    /// </para>
    /// </summary>
    public sealed partial class GearToolboxWindow
    {
        private const string MenuAddressablesEnable = "Gear/Addressables/启用 Addressables（检查安装并初始化配置）";
        private const string MenuAddressablesDisable = "Gear/Addressables/关闭 Addressables（运行时改走 Resources）";
        private const string MenuAddressablesEnsure = "Gear/Addressables/配置/初始化配置（分组+Label+Profile）";
        private const string MenuAddressablesRemoteCatalog = "Gear/Addressables/配置/启用远程 Catalog";
        private const string MenuAddressablesDelete = "Gear/Addressables/配置/删除本地 Addressables 配置";
        private const string MenuAddressablesMigrateAll = "Gear/Addressables/迁移/一键迁移全部（UI+音频+图片）";
        private const string MenuAddressablesBuildContent = "Gear/Addressables/构建/构建 Content（当前平台）";
        private const string MenuAddressablesGroupsWindow = "Window/Asset Management/Addressables/Groups";

        /// <summary>Addressables 包是否安装（按类型探测，未装时相关按钮禁用而不是报错）。</summary>
        private static bool IsAddressablesPackageInstalled
        {
            get
            {
                try
                {
                    return Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables", false) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        private void DrawAddressablesTab()
        {
            SectionHeader("Addressables（默认关闭）",
                "关闭时框架全程走 Resources：启动更快，不检查也不创建任何 Addressables 数据。" +
                "只有在这里启用后，才会检查包是否安装并初始化 Settings / 分组 / Label / Profile。");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusRow("运行时开关（GameConfig.enableHotUpdate）",
                    hotUpdateEnabled ? "已启用 → AddressablesMode.Remote（从 CDN 加载并检查更新）" : "已关闭 → AddressablesMode.Off（纯 Resources）",
                    hotUpdateEnabled ? MessageType.Warning : MessageType.Info);

                DrawStatusRow("Addressables 包",
                    IsAddressablesPackageInstalled ? "已安装 com.unity.addressables" : "未安装（启用会失败，请先在 Package Manager 安装）",
                    IsAddressablesPackageInstalled ? MessageType.Info : MessageType.Warning);

                DrawStatusRow("本地配置（AddressableAssetsData）",
                    addressablesSettingsFound
                        ? "已存在：" + addressablesSettingsPath + (addressablesStructureValid ? "（分组/Label/Profile 齐全）" : "（结构不完整，可点「补齐配置」）")
                        : "未创建（仓库默认不带，启用时按需生成）",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(hotUpdateEnabled || !IsAddressablesPackageInstalled))
                {
                    if (GUILayout.Button("启用（检查安装并初始化）", GUILayout.Height(24f)))
                    {
                        ExecuteMenu(MenuAddressablesEnable);
                        RefreshAll();
                    }
                }

                using (new EditorGUI.DisabledScope(!hotUpdateEnabled))
                {
                    if (GUILayout.Button("关闭（运行时改走 Resources）", GUILayout.Height(24f)))
                    {
                        ExecuteMenu(MenuAddressablesDisable);
                        RefreshAll();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hotUpdateEnabled))
                {
                    if (GUILayout.Button("补齐配置")) ExecuteMenu(MenuAddressablesEnsure);
                    if (GUILayout.Button("迁移全部资源…")) ExecuteMenu(MenuAddressablesMigrateAll,
                        "迁移全部资源？", "会复制并标记 UI、音频、图片、配置、预制体、场景与字体；源 Resources 不删除。建议先提交。");
                    if (GUILayout.Button("构建 Content…")) ExecuteMenu(MenuAddressablesBuildContent,
                        "构建 Addressables Content？", "会生成或覆盖当前平台的构建产物，请先确认 Profile 与目标平台。");
                    if (GUILayout.Button("启用远程 Catalog…")) ExecuteMenu(MenuAddressablesRemoteCatalog,
                        "启用远程 Catalog？", "会在构建时生成远程 catalog，需要配合已部署的 CDN 地址使用。");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!IsAddressablesPackageInstalled))
                {
                    if (GUILayout.Button("打开 Addressables Groups")) ExecuteMenu(MenuAddressablesGroupsWindow);
                }

                using (new EditorGUI.DisabledScope(hotUpdateEnabled || !addressablesSettingsFound))
                {
                    if (GUILayout.Button("删除本地配置…")) ExecuteMenu(MenuAddressablesDelete);
                }
            }

            if (hotUpdateEnabled && !addressablesStructureValid)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(addressablesCheckMessage, MessageType.Warning);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "关闭状态不会删除已有数据；要清理用「删除本地配置」（会一并删除 AddressableAssetsData 与迁移产物 AddressableAssets）。\n" +
                "开启后运行时才会初始化 catalog；远程热更还需要在 GameConfig 里填 remoteBundleUrl / remoteCatalogUrl。",
                MessageType.None);
        }

        private static void DrawStatusRow(string label, string value, MessageType type)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(240f));
                EditorGUILayout.LabelField(value, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
