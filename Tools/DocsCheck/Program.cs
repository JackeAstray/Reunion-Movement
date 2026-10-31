using System.Text.RegularExpressions;

namespace Gear.DocsCheck;

/// <summary>
/// Markdown 本地链接检查：把 Docs/ 与根 README 里指向不存在文件的相对链接挡在 CI 里。
/// 只检查本地路径，跳过 http(s)、mailto、锚点与代码块内的示例。
/// </summary>
public static class Program
{
    private static readonly string[] ExcludedSegments =
    {
        "/.git/", "/Library/", "/Temp/", "/obj/", "/bin/", "/Build/",
    };

    private static readonly Regex LinkPattern = new Regex(
        @"\[[^\]]*\]\(\s*(<[^>]+>|[^)\s]+)(?:\s+""[^""]*"")?\s*\)",
        RegexOptions.Compiled);

    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : ".";
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"[docs] 目录不存在：{root}");
            return 2;
        }

        string rootFull = Path.GetFullPath(root);
        var files = Directory.EnumerateFiles(rootFull, "*.md", SearchOption.AllDirectories)
            .Where(path =>
            {
                string normalized = path.Replace('\\', '/');
                return !ExcludedSegments.Any(s => normalized.Contains(s, StringComparison.OrdinalIgnoreCase));
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        int linkCount = 0;
        int missing = 0;

        foreach (string file in files)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[docs] 读取失败 {file}: {ex.Message}");
                missing++;
                continue;
            }

            bool inFence = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }
                if (inFence) continue;

                foreach (Match match in LinkPattern.Matches(line))
                {
                    string target = match.Groups[1].Value.Trim();
                    if (target.StartsWith('<') && target.EndsWith('>'))
                    {
                        target = target.Substring(1, target.Length - 2);
                    }
                    if (string.IsNullOrWhiteSpace(target)) continue;
                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // 去掉锚点与查询串；链接里的 %20 等编码不展开（本仓库文档路径不含空格）
                    int cut = target.IndexOfAny(new[] { '#', '?' });
                    if (cut >= 0) target = target.Substring(0, cut);
                    if (string.IsNullOrEmpty(target)) continue;

                    linkCount++;
                    string baseDir = Path.GetDirectoryName(file) ?? rootFull;
                    string resolved = Path.GetFullPath(Path.Combine(baseDir, target.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    {
                        string relative = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
                        Console.Error.WriteLine($"{relative}:{i + 1}: 链接目标不存在 -> {target}");
                        missing++;
                    }
                }
            }
        }

        Console.WriteLine($"[docs] 扫描 {files.Count} 个 Markdown，检查 {linkCount} 个本地链接，缺失 {missing} 个");
        return missing == 0 ? 0 : 1;
    }
}
