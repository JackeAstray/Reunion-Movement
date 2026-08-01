using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ReunionMovement.SourceGenerator
{
    /// <summary>
    /// 源码生成器 —— 扫描所有带 [RegisterCommand] 特性的静态方法，
    /// 生成 TerminalCommandRegistry.RegisterAll，替代 TerminalRequest.RegisterCommands 的
    /// 全程序集反射扫描（GetAssemblies → GetTypes → GetMethods → GetCustomAttribute）。
    /// </summary>
    [Generator]
    public class TerminalCommandRegistryGenerator : ISourceGenerator
    {
        private const string RegisterCommandMetadataName = "ReunionMovement.Core.Terminal.RegisterCommandAttribute";
        private const string CommandArgMetadataName = "ReunionMovement.Core.Terminal.CommandArg";

        private static readonly DiagnosticDescriptor MethodNotAccessible = new DiagnosticDescriptor(
            "RMG001",
            "RegisterCommand 方法需要 public 或 internal",
            "方法 '{0}' 带有 [RegisterCommand] 但不是 public/internal。源码生成器生成的注册代码无法直接调用它，请改为 internal static（推荐）或 public static",
            "ReunionMovement.SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidSignature = new DiagnosticDescriptor(
            "RMG002",
            "RegisterCommand 方法签名不正确",
            "方法 '{0}' 带有 [RegisterCommand]，但签名不是 'static void (CommandArg[] args)'，无法生成直接注册代码",
            "ReunionMovement.SourceGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var attrSymbol = context.Compilation.GetTypeByMetadataName(RegisterCommandMetadataName);
            if (attrSymbol == null) return;

            // 仅当 RegisterCommandAttribute 由当前编译的程序集定义时才生成，
            // 否则会在引用 Assembly-CSharp 的插件程序集编译时重复生成同名类（CS0436/CS0101）
            if (!SymbolEqualityComparer.Default.Equals(attrSymbol.ContainingAssembly, context.Compilation.Assembly))
            {
                return;
            }

            var commandArgSymbol = context.Compilation.GetTypeByMetadataName(CommandArgMetadataName);
            var entries = new List<CommandEntry>();

            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var model = context.Compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot()
                             .DescendantNodes()
                             .OfType<MethodDeclarationSyntax>())
                {
                    if (!(model.GetDeclaredSymbol(node) is IMethodSymbol method)) continue;
                    if (!method.IsStatic) continue;

                    var attr = method.GetAttributes()
                        .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrSymbol));
                    if (attr == null) continue;

                    // 读取特性参数：位置参数（构造参数）与具名参数（Name/Min/Max/Help/Hint）
                    string explicitName = attr.ConstructorArguments.Length > 0
                        ? attr.ConstructorArguments[0].Value as string
                        : null;

                    int minArgs = 0, maxArgs = -1;
                    string help = null, hint = null;
                    foreach (var named in attr.NamedArguments)
                    {
                        switch (named.Key)
                        {
                            case "Name":
                                if (named.Value.Value is string nameValue) explicitName = nameValue;
                                break;
                            case "MinArgCount": minArgs = (int)(named.Value.Value ?? 0); break;
                            case "MaxArgCount": maxArgs = (int)(named.Value.Value ?? -1); break;
                            case "Help": help = named.Value.Value as string; break;
                            case "Hint": hint = named.Value.Value as string; break;
                        }
                    }

                    // 可访问性检查：生成代码必须能直接调用
                    if (method.DeclaredAccessibility != Accessibility.Public &&
                        method.DeclaredAccessibility != Accessibility.Internal)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MethodNotAccessible,
                            method.Locations.FirstOrDefault(),
                            method.ToDisplayString()));
                        continue;
                    }

                    // 签名检查：必须是 static void (CommandArg[])。注意参数是 CommandArg[]（数组），
                    // 需比较数组的元素类型，而非参数类型本身。
                    bool validSignature = method.Parameters.Length == 1 &&
                                          method.Parameters[0].Type is IArrayTypeSymbol arrayType &&
                                          SymbolEqualityComparer.Default.Equals(arrayType.ElementType, commandArgSymbol);
                    if (!validSignature)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidSignature,
                            method.Locations.FirstOrDefault(),
                            method.ToDisplayString()));
                        continue;
                    }

                    // 命令名推断（与 TerminalRequest.RegisterCommands 逻辑一致）
                    string commandName = explicitName;
                    if (commandName == null)
                    {
                        commandName = InferCommandName(InferFrontCommandName(method.Name) ?? method.Name);
                    }

                    entries.Add(new CommandEntry
                    {
                        CommandName = commandName,
                        ContainingType = method.ContainingType.ToDisplayString(),
                        MethodName = method.Name,
                        MinArgs = minArgs,
                        MaxArgs = maxArgs,
                        Help = help,
                        Hint = hint
                    });
                }
            }

            // 注意：即使 entries 为空也必须生成类（空 RegisterAll），
            // 否则条件编译（#if UNITY_EDITOR 等）裁剪掉全部命令的 Player 构建中，
            // TerminalCommandRegistry 类不存在 → TerminalRequest.RegisterCommands 编译失败。
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated> by ReunionMovement.SourceGenerator / TerminalCommandRegistryGenerator</auto-generated>");
            sb.AppendLine("#pragma warning disable");
            sb.AppendLine("using System;");
            sb.AppendLine("namespace ReunionMovement.Core.Terminal");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>编译期生成的终端命令注册表（替代运行时全程序集反射扫描）</summary>");
            sb.AppendLine("    public static class TerminalCommandRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>注册全部 [RegisterCommand] 命令（由 TerminalRequest.RegisterCommands 调用）</summary>");
            sb.AppendLine("        public static void RegisterAll(TerminalRequest request)");
            sb.AppendLine("        {");
            foreach (var e in entries)
            {
                sb.Append("            request.AddCommand(");
                sb.Append(Lit(e.CommandName));
                sb.Append(", args => ");
                sb.Append(e.ContainingType);
                sb.Append('.');
                sb.Append(e.MethodName);
                sb.Append("(args), ");
                sb.Append(e.MinArgs);
                sb.Append(", ");
                sb.Append(e.MaxArgs);
                sb.Append(", ");
                sb.Append(Lit(e.Help));
                sb.Append(", ");
                sb.Append(Lit(e.Hint));
                sb.AppendLine(");");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("ReunionMovement.TerminalCommandRegistry.g.cs",
                SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        /// <summary>将字符串生成为 C# 字符串字面量（null 生成 null 字面量，SymbolDisplay.FormatLiteral 不接受 null）</summary>
        private static string Lit(string s)
        {
            return s == null ? "null" : SymbolDisplay.FormatLiteral(s, true);
        }

        /// <summary>删除方法名中的 FRONT 前缀（与 TerminalRequest.InferFrontCommandName 一致）</summary>
        private static string InferFrontCommandName(string methodName)
        {
            int index = methodName.IndexOf("FRONT", System.StringComparison.CurrentCultureIgnoreCase);
            return index >= 0 ? methodName.Remove(index, 5) : null;
        }

        /// <summary>删除方法名中的 COMMAND 标记（与 TerminalRequest.InferCommandName 一致）</summary>
        private static string InferCommandName(string methodName)
        {
            int index = methodName.IndexOf("COMMAND", System.StringComparison.CurrentCultureIgnoreCase);
            return index >= 0 ? methodName.Remove(index, 7) : methodName;
        }

        private struct CommandEntry
        {
            public string CommandName;
            public string ContainingType;
            public string MethodName;
            public int MinArgs;
            public int MaxArgs;
            public string Help;
            public string Hint;
        }
    }
}
