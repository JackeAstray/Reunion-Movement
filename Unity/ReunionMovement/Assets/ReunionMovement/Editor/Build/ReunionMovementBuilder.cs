using System;
using System.Collections.Generic;
using System.IO;
using ReunionMovement.Editor.Shared;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ReunionMovement.EditorTools.Build
{
    /// <summary>
    /// 多平台构建入口：菜单 + 命令行。
    ///
    /// <para>
    /// 菜单：<c>ReunionMovement → 构建 → Windows / macOS / Linux / Android / WebGL / iOS</c>。
    /// 命令行（当前 Active Build Target）：
    /// </para>
    ///
    /// <code>
    /// Unity -batchmode -quit -projectPath Unity/ReunionMovement \
    ///   -executeMethod ReunionMovement.EditorTools.Build.ReunionMovementBuilder.BuildFromCommandLine
    /// </code>
    ///
    /// <para>
    /// 或显式指定目标（不依赖 Active Target）：
    /// </para>
    ///
    /// <code>
    /// Unity -batchmode -quit -projectPath Unity/ReunionMovement \
    ///   -executeMethod ReunionMovement.EditorTools.Build.ReunionMovementBuilder.BuildFromCommandLineWithTarget \
    ///   -reunionTarget Android
    /// </code>
    ///
    /// <para>
    /// 输出统一在 <c>&lt;project&gt;/Build/&lt;Platform&gt;/</c>；场景取 Build Settings 里启用的项。
    /// </para>
    /// </summary>
    public static class ReunionMovementBuilder
    {
        private const string OutputRoot = "Build";

        [MenuItem("ReunionMovement/构建/Windows (x64)", false, 141)]
        public static void BuildWindows() => BuildInteractive(BuildTarget.StandaloneWindows64);

        [MenuItem("ReunionMovement/构建/macOS", false, 142)]
        public static void BuildMacOS() => BuildInteractive(BuildTarget.StandaloneOSX);

        [MenuItem("ReunionMovement/构建/Linux (x64)", false, 143)]
        public static void BuildLinux() => BuildInteractive(BuildTarget.StandaloneLinux64);

        [MenuItem("ReunionMovement/构建/Android (APK)", false, 144)]
        public static void BuildAndroid() => BuildInteractive(BuildTarget.Android);

        [MenuItem("ReunionMovement/构建/WebGL", false, 145)]
        public static void BuildWebGL() => BuildInteractive(BuildTarget.WebGL);

        [MenuItem("ReunionMovement/构建/iOS (Xcode 工程)", false, 146)]
        public static void BuildIOS() => BuildInteractive(BuildTarget.iOS);

        /// <summary>命令行入口：构建当前 Active Build Target。</summary>
        public static void BuildFromCommandLine()
        {
            bool ok = Build(EditorUserBuildSettings.activeBuildTarget);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>命令行入口：用 <c>-reunionTarget &lt;windows|macos|linux|android|webgl|ios&gt;</c> 指定目标。</summary>
        public static void BuildFromCommandLineWithTarget()
        {
            BuildTarget target = ParseTarget(GetArg("-reunionTarget")) ?? EditorUserBuildSettings.activeBuildTarget;
            bool ok = Build(target);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void BuildInteractive(BuildTarget target)
        {
            bool ok = Build(target);
            EditorUtility.DisplayDialog("ReunionMovement 构建",
                ok ? "构建成功 ✅" : "构建失败 ❌，详见 Console", "好");
        }

        private static bool Build(BuildTarget target)
        {
            if (!Preflight(target)) return false;

            string[] scenes = GetScenes();
            string output = GetOutputPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };

            Debug.Log($"[ReunionBuild] 开始构建 {target} → {output}（{scenes.Length} 个场景）");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;

            Debug.Log($"[ReunionBuild] {summary.result}：{summary.totalSize / 1024f / 1024f:F1} MB，" +
                      $"{summary.totalErrors} 个错误，耗时 {summary.totalTime}");
            return ok;
        }

        private static bool Preflight(BuildTarget target)
        {
            bool ok = true;

            if (GetScenes().Length == 0)
            {
                Debug.LogError("[ReunionBuild] Build Settings 里没有启用的场景");
                ok = false;
            }

            string linkXml = ToAbsolute(ReunionPaths.InFramework("link.xml"));
            if (!File.Exists(linkXml))
            {
                Debug.LogWarning($"[ReunionBuild] 未找到 link.xml（{linkXml}）；IL2CPP + 高裁剪可能有反射类型被裁掉");
            }

            if (target == BuildTarget.Android)
            {
                var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
                if (backend != ScriptingImplementation.IL2CPP)
                {
                    Debug.LogWarning($"[ReunionBuild] Android 建议使用 IL2CPP（当前 {backend}）");
                }
                if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                {
                    Debug.LogWarning("[ReunionBuild] Android 未勾选 ARM64；Google Play 要求 ARM64");
                }
            }

            if (target == BuildTarget.WebGL)
            {
                Debug.Log($"[ReunionBuild] WebGL 压缩: {PlayerSettings.WebGL.compressionFormat}, " +
                          $"decompressionFallback: {PlayerSettings.WebGL.decompressionFallback}");
            }

#if !UNITY_EDITOR_OSX
            if (target == BuildTarget.iOS)
            {
                Debug.LogError("[ReunionBuild] iOS 构建只能在 macOS 上执行");
                ok = false;
            }
#endif

            return ok;
        }

        private static string[] GetScenes()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }
            return scenes.ToArray();
        }

        private static string GetOutputPath(BuildTarget target)
        {
            string product = string.IsNullOrEmpty(PlayerSettings.productName)
                ? "ReunionMovement"
                : PlayerSettings.productName;
            string root = Path.Combine(ProjectRoot, OutputRoot);

            switch (target)
            {
                case BuildTarget.StandaloneWindows64: return Path.Combine(root, "Windows", product + ".exe");
                case BuildTarget.StandaloneOSX: return Path.Combine(root, "macOS", product + ".app");
                case BuildTarget.StandaloneLinux64: return Path.Combine(root, "Linux", product + ".x86_64");
                case BuildTarget.Android: return Path.Combine(root, "Android", product + ".apk");
                case BuildTarget.WebGL: return Path.Combine(root, "WebGL");
                case BuildTarget.iOS: return Path.Combine(root, "iOS");
                default: return Path.Combine(root, target.ToString());
            }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string ToAbsolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        private static BuildTarget? ParseTarget(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            switch (value.ToLowerInvariant())
            {
                case "windows":
                case "win":
                case "win64":
                    return BuildTarget.StandaloneWindows64;
                case "macos":
                case "osx":
                    return BuildTarget.StandaloneOSX;
                case "linux":
                    return BuildTarget.StandaloneLinux64;
                case "android":
                    return BuildTarget.Android;
                case "webgl":
                    return BuildTarget.WebGL;
                case "ios":
                    return BuildTarget.iOS;
                default:
                    Debug.LogWarning($"[ReunionBuild] 未知目标 {value}，使用当前 Active Target");
                    return null;
            }
        }
    }
}
