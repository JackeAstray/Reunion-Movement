using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ReunionMovement.Editor.Shared;
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

        private static readonly string Root = ReunionPaths.Root;

        /// <summary>行数基线文件：记录历史遗留的超大文件，只允许变小。</summary>
        private static readonly string BaselinePath =
            ReunionPaths.InFramework("Editor/Toolbox/Guards/file-size-baseline.txt");

        /// <summary>.Instance 使用点基线：记录每个系统的现有调用数，只允许变少。</summary>
        private static readonly string InstanceBaselinePath =
            ReunionPaths.InFramework("Editor/Toolbox/Guards/instance-usage-baseline.txt");

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
            ["ReunionMovement.Base"] = new[] { "ReunionMovement.Common", "ReunionMovement.Utils", "ReunionMovement.Utilities", "ReunionMovement.Core" },
            ["ReunionMovement.Common"] = new[] { "ReunionMovement.Utils", "ReunionMovement.Utilities", "ReunionMovement.Core" },
            ["ReunionMovement.Utilities"] = new[] { "ReunionMovement.Utils", "ReunionMovement.Core" },
            ["ReunionMovement.Utils"] = new[] { "ReunionMovement.Core" },
            ["ReunionMovement.Core"] = new[]
            {
                "ReunionMovement.EditorTools",
                "ReunionMovement.Toolbox.Editor",
                "ReunionMovement.Tests",
            },
        };

        [MenuItem("ReunionMovement/开发/守卫/运行架构检查", false, 501)]
        public static void RunAllFromMenu()
        {
            bool ok = RunAll();
            EditorUtility.DisplayDialog("架构检查", ok ? "全部通过 ✅" : "发现问题 ❌，详见 Console", "好");
        }

        [MenuItem("ReunionMovement/开发/守卫/只检查超大文件", false, 502)]
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
            bool instances = CheckInstanceUsage();
            bool orders = CheckModuleOrder();
            bool ok = sizes && metas && layers && instances && orders;

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
        public static bool CheckFileSizes() => CheckFileSizes(out _);

        /// <summary>同上，并返回违规清单（供工具箱面板展示）。</summary>
        public static bool CheckFileSizes(out IList<string> offenders)
        {
            Dictionary<string, int> baseline = LoadBaseline();
            var found = new List<string>();
            var stale = new List<string>();

            foreach (string path in EnumerateSources("*.cs"))
            {
                if (IsExcluded(path)) continue;
                int lines = File.ReadAllLines(path).Length;

                if (baseline.TryGetValue(path, out int allowed))
                {
                    if (lines > allowed)
                    {
                        found.Add($"{path}：{lines} 行，超过基线 {allowed} 行（基线只允许变小）");
                    }
                    else if (lines <= MaxLines)
                    {
                        stale.Add($"{path}：已降到 {lines} 行，可从基线移除");
                    }
                    continue;
                }

                if (lines > MaxLines) found.Add($"{path}：{lines} 行，超过阈值 {MaxLines} 行");
            }

            foreach (string s in stale) Debug.Log($"[架构守卫] 基线可清理：{s}");

            offenders = found;
            if (found.Count == 0)
            {
                Debug.Log($"[架构守卫] 行数检查通过（阈值 {MaxLines} 行，基线豁免 {baseline.Count} 个文件）");
                return true;
            }

            found.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            sb.AppendLine($"[架构守卫] {found.Count} 个文件行数违规：");
            foreach (string o in found) sb.AppendLine("  " + o);
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
        public static bool CheckMetaFiles() => CheckMetaFiles(out _);

        /// <summary>同上，并返回缺失 .meta 的文件清单。</summary>
        public static bool CheckMetaFiles(out IList<string> offenders)
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

            offenders = missing;
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

        /// <summary>
        /// 检查项 4：<c>XxxSystem.Instance</c> 使用点棘轮。
        /// <para>
        /// 项目里同时存在"单例直取"和"<c>Modules.Get&lt;T&gt;()</c> 统一访问"两套方式，
        /// 是新人最容易绕晕的地方。历史量先冻结，新增调用点立即失败——只减不增。
        /// </para>
        /// </summary>
        public static bool CheckInstanceUsage() => CheckInstanceUsage(out _);

        /// <summary>同上，并返回超基线的系统清单。</summary>
        public static bool CheckInstanceUsage(out IList<string> offenders)
        {
            Dictionary<string, int> baseline = LoadCountBaseline(InstanceBaselinePath);
            if (baseline.Count == 0)
            {
                Debug.Log("[架构守卫] 未找到 .Instance 基线，跳过该检查");
                offenders = Array.Empty<string>();
                return true;
            }

            var found = new List<string>();
            var reduced = new List<string>();
            string[] files = GetCheckedSourceFiles();

            foreach (KeyValuePair<string, int> pair in baseline)
            {
                string pattern = $@"\b{Regex.Escape(pair.Key)}\.Instance\b";
                int count = 0;
                foreach (string file in files)
                {
                    count += Regex.Matches(File.ReadAllText(file), pattern).Count;
                }

                if (count > pair.Value) found.Add($"{pair.Key}.Instance：{count} 处，超过基线 {pair.Value} 处");
                else if (count < pair.Value) reduced.Add($"{pair.Key}.Instance：{count} 处（基线 {pair.Value}，可下调）");
            }

            foreach (string r in reduced) Debug.Log($"[架构守卫] 可收紧基线：{r}");

            offenders = found;
            if (found.Count == 0)
            {
                Debug.Log($"[架构守卫] .Instance 使用点检查通过（{baseline.Count} 个系统，基线总计 {SumBaseline(baseline)} 处）");
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[架构守卫] 新增了 .Instance 调用点（应改用 Modules.Get<T>()）：");
            foreach (string o in found) sb.AppendLine("  " + o);
            Debug.LogError(sb.ToString());
            return false;
        }

        /// <summary>
        /// 检查项 5：每个 <c>ICustomSystem</c> 实现都必须声明 <c>[ModuleOrder]</c>。
        /// <para>
        /// 没有顺序声明的模块只能靠 <c>CreateModules()</c> 里的书写位置决定初始化次序，
        /// 加一行就会悄悄改变启动顺序——阶段 2 的契约要靠这条规则守住。
        /// </para>
        /// </summary>
        public static bool CheckModuleOrder()
        {
            var missing = new List<string>();
            var declaration = new Regex(@"class\s+\w+\s*:\s*[^{]{0,400}?\bICustomSystem\b", RegexOptions.Singleline);

            foreach (string path in GetCheckedSourceFiles())
            {
                if (path.IndexOf("/Tests/", StringComparison.Ordinal) >= 0) continue;
                if (path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0) continue;
                if (path.EndsWith("ModuleOrderAttribute.cs", StringComparison.Ordinal)) continue;

                string text = File.ReadAllText(path);
                if (!declaration.IsMatch(text)) continue;
                if (text.IndexOf("[ModuleOrder(", StringComparison.Ordinal) < 0)
                {
                    missing.Add(path);
                }
            }

            if (missing.Count == 0)
            {
                Debug.Log("[架构守卫] 模块顺序声明检查通过");
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[架构守卫] {missing.Count} 个 ICustomSystem 实现缺少 [ModuleOrder]：");
            foreach (string m in missing) sb.AppendLine("  " + m);
            Debug.LogError(sb.ToString());
            return false;
        }

        /// <summary>读取"名字 → 次数"形式的基线。</summary>
        private static Dictionary<string, int> LoadCountBaseline(string path)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!File.Exists(path)) return result;

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[1], out int count)) continue;
                result[parts[0]] = count;
            }
            return result;
        }

        private static int SumBaseline(Dictionary<string, int> baseline)
        {
            int sum = 0;
            foreach (int v in baseline.Values) sum += v;
            return sum;
        }

        /// <summary>按守卫的根目录与排除规则列出参与检查的源文件。</summary>
        private static string[] GetCheckedSourceFiles()
        {
            var list = new List<string>();
            foreach (string path in EnumerateSources("*.cs"))
            {
                if (IsExcluded(path)) continue;
                list.Add(path);
            }
            return list.ToArray();
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
            // JsonUtility 通过反射赋值，编译器看不到，故显式初始化以消除 CS0649
            public string name = string.Empty;
            public string[] references = Array.Empty<string>();
        }
    }
}
