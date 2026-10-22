using System;
using System.Collections.Generic;
using System.IO;
using ReunionMovement.Editor.Shared;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ReunionMovement.EditorTools.Package
{
    /// <summary>
    /// 检测“官方 UPM 包”和“框架内置副本”同时存在的情况。
    ///
    /// <para>
    /// 框架自带了 UniTask / R3 / ZString / DOTween / Newtonsoft.Json 的副本，
    /// 目的是让仓库离线可编译。接入方如果已经装了官方包，就会出现同名类型
    /// 或同名程序集（CS0433 / 同名 asmdef），编译直接失败。
    /// 这里不自动删除任何东西，只把冲突和两种处理方式打出来。
    /// </para>
    ///
    /// 菜单：ReunionMovement → 开发 → 检查第三方依赖冲突
    /// 命令行：-executeMethod ReunionMovement.EditorTools.Package.ThirdPartyDependencyChecker.CheckBatch
    /// 文档：Docs/第三方依赖.md
    /// </summary>
    public static class ThirdPartyDependencyChecker
    {
        private sealed class Rule
        {
            public readonly string Component;
            public readonly string PackageName;
            public readonly string VendorPath;

            public Rule(string component, string packageName, string vendorPath)
            {
                Component = component;
                PackageName = packageName;
                VendorPath = vendorPath;
            }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("UniTask", "com.cysharp.unitask", "Plugins/UniTask"),
            new Rule("R3", "com.cysharp.r3", "Plugins/R3"),
            new Rule("ZString", "com.cysharp.zstring", "Plugins/ZString"),
            new Rule("Newtonsoft.Json", "com.unity.nuget.newtonsoft-json", "Plugins/Newtonsoft.Json.dll"),
            new Rule("DOTween", "com.demigiant.dotween", "Plugins/DOTween"),
        };

        [MenuItem("ReunionMovement/开发/检查第三方依赖冲突", false, 63)]
        public static void CheckFromMenu()
        {
            IList<string> conflicts = Check();
            if (conflicts.Count == 0)
            {
                EditorUtility.DisplayDialog("第三方依赖", "未检测到官方包与内置副本冲突。", "好");
            }
            else
            {
                EditorUtility.DisplayDialog("第三方依赖",
                    $"检测到 {conflicts.Count} 处冲突，详见 Console：\n\n{string.Join("\n\n", conflicts)}", "好");
            }
        }

        /// <summary>命令行入口：有冲突时退出码 1。</summary>
        public static void CheckBatch()
        {
            IList<string> conflicts = Check();
            EditorApplication.Exit(conflicts.Count == 0 ? 0 : 1);
        }

        /// <summary>返回冲突描述；空列表表示没有冲突。</summary>
        public static IList<string> Check()
        {
            var conflicts = new List<string>();
            var installed = new HashSet<string>(StringComparer.Ordinal);

            foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
            {
                if (!string.IsNullOrEmpty(package.name))
                {
                    installed.Add(package.name);
                }
            }

            foreach (Rule rule in Rules)
            {
                if (!installed.Contains(rule.PackageName)) continue;

                string vendor = ToAbsolute(ReunionPaths.InFramework(rule.VendorPath));
                if (!File.Exists(vendor) && !Directory.Exists(vendor)) continue;

                string message =
                    $"{rule.Component}: 官方包 {rule.PackageName} 与框架内置副本 {rule.VendorPath} 同时存在。" +
                    "两者会产生同名类型/同名程序集（CS0433），请二选一：删除内置副本，或卸载官方包。" +
                    "详见 Docs/第三方依赖.md";
                Debug.LogError("[第三方依赖] " + message);
                conflicts.Add($"{rule.Component}：{rule.PackageName} ↔ {rule.VendorPath}");
            }

            if (conflicts.Count == 0)
            {
                Debug.Log("[第三方依赖] 未检测到官方包与内置副本冲突");
            }

            return conflicts;
        }

        private static string ToAbsolute(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
