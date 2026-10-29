using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gear.Editor.Shared;
using UnityEditor;
using UnityEngine;

namespace Gear.EditorTools.Package
{
    /// <summary>
    /// 导出可被 Package Manager 安装的 UPM 包。
    ///
    /// <para>
    /// 与 <see cref="GearPackageExporter"/> 的区别：
    /// 后者导出整目录的 .unitypackage；本工具生成标准 UPM 结构（package.json / README / CHANGELOG / LICENSE /
    /// Documentation~），可以被 <c>file:</c> 或 git URL 安装，也能随版本升级。
    /// </para>
    ///
    /// <para>
    /// 两种 profile：
    /// <list type="bullet">
    /// <item><b>Full</b>：框架 + 示例 + 字体（约 190 MB），功能最完整；</item>
    /// <item><b>Runtime</b>：去掉示例场景、示例 UI、字体与第三方演示资源（约 50 MB），
    /// 适合接入自己的游戏；缺失的字体/示例需自行补。</item>
    /// </list>
    /// </para>
    ///
    /// 菜单：Gear → 打包 → 导出 UPM 包（运行时 / 完整）
    /// 命令行：Unity -batchmode -quit -projectPath ... -executeMethod Gear.EditorTools.Package.GearUpmExporter.ExportRuntimeBatch
    /// </summary>
    public static class GearUpmExporter
    {
        public enum PackageProfile
        {
            Runtime,
            Full,
            /// <summary>运行时包，但不内置 UniTask / R3 / ZString，改为 OpenUPM 依赖。</summary>
            RuntimeOpenUpm,
        }

        private const string PackageName = "com.jackeastray.gear";
        private const string PackageVersion = "1.2.0";
        private const string DefaultOutputRelative = "Build/Packages";

        /// <summary>默认输出根（<c>&lt;project&gt;/Build/Packages</c>）。</summary>
        public static string DefaultOutputRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", DefaultOutputRelative));

        [MenuItem("Gear/打包/导出 UPM 包（运行时）", false, 110)]
        public static void ExportRuntimeFromMenu() => ExportWithDialog(PackageProfile.Runtime);

        [MenuItem("Gear/打包/导出 UPM 包（完整）", false, 111)]
        public static void ExportFullFromMenu() => ExportWithDialog(PackageProfile.Full);

        [MenuItem("Gear/打包/导出 UPM 包（OpenUPM 依赖）", false, 112)]
        public static void ExportOpenUpmFromMenu() => ExportWithDialog(PackageProfile.RuntimeOpenUpm);

        /// <summary>命令行入口：运行时 profile，输出到默认目录。</summary>
        public static void ExportRuntimeBatch() => Batch(PackageProfile.Runtime);

        /// <summary>命令行入口：完整 profile，输出到默认目录。</summary>
        public static void ExportFullBatch() => Batch(PackageProfile.Full);

        /// <summary>命令行入口：OpenUPM 依赖 profile，输出到默认目录。</summary>
        public static void ExportOpenUpmBatch() => Batch(PackageProfile.RuntimeOpenUpm);

        private static void Batch(PackageProfile profile)
        {
            bool ok;
            try
            {
                ok = Export(profile, DefaultOutputRoot, reveal: false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GearUpm] 导出失败: {ex}");
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void ExportWithDialog(PackageProfile profile)
        {
            string root = DefaultOutputRoot;
            string selected = EditorUtility.SaveFolderPanel(
                $"选择 UPM 输出目录（将生成 {PackageName}/）", root, PackageName);
            if (string.IsNullOrEmpty(selected)) return;

            if (Export(profile, selected, reveal: true))
            {
                EditorUtility.DisplayDialog("导出 UPM 包",
                    $"已导出 {PackageName}（{profile}）到:\n{Path.Combine(selected, PackageName)}", "好");
            }
        }

        /// <summary>执行导出，返回是否成功。</summary>
        public static bool Export(PackageProfile profile, string outputRoot, bool reveal)
        {
            string sourceRoot = AssetToAbsolute(GearPaths.Root);
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError($"[GearUpm] 框架根目录不存在: {sourceRoot}");
                return false;
            }

            string packageDir = Path.GetFullPath(Path.Combine(outputRoot, PackageName));
            string sourceFull = Path.GetFullPath(sourceRoot);
            if (packageDir.StartsWith(sourceFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[GearUpm] 输出目录不能位于框架源码目录内部");
                return false;
            }

            // 1. 清理旧包（只清理我们自己创建的包目录）
            if (Directory.Exists(packageDir))
            {
                MakeWritable(packageDir);
                Directory.Delete(packageDir, true);
            }
            Directory.CreateDirectory(packageDir);

            // 2. 复制运行时 / 编辑器资源
            int copied = 0;
            foreach (string file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string rel = Relative(sourceRoot, file);
                if (ShouldExclude(rel, profile)) continue;

                string dst = Path.Combine(packageDir, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(file, dst, true);
                File.SetAttributes(dst, FileAttributes.Normal);
                copied++;
            }

            // 3. 文档与许可证
            CopyDirectory(Path.Combine(sourceRoot, "Documentation"),
                Path.Combine(packageDir, "Documentation~"), excludeMetas: false);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string docsDir = FindDirectoryInAncestors(projectRoot, "Docs");
            if (docsDir != null)
            {
                CopyDirectory(docsDir,
                    Path.Combine(packageDir, "Documentation~", "Docs"), excludeMetas: true);
            }

            string license = FindFileInAncestors(projectRoot, "LICENSE");
            if (license != null)
            {
                File.Copy(license, Path.Combine(packageDir, "LICENSE.md"), true);
            }
            else
            {
                WriteText(Path.Combine(packageDir, "LICENSE.md"),
                    "Apache License 2.0\n\nThis package is distributed under the Apache License 2.0.\n" +
                    "See https://www.apache.org/licenses/LICENSE-2.0 for the full text.\n");
            }

            // 4. 包元数据
            WriteText(Path.Combine(packageDir, "package.json"), BuildPackageJson(profile));
            WriteText(Path.Combine(packageDir, "README.md"), BuildReadme(profile));
            WriteText(Path.Combine(packageDir, "CHANGELOG.md"), BuildChangelog());

            // 5. 校验 + 报告
            if (!Validate(packageDir, out string error))
            {
                Debug.LogError($"[GearUpm] 包校验失败: {error}");
                return false;
            }

            long bytes = DirectorySize(packageDir);
            Debug.Log($"[GearUpm] 导出完成: {packageDir}（{profile}，{copied} 个源文件，" +
                      $"{bytes / 1024f / 1024f:F1} MB）");
            if (reveal) EditorUtility.RevealInFinder(packageDir);
            return true;
        }

        // ---------------------------------------------------------------- 排除规则

        private static bool ShouldExclude(string rel, PackageProfile profile)
        {
            if (rel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                rel.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                rel.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                rel.EndsWith(".user", StringComparison.OrdinalIgnoreCase) ||
                rel.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                rel.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) ||
                rel.EndsWith(".suo", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 永不进包：测试、文档、示例入口（Game/）
            if (rel.StartsWith("Tests/", StringComparison.Ordinal) ||
                rel.StartsWith("Documentation/", StringComparison.Ordinal) ||
                rel.StartsWith("Game/", StringComparison.Ordinal) ||
                rel.StartsWith("Library/", StringComparison.Ordinal) ||
                rel.StartsWith("Temp/", StringComparison.Ordinal) ||
                rel.StartsWith("Logs/", StringComparison.Ordinal) ||
                rel.Contains("/obj/") || rel.Contains("/bin/") ||
                rel == "Tests.meta" || rel == "Documentation.meta" || rel == "Game.meta")
            {
                return true;
            }

            // OpenUPM profile：UniTask 不内置，由 package.json 依赖提供。
            // R3 的 OpenUPM 包只有 R3.Unity，核心 R3.dll 仍需另装；ZString 需要每个 asmdef 显式引用。
            // 因此这两个继续内置，避免“看起来能切、实际编译不过”。
            if (profile == PackageProfile.RuntimeOpenUpm && IsVendoredUniTask(rel))
            {
                return true;
            }

            if (profile == PackageProfile.Full)
            {
                return false;
            }

            // Runtime profile：去掉示例、字体、演示资源
            // Editor/UIExamples 依赖 UI/Select（演示业务），两者必须一起进出包。
            if (rel.StartsWith("Editor/UIExamples/", StringComparison.Ordinal) ||
                IsExcludedRuntimeFolder(rel) || IsExcludedRuntimeMeta(rel))
            {
                return true;
            }

            return rel.Contains("/Example/", StringComparison.Ordinal) ||
                   rel.Contains("/Examples/", StringComparison.Ordinal) ||
                   rel.EndsWith("/Example.meta", StringComparison.Ordinal) ||
                   rel.EndsWith("/Examples.meta", StringComparison.Ordinal);
        }

        private static bool IsVendoredUniTask(string rel)
        {
            return rel.StartsWith("Plugins/UniTask/", StringComparison.Ordinal) ||
                   rel == "Plugins/UniTask.meta";
        }

        private static bool IsExcludedRuntimeFolder(string rel)
        {
            return rel.StartsWith("Fonts/", StringComparison.Ordinal) ||
                   rel.StartsWith("Scenes/", StringComparison.Ordinal) ||
                   rel.StartsWith("UI/Select/", StringComparison.Ordinal) ||
                   rel.StartsWith("Resources/Prefabs/UIs/", StringComparison.Ordinal) ||
                   rel.StartsWith("3rd/Joystick Pack/", StringComparison.Ordinal) ||
                   rel.StartsWith("3rd/Animated Loading Icons/", StringComparison.Ordinal) ||
                   rel.StartsWith("3rd/UI/", StringComparison.Ordinal);
        }

        private static bool IsExcludedRuntimeMeta(string rel)
        {
            switch (rel)
            {
                case "Fonts.meta":
                case "Scenes.meta":
                case "UI/Select.meta":
                case "Resources/Prefabs/UIs.meta":
                case "3rd/Joystick Pack.meta":
                case "3rd/Animated Loading Icons.meta":
                case "3rd/UI.meta":
                case "Editor/UIExamples.meta":
                    return true;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- 元数据

        private static string BuildPackageJson(PackageProfile profile)
        {
            string cysharpDeps = profile == PackageProfile.RuntimeOpenUpm
                ? @",
    ""com.cysharp.unitask"": ""2.5.11"""
                : string.Empty;

            return @"{
  ""name"": ""__NAME__"",
  ""displayName"": ""Gear"",
  ""version"": ""__VERSION__"",
  ""unity"": ""6000.3"",
  ""description"": ""Unity game framework: pure C# lifecycle, modular systems, four-transport networking, Addressables hot update, SDF shape UI."",
  ""author"": { ""name"": ""Gear"" },
  ""license"": ""Apache-2.0"",
  ""keywords"": [ ""unity"", ""framework"", ""ugui"", ""addressables"", ""unitask"" ],
  ""dependencies"": {
    ""com.unity.addressables"": ""2.9.1"",
    ""com.unity.inputsystem"": ""1.18.0"",
    ""com.unity.nuget.newtonsoft-json"": ""3.2.2""__CYSHARP__,
    ""com.unity.ugui"": ""2.0.0"",
    ""com.unity.render-pipelines.universal"": ""17.3.0""
  },
  ""samples"": [
    {
      ""displayName"": ""Hello World"",
      ""description"": ""最小入口：运行时创建一个 uGUI 文本，不依赖场景/预制体。"",
      ""path"": ""Samples~/HelloWorld""
    }
  ]
}
".Replace("__NAME__", PackageName)
 .Replace("__VERSION__", PackageVersion)
 .Replace("__CYSHARP__", cysharpDeps);
        }

        private static string BuildReadme(PackageProfile profile)
        {
            string openUpm = profile == PackageProfile.RuntimeOpenUpm
                ? @"
## OpenUPM 依赖

本包**不内置** UniTask（改用 OpenUPM）；R3 / ZString 仍内置。安装前在目标工程的 `Packages/manifest.json` 加入：

```json
""scopedRegistries"": [
  {
    ""name"": ""OpenUPM"",
    ""url"": ""https://package.openupm.com"",
    ""scopes"": [ ""com.cysharp"" ]
  }
]
```

UniTask 版本由 `package.json` 声明：2.5.11。
"
                : "";

            string runtimeNote = profile == PackageProfile.Full
                ? "完整包含示例场景、字体与第三方演示资源；导入后按需加入 Build Settings。"
                : "运行时包不含示例场景、示例 UI、字体与第三方演示资源；请自备字体/界面。";

            return $@"# Gear

Unity 6000.3 游戏框架（{profile} 包）。

## 安装

在目标工程的 `Packages/manifest.json` 中加入（路径按实际导出位置调整）：

```json
""com.jackeastray.gear"": ""file:../Gear/Build/Packages/{PackageName}""
```

或 Package Manager → `+` → `Add package from disk...`，选择本目录的 `package.json`。

## 依赖

- Addressables 2.9.1
- Input System 1.18.0
- Newtonsoft.Json 3.2.2
- uGUI 2.0.0
- URP 17.3.0
{openUpm}
## 注意

- 包安装时，编辑器生成物写到 `Assets/GearGenerated/`（包目录只读）。
- {runtimeNote}
- 入口：`[assembly: GearGameEntry(typeof(MyGameEntry))]`，或 `Bootstrap.Options.Entry`。
  没有入口时 Bootstrap 会报错并跳过启动，不会接管宿主工程。

更多文档见 `Documentation~/`。
";
        }

        private static string BuildChangelog()
        {
            return $@"# Changelog

## [{PackageVersion}] - 2026-10-25

### Added
- `ModulePresets.Full / Minimal / Without<T> / Of`：模块清单从「背依赖表」变成选一行。
- `ModuleCatalog.ExpandDependencies`：启动时按 `[ModuleOrder(..., DependsOn)]` 自动补回缺失的被依赖模块，
  由 `BootstrapOptions.AutoExpandModuleDependencies`（默认开）控制。

### Changed
- Addressables 默认关闭：仓库不再带 `Assets/AddressableAssetsData`，`AddressablesSetup` 不再自动执行；
  只有显式启用（工具箱「Addressables」页 / 菜单）时才检查包安装并按需初始化。
- 菜单顶层按段位固化：工具箱 → 打包 → 构建 → 内容 → Addressables → 开发；`导出 unitypackage` 收进 `打包`。

## [1.1.0] - 2026-10-22

### Added
- 标准 UPM 包结构（package.json / README / CHANGELOG / LICENSE / Documentation~）。
- 运行时 / 完整 / OpenUPM 依赖三种导出 profile。

### Changed
- 编辑器工具通过 `GearPaths` 解析框架根，不再硬编码 `Assets/Gear`。
- 没有入口声明时 Bootstrap 不再隐式回退 `StartGame`；可用 `BootstrapOptions.Entry` 显式指定。
";
        }

        // ---------------------------------------------------------------- 校验与工具

        private static bool Validate(string packageDir, out string error)
        {
            error = null;
            var problems = new List<string>();

            string pkg = Path.Combine(packageDir, "package.json");
            if (!File.Exists(pkg))
            {
                problems.Add("缺少 package.json");
            }
            else
            {
                string json = File.ReadAllText(pkg);
                if (!json.Contains($"\"name\": \"{PackageName}\""))
                    problems.Add("package.json 的 name 不正确");
                if (!json.Contains("\"version\""))
                    problems.Add("package.json 缺少 version");
            }

            foreach (string file in Directory.GetFiles(packageDir, "*", SearchOption.AllDirectories))
            {
                if ((file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                     file.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) &&
                    !File.Exists(file + ".meta"))
                {
                    problems.Add("缺少 .meta: " + Relative(packageDir, file));
                }
            }

            if (problems.Count > 0)
            {
                error = string.Join("; ", problems.GetRange(0, Math.Min(10, problems.Count)));
                if (problems.Count > 10) error += $"; … 共 {problems.Count} 项";
                return false;
            }
            return true;
        }

        private static string FindFileInAncestors(string startDir, string fileName, int maxDepth = 4)
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i < maxDepth && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static string FindDirectoryInAncestors(string startDir, string name, int maxDepth = 4)
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i < maxDepth && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, name);
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static void CopyDirectory(string source, string dest, bool excludeMetas)
        {
            if (!Directory.Exists(source)) return;
            if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (excludeMetas && file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string rel = Relative(source, file);
                string dst = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(file, dst, true);
                File.SetAttributes(dst, FileAttributes.Normal);
            }
        }

        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string AssetToAbsolute(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Relative(string root, string file)
        {
            string rel = file.Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace('\\', '/');
        }

        private static long DirectorySize(string dir)
        {
            long total = 0;
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                total += new FileInfo(file).Length;
            }
            return total;
        }

        private static void MakeWritable(string dir)
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }
    }
}
