using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Gear.SyntaxCheck;

/// <summary>
/// 不依赖 Unity 的 C# 语法门禁。
/// <para>
/// 解决的问题：PR 上唯一无条件执行的 CI 作业是静态 shell 守卫，它不编译 C#，
/// 语法错误（少括号、写坏字符串）可以合入。真正的类型检查仍需要 Unity 生成的 csproj
/// 与 Unity 程序集，本工具只覆盖"Parser 能发现的那一类"，成本约 2 秒。
/// </para>
/// <para>
/// 用法：dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear
/// 退出码：0 无语法错误；1 有错误；2 参数/目录错误。
/// </para>
/// </summary>
public static class Program
{
    private static readonly string[] ExcludedSegments =
    {
        "/Library/", "/Temp/", "/obj/", "/bin/", "/.git/",
    };

    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "Unity/Gear/Assets/Gear";
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"[syntax] 目录不存在：{root}");
            return 2;
        }

        var options = new CSharpParseOptions(LanguageVersion.Latest);
        int files = 0;
        int errors = 0;

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (ExcludedSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            files++;
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[syntax] 读取失败 {file}: {ex.Message}");
                errors++;
                continue;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(
                SourceText.From(text), options, path: file);
            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity != DiagnosticSeverity.Error) continue;

                FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                Console.Error.WriteLine(
                    $"{file}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): " +
                    $"error {diagnostic.Id}: {diagnostic.GetMessage()}");
                errors++;
            }
        }

        Console.WriteLine($"[syntax] 扫描 {files} 个 .cs，语法错误 {errors} 个");
        return errors == 0 ? 0 : 1;
    }
}
