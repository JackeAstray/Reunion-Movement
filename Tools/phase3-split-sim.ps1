# Phase-3 split simulation: prove that ReunionMovement.Utils can be split into
# "Framework.Modules" + "Utilities" WITHOUT touching a single .cs file.
#
# It reads the Unity-generated ReunionMovement.Utils.csproj (which lists every
# compiled file and every reference), partitions the compile set by folder, and
# emits two independent csproj files that are built with dotnet.
#
# If both build with 0 errors, the folder boundary is a valid assembly boundary.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools\phase3-split-sim.ps1
#
# Requires: dotnet build of the Unity project already produced the csproj.

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'Unity\ReunionMovement'
$src = Join-Path $proj 'ReunionMovement.Utils.csproj'
$outDir = Join-Path $proj 'Temp\phase3sim'

if (-not (Test-Path $src)) {
    Write-Host "[FAIL] missing $src - run 'dotnet build ReunionMovement.Utils.csproj' first" -ForegroundColor Red
    exit 2
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Folders that become "Utilities". Everything else under Utils\ becomes "Framework.Modules".
$utilPrefixes = @(
    'Assets\ReunionMovement\Utils\Util\',
    'Assets\ReunionMovement\Utils\UI\'
)

$lines = [IO.File]::ReadAllLines($src, [Text.Encoding]::UTF8)
$compileUtil = New-Object System.Collections.ArrayList
$compileModule = New-Object System.Collections.ArrayList
$rest = New-Object System.Collections.ArrayList

foreach ($line in $lines) {
    if ($line -match '<Compile Include="([^"]+)"') {
        $path = $Matches[1]
        $isUtil = $false
        foreach ($p in $utilPrefixes) {
            if ($path.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)) { $isUtil = $true; break }
        }
        if ($isUtil) { [void]$compileUtil.Add($path) } else { [void]$compileModule.Add($path) }
    }
    else {
        [void]$rest.Add($line)
    }
}

Write-Host ("compile items: utilities=" + $compileUtil.Count + " modules=" + $compileModule.Count)

# The generated csproj lives two levels deeper, so every relative path must go up twice.
$rebasedCompile = @()
foreach ($c in $compileUtil) { $rebasedCompile += ('..\..\' + $c) }
$rebasedCompileModule = @()
foreach ($c in $compileModule) { $rebasedCompileModule += ('..\..\' + $c) }

function Rebase-Line([string]$text) {
    $text = $text -replace '<ProjectReference Include="ReunionMovement\.', '<ProjectReference Include="..\..\ReunionMovement.'
    $text = $text -replace '<ProjectReference Include="UniTask', '<ProjectReference Include="..\..\UniTask'
    # HintPath may be relative (Library\ScriptAssemblies\... or Assets\...)
    $text = [regex]::Replace($text, '<HintPath>([^<]+)</HintPath>', {
        param($m)
        $v = $m.Groups[1].Value
        if ($v -match '^[A-Za-z]:' -or $v.StartsWith('\')) { return $m.Value }
        return '<HintPath>..\..\' + $v + '</HintPath>'
    })
    return $text
}

function Emit-Project([string]$assemblyName, $compileItems, [string]$extraRef) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($line in $rest) {
        if ($line -match '<AssemblyName>') {
            [void]$sb.AppendLine('    <AssemblyName>' + $assemblyName + '</AssemblyName>')
        }
        else {
            [void]$sb.AppendLine((Rebase-Line $line))
        }
    }

    $xml = New-Object System.Text.StringBuilder
    [void]$xml.AppendLine('  <ItemGroup>')
    foreach ($c in $compileItems) { [void]$xml.AppendLine('    <Compile Include="' + $c + '" />') }
    [void]$xml.AppendLine('  </ItemGroup>')
    if ($extraRef) {
        [void]$xml.AppendLine('  <ItemGroup>')
        [void]$xml.AppendLine('    <ProjectReference Include="' + $extraRef + '" />')
        [void]$xml.AppendLine('  </ItemGroup>')
    }

    $final = $sb.ToString().Replace('</Project>', $xml.ToString() + '</Project>')
    $target = Join-Path $outDir ($assemblyName + '.csproj')
    [IO.File]::WriteAllText($target, $final, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("wrote " + $target)
}

Emit-Project 'ReunionMovement.Utilities' $rebasedCompile $null
Emit-Project 'ReunionMovement.Framework.Modules' $rebasedCompileModule 'ReunionMovement.Utilities.csproj'

Write-Host ''
Write-Host '=== build 1/2: Utilities (standalone, must have no outward dependency) ==='
Push-Location $outDir
try {
    dotnet build 'ReunionMovement.Utilities.csproj' -v q --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host '[FAIL] Utilities does not compile on its own' -ForegroundColor Red; exit 1 }

    Write-Host ''
    Write-Host '=== build 2/2: Framework.Modules (references Utilities) ==='
    dotnet build 'ReunionMovement.Framework.Modules.csproj' -v q --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host '[FAIL] Modules does not compile against Utilities' -ForegroundColor Red; exit 1 }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host '=== PASS: the folder boundary is a valid assembly boundary ===' -ForegroundColor Green
Write-Host ('    Utilities  = ' + $compileUtil.Count + ' files, no outward dependency')
Write-Host ('    Modules    = ' + $compileModule.Count + ' files, depends on Utilities (one way)')
exit 0
