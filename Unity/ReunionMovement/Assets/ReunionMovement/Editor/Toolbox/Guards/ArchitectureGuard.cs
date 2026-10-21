using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.Editor.Guards
{
    /// <summary>
    /// 架构守卫：把"文档里写了但没人执行"的规则变成可自动运行的检查。
    /// <para>检查项：</para>
    /// <list type="number">
    /// <item>单文件行数超过阈值（巨型类重生预警）</item>
    /// <item>源文件缺少 .meta（移动文件时最容易犯、后果最隐蔽的错）</item>
    /// <item>asmdef 依赖方向颠倒（Base ← Common ← Utils ← Core 只能单向）</item>
    /// </list>
    /// <para>CI / 命令行调用：<c>Unity -executeMethod ReunionMovement.Editor.Guards.ArchitectureGuard.RunAll -quit</c></para>
    /// </summary>
    public static class ArchitectureGuard
    {
        /// <summary>单文件行数上限。超过即视为需要拆分。</summary>
        public const int MaxLines = 600;

        private const string Root = "Assets/ReunionMovement";

        /// <summary>行数基线文件：记录历史遗留的超大文件，只允许变小。</summary>
        private const string BaselinePath = "Assets/ReunionMovement/Editor/Toolbox/Guards/file-size-baseline.txt";

        /// <summary>第三方、生成代码与示例不参与行数检查。</summary>
        private static readonly string[] ExcludedSegments =
        {
            "/Plugins/",
            "/ThirdParty/",
            "/3rd/",
            "/GenerateScript/",
            "/Examples/",
            "/Samples/",
        };

        /// <summary>每层允许引用的下层白名单（未列出的层不做方向检查）。</summary>
        private static readonly Dictionary<string, string[]> ForbiddenReferences = new Dictionary<string, string[]>
        {
            ["ReunionMovement.Base"] = new[] { "ReunionMovement.Common", "ReunionMovement.Utils", "ReunionMovement.Core" },
            ["ReunionMovement.Common"] = new[] { "ReunionMovement.Utils", "ReunionMovement.Core" },
            ["ReunionMovement.Utils"] = new[] { "ReunionMovement.Core" },
            ["ReunionMovement.Core"] = new[]
            {
                "ReunionMovement.EditorTools",
                "ReunionMovement.Toolbox.Editor",
                "ReunionMovement.Tests",
            },
        };

        [MenuItem("ReunionMovement/守卫/运行架构检查", false, 100)]
        public static void RunAllFromMenu()
        {
            bool ok = RunAll();
            EditorUtility.DisplayDialog("架构检查", ok ? "全部通过 ✅" : "发现问题 ❌，详见 Console", "好");
        }

        [MenuItem("ReunionMovement/守卫/只检查超大文件", false, 101)]
        public static void RunFileSizeOnlyFromMenu()
        {
            bool ok = CheckFileSizes();
            EditorUtility.DisplayDialog("超大文件检查", ok ? "全部通过 ✅" : "发现超大文件 ❌，详见 Console", "好");
        }

        /// <summary>执行全部检查。返回 false 表示有违规项。</summary>
        public static bool RunAll()
        {
            Debug.Log("[架构守卫] 开始检查…");
            bool sizes = CheckFileSizes();
            bool metas = CheckMetaFiles();
            bool layers = CheckAssemblyLayers();
            bool ok = sizes && metas && layers;

            if (ok)
            {
                Debug.Log("[架构守卫] 全部通过 ✅");
            }
            else
            {
                Debug.LogError("[架构守卫] 存在违规项，详见上方日志 ❌");
            }

            return ok;
        }

        /// <summary>
        /// 批处理入口：跑完检查后按结果退出 Unity，供 CI 的 -executeMethod 调用。
        /// 与 <see cref="RunAll"/> 分开，是因为测试里会直接调用 RunAll，不能被 Exit 打断。
        /// </summary>
        public static void RunAllBatch()
        {
            bool ok = RunAll();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// 检查项 1：单文件行数。
        /// <para>
        /// 采用"棘轮"策略：历史遗留的超大文件记在基线里可以暂时豁免，
        /// 但它们只能变小不能变大；未登记的文件一旦超过阈值立即失败。
        /// </para>
        /// </summary>
        public static bool CheckFileSizes()
        {
            Dictionary<string, int> baseline = LoadBaseline();
            var offenders = new List<string>();
            var stale = new List<string>();

            foreach (string path in EnumerateSources("*.cs"))
            {
                if (IsExcluded(path)) continue;
                int lines = File.ReadAllLines(path).Length;

                if (baseline.TryGetValue(path, out int allowed))
                {
                    if (lines > allowed)
                    {
                        offenders.Add($"{path}：{lines} 行，超过基线 {allowed} 行（基线只允许变小）");
                    }
                    else if (lines <= MaxLines)
                    {
                        stale.Add($"{path}：已降到 {lines} 行，可从基线移除");
                    }
                    continue;
                }

                if (lines > MaxLines) offenders.Add($"{path}：{lines} 行，超过阈值 {MaxLines} 行");
            }

            foreach (string s in stale) Debug.Log($"[架构守卫] 基线可清理：{s}");

            if (offenders.Count == 0)
            {
                Debug.Log($"[架构守卫] 行数检查通过（阈值 {MaxLines} 行，基线豁免 {baseline.Count} 个文件）");
                return true;
            }

            offenders.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            sb.AppendLine($"[架构守卫] {offenders.Count} 个文件行数违规：");
            foreach (string o in offenders) sb.AppendLine("  " + o);
            Debug.LogError(sb.ToString());
            return false;
        }

        /// <summary>读取行数基线；文件不存在时返回空表（等价于全部按阈值检查）。</summary>
        private static Dictionary<string, int> LoadBaseline()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!File.Exists(BaselinePath)) return result;

            foreach (string raw in File.ReadAllLines(BaselinePath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                int space = line.IndexOf(' ');
                if (space <= 0) continue;
                if (!int.TryParse(line.Substring(0, space), out int lines)) continue;

                string path = line.Substring(space + 1).Trim().Replace('\\', '/');
                if (path.Length > 0) result[path] = lines;
            }
            return result;
        }

        /// <summary>检查项 2：每个源文件都要有 .meta。</summary>
        public static bool CheckMetaFiles()
        {
            var missing = new List<string>();
            foreach (string path in EnumerateSources("*.cs"))
            {
                if (!File.Exists(path + ".meta")) missing.Add(path);
            }
            foreach (string path in EnumerateSources("*.asmdef"))
            {
                if (!File.Exists(path + ".meta")) missing.Add(path);
            }

            if (missing.Count == 0)
            {
                Debug.Log("[架构守卫] .meta 检查通过");
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[架构守卫] {missing.Count} 个文件缺少 .meta（GUID 会重新生成，场景/预制体引用将丢失）：");
            foreach (string m in missing) sb.AppendLine("  " + m);
            Debug.LogError(sb.ToString());
            return false;
        }

        /// <summary>检查项 3：asmdef 依赖方向。</summary>
        public static bool CheckAssemblyLayers()
        {
            var violations = new List<string>();
            foreach (string path in EnumerateSources("*.asmdef"))
            {
                string json = File.ReadAllText(path);
                AsmdefData data;
                try
                {
                    data = JsonUtility.FromJson<AsmdefData>(json);
                }
                catch (Exception ex)
                {
                    violations.Add($"{path}: asmdef 解析失败 - {ex.Message}");
                    continue;
                }

                if (data == null || string.IsNullOrEmpty(data.name)) continue;
                if (!ForbiddenReferences.TryGetValue(data.name, out string[] forbidden)) continue;

                foreach (string reference in data.references ?? Array.Empty<string>())
                {
                    // 引用可能是 "GUID:xxxx" 或直接的程序集名
                    string assemblyName = reference.StartsWith("GUID:", StringComparison.Ordinal)
                        ? ResolveAssemblyNameByGuid(reference.Substring(5))
                        : reference;

                    if (assemblyName == null) continue;
                    foreach (string bad in forbidden)
                    {
                        if (string.Equals(assemblyName, bad, StringComparison.Ordinal))
                        {
                            violations.Add($"{data.name} 不应引用上层程序集 {bad}（{path}）");
                        }
                    }
                }
            }

            if (violations.Count == 0)
            {
                Debug.Log("[架构守卫] 程序集依赖方向检查通过");
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[架构守卫] {violations.Count} 处依赖方向违规：");
            foreach (string v in violations) sb.AppendLine("  " + v);
            Debug.LogError(sb.ToString());
            return false;
        }

        /// <summary>把 GUID 形式的 asmdef 引用还原成程序集名。</summary>
        private static string ResolveAssemblyNameByGuid(string guid)
        {
            string[] guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            foreach (string candidate in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(candidate);
                if (string.IsNullOrEmpty(path)) continue;
                if (!string.Equals(AssetDatabase.AssetPathToGUID(path), guid, StringComparison.Ordinal)) continue;

                try
                {
                    var data = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(path));
                    return data?.name;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private static IEnumerable<string> EnumerateSources(string pattern)
        {
            if (!Directory.Exists(Root)) yield break;
            foreach (string path in Directory.GetFiles(Root, pattern, SearchOption.AllDirectories))
            {
                yield return path.Replace('\\', '/');
            }
        }

        private static bool IsExcluded(string path)
        {
            foreach (string segment in ExcludedSegments)
            {
                if (path.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string[] references;
        }
    }
}
