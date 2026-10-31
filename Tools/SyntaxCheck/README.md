# SyntaxCheck · 不依赖 Unity 的 C# 语法门禁

PR 上原有的 `guard-only` 只做文件级静态检查，不编译 C#；语法错误（少括号、写坏字符串）
可以合入。本工具用 .NET SDK 自带的 Roslyn 解析全部 `.cs`，把 Parser 能发现的错误挡在合并前。

> 它**只做语法检查**，不做类型检查。类型错误仍需要 Unity 生成的 `.csproj` 与 Unity 程序集，
> 由 `Tools/unity-verify.ps1` 或 CI 的 Unity 作业负责。

## 用法

```bash
dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear
```

退出码：`0` 无语法错误，`1` 有错误，`2` 目录不存在。

## 为什么不用 NuGet

`SyntaxCheck.csproj` 直接引用 `$(MSBuildToolsPath)/Roslyn/bincore` 下 SDK 自带的
`Microsoft.CodeAnalysis*.dll`，因此不需要联网还原包；只要装了 .NET 10 SDK 就能跑。
