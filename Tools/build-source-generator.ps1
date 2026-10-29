<#
.SYNOPSIS
    重建 Assets 内的源码生成器 DLL。

.DESCRIPTION
    `Assets/Gear/Plugins/Generators/Gear.SourceGenerator.dll` 是提交在仓库里的构建产物，
    源码在 `Unity/Gear/Tools/Gear.SourceGenerator/`。

    生成器用 **元数据名字符串**匹配 `[RegisterCommand]` 与 `UIController`
    （见 TerminalCommandRegistryGenerator / UIControllerRegistryGenerator 里的 MetadataName 常量），
    所以改了这两个类型所在的命名空间后，**必须重建 DLL**，否则生成物缺失、编译报错。

    Roslyn 包版本要与 Unity 自带的编译器对齐（6000.3 为 4.3.0）。版本高于 Unity 的 Roslyn，
    分析器会因程序集绑定失败而被跳过。

.EXAMPLE
    pwsh Tools/build-source-generator.ps1
#>
[CmdletBinding()]
param(
    [string]$RoslynVersion = '4.3.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$generatorDir = Join-Path $repoRoot 'Unity\Gear\Tools\Gear.SourceGenerator'
$target = Join-Path $repoRoot 'Unity\Gear\Assets\Gear\Plugins\Generators\Gear.SourceGenerator.dll'

if (-not (Test-Path $generatorDir)) { throw "找不到生成器源码目录: $generatorDir" }
if (-not (Test-Path $target)) { throw "找不到目标 DLL: $target" }

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("gear-gen-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null

$csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>Gear.SourceGenerator</AssemblyName>
    <NoWarn>RS1035;CS8032;RS2008</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$generatorDir\TerminalCommandRegistryGenerator.cs" />
    <Compile Include="$generatorDir\UIControllerRegistryGenerator.cs" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="$RoslynVersion" PrivateAssets="all" />
  </ItemGroup>
</Project>
"@

Set-Content -LiteralPath (Join-Path $work 'Gen.csproj') -Value $csproj -Encoding UTF8

Write-Host "==> 编译生成器（Roslyn $RoslynVersion）…" -ForegroundColor Cyan
& dotnet build (Join-Path $work 'Gen.csproj') -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "生成器编译失败（退出码 $LASTEXITCODE）" }

$built = Join-Path $work 'bin\Release\netstandard2.0\Gear.SourceGenerator.dll'
if (-not (Test-Path $built)) { throw "没有产出 DLL: $built" }

Copy-Item -LiteralPath $built -Destination $target -Force
$size = (Get-Item -LiteralPath $target).Length
Write-Host "   已更新: $target ($size bytes)" -ForegroundColor Green
Write-Host "   提示: 回到 Unity 会重新编译；跑 pwsh Tools/unity-verify.ps1 验证 125 条用例与守卫。" -ForegroundColor DarkGray

Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
