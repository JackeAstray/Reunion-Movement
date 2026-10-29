using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.Editor.Shared
{
    /// <summary>
    /// 框架根路径解析 —— 让编辑器工具不再硬编码 <c>Assets/ReunionMovement</c>。
    ///
    /// <para>
    /// 支持两种安装方式：
    /// 1. 源码放在 <c>Assets/ReunionMovement</c>（当前仓库的形态）；
    /// 2. 作为 UPM 包放在 <c>Packages/com.reunion.movement</c>（git URL / 本地包）。
    /// </para>
    ///
    /// <para>
    /// 约定：<b>读</b>框架资源用 <see cref="InFramework"/>；<b>写</b>生成物用 <see cref="InWritable"/>。
    /// 包安装时包目录是只读的，生成物会落到 <see cref="GeneratedRoot"/>（仍在 Assets/ 下）。
    /// </para>
    /// </summary>
    public static class ReunionPaths
    {
        private const string CoreAsmdefSuffix = "/Framework/Runtime/ReunionMovement.Core.asmdef";
        private const string FallbackRoot = "Assets/ReunionMovement";

        /// <summary>包安装时生成物的默认落点（始终位于 Assets/，可写）。</summary>
        public const string GeneratedRoot = "Assets/ReunionMovementGenerated";

        private static string root;
        private static bool resolved;

        /// <summary>框架根目录（AssetDatabase 路径，不带尾部斜杠）。</summary>
        public static string Root
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    root = Locate();
                }
                return root;
            }
        }

        /// <summary>框架是否以 UPM 包形式安装（位于 Packages/ 或 PackageCache/，不应写入）。</summary>
        public static bool IsInstalledAsPackage
        {
            get
            {
                string path = Root;
                return path.StartsWith("Packages/", StringComparison.Ordinal) ||
                       path.StartsWith("Library/PackageCache/", StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 可写根目录：源码安装时就是框架根；包安装时退到 <see cref="GeneratedRoot"/>。
        /// </summary>
        public static string WritableRoot => IsInstalledAsPackage ? GeneratedRoot : Root;

        /// <summary>拼出框架内的相对路径（只读用途）。</summary>
        public static string InFramework(string relative) => Combine(Root, relative);

        /// <summary>拼出可写根下的相对路径（生成物用途）。</summary>
        public static string InWritable(string relative) => Combine(WritableRoot, relative);

        /// <summary>
        /// 确保一个 AssetDatabase 目录存在（逐级创建）。
        /// 只应用于 <c>Assets/</c> 下的路径；包目录不可写，会在日志里报错并返回。
        /// </summary>
        public static void EnsureFolder(string assetPath)
        {
            string path = Normalize(assetPath);
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.IsValidFolder(path)) return;

            if (!IsWritable(path))
            {
                Debug.LogError($"[ReunionPaths] 目录不可写（框架处于包安装模式）: {path}。" +
                               $"请改用 {GeneratedRoot}/... 作为生成物落点。");
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        /// <summary>拼出可写目录并确保它存在，返回 AssetDatabase 路径。</summary>
        public static string EnsureWritableFolder(string relative)
        {
            string path = InWritable(relative);
            EnsureFolder(path);
            return path;
        }

        /// <summary>AssetDatabase 路径是否位于可写的 Assets/ 下。</summary>
        public static bool IsWritable(string assetPath)
        {
            string path = Normalize(assetPath);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        /// <summary>清除缓存。包被添加/移除或根目录变化后调用；Domain Reload 会自动清零。</summary>
        public static void ResetCache()
        {
            resolved = false;
            root = null;
        }

        private static string Locate()
        {
            // 用 Core 的 asmdef 反查根目录：无论框架在 Assets/ 还是 Packages/ 下都适用。
            // 不按文件名过滤，避免不同 Unity 版本对 FindAssets 名称过滤的差异。
            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(CoreAsmdefSuffix, StringComparison.Ordinal))
                {
                    return path.Substring(0, path.Length - CoreAsmdefSuffix.Length);
                }
            }

            if (AssetDatabase.IsValidFolder(FallbackRoot))
            {
                Debug.LogWarning($"[ReunionPaths] 未定位到 ReunionMovement.Core.asmdef，回退到 {FallbackRoot}");
                return FallbackRoot;
            }

            Debug.LogError("[ReunionPaths] 无法定位 ReunionMovement 框架根目录；路径相关工具将不可用。");
            return FallbackRoot;
        }

        private static string Combine(string rootPath, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return Normalize(rootPath);
            return Normalize(rootPath) + "/" + relative.Replace('\\', '/').Trim('/');
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
