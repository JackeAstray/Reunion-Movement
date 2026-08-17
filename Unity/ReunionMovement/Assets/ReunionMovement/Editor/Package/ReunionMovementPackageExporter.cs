using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Package
{
    /// <summary>
    /// ReunionMovement 打包工具 —— 一键导出 .unitypackage。
    ///
    /// 独立于 Addressables 的框架分发工具（只顺带校验 Addressables 依赖）：
    /// - 导出根目录：Assets/ReunionMovement（运行时 + Editor + Resources + 第三方插件全量）
    /// - AddressableAssetsData/ 为【项目级配置】且位于包外（Assets/AddressableAssetsData），【不随包导出】。
    ///   接收方打开项目时由 AddressablesSetup 的 [InitializeOnLoad] 自动创建分组/Label/Profile，
    ///   因此包内地址约定（BuiltIn/UI/...）与自动生成的配置始终一致。
    /// - 依赖包：com.unity.addressables@2.9.1（自动带 com.unity.scriptablebuildpipeline），需接收方自行安装。
    ///
    /// 菜单：ReunionMovement → 打包 → 导出 ReunionMovement.unitypackage
    /// 文档：Docs/Packaging/打包指南.md
    /// </summary>
    public static class ReunionMovementPackageExporter
    {
        /// <summary>导出根（覆盖 ReunionMovement 全部功能）</summary>
        private const string ExportRoot = "Assets/ReunionMovement";

        /// <summary>默认输出（相对项目根）</summary>
        private const string DefaultOutput = "Build/Packages/ReunionMovement.unitypackage";

        [MenuItem("ReunionMovement/打包/导出 ReunionMovement.unitypackage")]
        public static void ExportPackage()
        {
            if (!Directory.Exists(ExportRoot))
            {
                Debug.LogError($"[ReunionMovementPackage] 导出根目录不存在: {ExportRoot}");
                return;
            }

            // 1. 依赖检查（Addressables 未安装时提醒，仍允许导出）
            if (!IsAddressablesInstalled())
            {
                if (!EditorUtility.DisplayDialog("导出 ReunionMovement",
                        "检测到未安装 com.unity.addressables 包（ReunionMovement 运行时依赖它）。\n\n" +
                        "是否仍继续导出？（接收方导入后需自行安装 Addressables 才能正常工作）",
                        "继续导出", "取消"))
                {
                    return;
                }
            }

            // 2. 迁移提示（UI 资源未迁移到 Addressables 时提醒，属可选操作）
            if (!IsUIMigrated())
            {
                Debug.LogWarning("[ReunionMovementPackage] 提示：UI 资源尚未迁移到 Addressables（未发现 " +
                    "Assets/AddressableAssets/BuiltIn/UI）。可在打包前先执行「迁移/UI 资源」，接收方导入即可直接用 Addressables 加载；" +
                    "不迁移也能用（运行时自动降级 Resources）。");
            }

            // 3. 导出
            string output = DefaultOutput;
            var dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            AssetDatabase.ExportPackage(ExportRoot, output, ExportPackageOptions.Recurse | ExportPackageOptions.Default);

            string fullPath = Path.GetFullPath(output);
            Debug.Log($"[ReunionMovementPackage] 导出完成: {fullPath}");
            EditorUtility.RevealInFinder(fullPath);
        }

        /// <summary>检测 Addressables 包是否已安装</summary>
        private static bool IsAddressablesInstalled()
        {
            return System.Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables") != null;
        }

        /// <summary>检测 UI 资源是否已迁移（存在 AddressableAssets/BuiltIn/UI 目录即视为已迁移）</summary>
        private static bool IsUIMigrated()
        {
            return Directory.Exists("Assets/AddressableAssets/BuiltIn/UI");
        }
    }
}
