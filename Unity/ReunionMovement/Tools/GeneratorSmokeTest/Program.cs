using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ReunionMovement.SourceGenerator;

class Program
{
    static void Main()
    {
        // 模拟 Assembly-CSharp 中与生成器相关的关键代码
        string source = @"
using System;
namespace ReunionMovement.Core.UI
{
    public class UIController : UnityEngine.MonoBehaviour { }
    public class StartGameUIPlane : UIController { }
    public class PopupUIPlane : UIController { }
    public abstract class AbstractUI : UIController { }
    public class NotUI { }
}
namespace ReunionMovement.Core.Terminal
{
    public struct CommandArg { public string String; }
    public class RegisterCommandAttribute : Attribute
    {
        int min = 0, max = -1;
        public int MinArgCount { get => min; set => min = value; }
        public int MaxArgCount { get => max; set => max = value; }
        public string Name { get; set; }
        public string Help { get; set; }
        public string Hint { get; set; }
        public RegisterCommandAttribute(string command_name = null) { Name = command_name; }
    }
    public class TerminalRequest
    {
        public void AddCommand(string name, Action<CommandArg[]> proc, int min_args = 0, int max_args = -1, string help = """", string hint = null) { }
    }
    public class TerminalSystem
    {
        [RegisterCommand(Help = ""OpenWindow 1-16 String"", MinArgCount = 1, MaxArgCount = 16)]
        internal static void OpenWindow(CommandArg[] args) { }
        [RegisterCommand(Name = ""CUSTOM"", Help = ""Test"", MaxArgCount = 2)]
        internal static void TestTerminal(CommandArg[] args) { }
    }
}
";

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(UnityEngine.MonoBehaviour).Assembly.Location),
        };
        // net8.0 下补充 System.Runtime facade（避免 CS0012）
        var runtimeDll = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll");
        if (System.IO.File.Exists(runtimeDll))
        {
            references.Add(MetadataReference.CreateFromFile(runtimeDll));
        }

        var compilation = CSharpCompilation.Create(
            "Assembly-CSharp",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generators = new ISourceGenerator[]
        {
            new UIControllerRegistryGenerator(),
            new TerminalCommandRegistryGenerator(),
        };

        var driver = CSharpGeneratorDriver.Create(generators);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Console.WriteLine("=== 生成器诊断 ===");
        foreach (var d in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
        {
            Console.WriteLine($"[{d.Severity}] {d.Id}: {d.GetMessage()}");
        }

        Console.WriteLine();
        foreach (var tree in outputCompilation.SyntaxTrees.Skip(1)) // 跳过原始源码树
        {
            Console.WriteLine($"--- 生成文件: {tree.FilePath ?? "(memory)"} ---");
            Console.WriteLine(tree.ToString());
            Console.WriteLine();
        }

        // 验证生成的代码能编译
        var emitDiagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Console.WriteLine($"编译错误数: {emitDiagnostics.Count()}");
        foreach (var d in emitDiagnostics)
        {
            Console.WriteLine($"[{d.Id}] {d.GetMessage()}");
        }

        Console.WriteLine(emitDiagnostics.Any() ? "!!! 生成代码编译失败 !!!" : "OK: 生成代码编译通过");
    }
}

// UnityEngine.MonoBehaviour stub（仅用于冒烟测试）
namespace UnityEngine
{
    public class MonoBehaviour { }
}
