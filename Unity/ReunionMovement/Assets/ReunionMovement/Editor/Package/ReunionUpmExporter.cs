using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ReunionMovement.Editor.Shared;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools.Package
{
    /// <summary>
    /// 导出可被 Package Manager 安装的 UPM 包。
    ///
    /// <para>
    /// 与 <see cref="ReunionMovementPackageExporter"/> 的区别：
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
    /// 菜单：ReunionMovement → 打包 → 导出 UPM 包（运行时 / 完整）
    /// 命令行：Unity -batchmode -quit -projectPath ... -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportRuntimeBatch
    /// </summary>
    public static class ReunionUpmExporter
    {
        public enum PackageProfile
        {
            Runtime,
            Full,
        }

        private const string PackageName = "com.reunion.movement";
        private const string PackageVersion = "1.1.0";
        private const string DefaultOutputRelative = "Build/Packages";

        /// <summary>默认输出根（<c>&lt;project&gt;/Build/Packages</c>）。</summary>
        public static string DefaultOutputRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", DefaultOutputRelative));

        [MenuItem("ReunionMovement/打包/导出 UPM 包（运行时）", false, 131)]
        public static void ExportRuntimeFromMenu() => ExportWithDialog(PackageProfile.Runtime);

        [MenuItem("ReunionMovement/打包/导出 UPM 包（完整）", false, 132)]
        public static void ExportFullFromMenu() => ExportWithDialog(PackageProfile.Full);

        /// <summary>命令行入口：运行时 profile，输出到默认目录。</summary>
        public static void ExportRuntimeBatch() => Batch(PackageProfile.Runtime);

        /// <summary>命令行入口：完整 profile，输出到默认目录。</summary>
        public static void ExportFullBatch() => Batch(PackageProfile.Full);

        private static void Batch(PackageProfile profile)
        {
            bool ok;
            try
            {
                ok = Export(profile, DefaultOutputRoot, reveal: false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReunionUpm] 导出失败: {ex}");
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
            string sourceRoot = AssetToAbsolute(ReunionPaths.Root);
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError($"[ReunionUpm] 框架根目录不存在: {sourceRoot}");
                return false;
            }

            string packageDir = Path.GetFullPath(Path.Combine(outputRoot, PackageName));
            string sourceFull = Path.GetFullPath(sourceRoot);
            if (packageDir.StartsWith(sourceFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[ReunionUpm] 输出目录不能位于框架源码目录内部");
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
            WriteText(Path.Combine(packageDir, "package.json"), BuildPackageJson());
            WriteText(Path.Combine(packageDir, "README.md"), BuildReadme(profile));
            WriteText(Path.Combine(packageDir, "CHANGELOG.md"), BuildChangelog());

            // 5. 校验 + 报告
            if (!Validate(packageDir, out string error))
            {
                Debug.LogError($"[ReunionUpm] 包校验失败: {error}");
                return false;
            }

            long bytes = DirectorySize(packageDir);
            Debug.Log($"[ReunionUpm] 导出完成: {packageDir}（{profile}，{copied} 个源文件，" +
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

        private static string BuildPackageJson()
        {
            return @"{
  ""name"": ""__NAME__"",
  ""displayName"": ""Reunion Movement"",
  ""version"": ""__VERSION__"",
  ""unity"": ""6000.3"",
  ""description"": ""Unity game framework: pure C# lifecycle, modular systems, four-transport networking, Addressables hot update, SDF shape UI."",
  ""author"": { ""name"": ""Reunion Movement"" },
  ""license"": ""Apache-2.0"",
  ""keywords"": [ ""unity"", ""framework"", ""ugui"", ""addressables"", ""unitask"" ],
  ""dependencies"": {
    ""com.unity.addressables"": ""2.9.1"",
    ""com.unity.inputsystem"": ""1.18.0"",
    ""com.unity.nuget.newtonsoft-json"": ""3.2.2"",
    ""com.unity.ugui"": ""2.0.0"",
    ""com.unity.render-pipelines.universal"": ""17.3.0""
  }
}
".Replace("__NAME__", PackageName).Replace("__VERSION__", PackageVersion);
        }

        private static string BuildReadme(PackageProfile profile)
        {
            return $@"# Reunion Movement

Unity 6000.3 游戏框架（{profile} 包）。

## 安装

在目标工程的 `Packages/manifest.json` 中加入（路径按实际导出位置调整）：

```json
""com.reunion.movement"": ""file:../Reunion-Movement/Build/Packages/{PackageName}""
```

或 Package Manager → `+` → `Add package from disk...`，选择本目录的 `package.json`。

## 依赖

- Addressables 2.9.1
- Input System 1.18.0
- uGUI 2.0.0
- URP 17.3.0

## 注意

- 包安装时，编辑器生成物写到 `Assets/ReunionMovementGenerated/`（包目录只读）。
- {(profile == PackageProfile.Runtime
                ? "运行时包不含示例场景、示例 UI、字体与第三方演示资源；请自备字体/界面。"
                : "完整包含示例场景、字体与第三方演示资源；导入后按需加入 Build Settings。")}
- 入口：`[assembly: ReunionGameEntry(typeof(MyGameEntry))]`，或 `Bootstrap.Options.Entry`。
  没有入口时 Bootstrap 会报错并跳过启动，不会接管宿主工程。

更多文档见 `Documentation~/`。
";
        }

        private static string BuildChangelog()
        {
            return $@"# Changelog

## [{PackageVersion}] - 2026-10-22

### Added
- 标准 UPM 包结构（package.json / README / CHANGELOG / LICENSE / Documentation~）。
- 运行时 / 完整两种导出 profile。

### Changed
- 编辑器工具通过 `ReunionPaths` 解析框架根，不再硬编码 `Assets/ReunionMovement`。
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
