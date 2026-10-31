<#
.SYNOPSIS
    UPM 包安装烟测：新建空 Unity 工程 → 用 file: 安装导出的包 → 编译，确认不是"只在仓库里能编译"。

.DESCRIPTION
    发布前最容易漏的一步是"包在空工程里装不上/编译不过"。本脚本把这一步自动化：
      1. 校验 package.json 的 name / version / 必要文件；
      2. 用 Unity -createProject 建一个空工程；
      3. 在 Packages/manifest.json 里加 file: 依赖；
      4. 以 batchmode 打开工程，检查退出码与日志里的编译错误。

    需要本机已激活的 Unity（与 Tools/unity-verify.ps1 相同），不能在没有授权的 CI 上跑。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/upm-smoke-test.ps1 `
        -UnityPath "E:\UnityEditor\6000.3.10f1\Editor\Unity.exe"
#>
[CmdletBinding()]
param(
    [string]$PackagePath = "",
    [string]$UnityPath = "",
    [string]$WorkDir = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $repoRoot 'Unity/Gear/Build/Packages/com.jackeastray.gear'
}
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) { $UnityPath = $env:UNITY_EDITOR }
    else { throw "请用 -UnityPath 指定 Unity.exe，或设置 UNITY_EDITOR。" }
}
if (-not (Test-Path $UnityPath)) { throw "找不到 Unity：$UnityPath" }
if (-not (Test-Path $PackagePath)) { throw "找不到包目录：$PackagePath（先导出 UPM 包）" }

# Unity.exe 是 GUI 子系统程序：PowerShell 的 & 不会等待，也不会设置 $LASTEXITCODE。
# 与 Tools/unity-verify.ps1 一致，经 cmd /c 调用，确保等到进程结束并拿到退出码。
function Invoke-Unity {
    param([string[]]$UnityArgs)
    $joined = ($UnityArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    & cmd /c "`"$UnityPath`" $joined"
    return $LASTEXITCODE
}

$packageJsonPath = Join-Path $PackagePath 'package.json'
if (-not (Test-Path $packageJsonPath)) { throw "包目录缺少 package.json：$packageJsonPath" }
$pkg = Get-Content -LiteralPath $packageJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($pkg.name -ne 'com.jackeastray.gear') { throw "包名不正确：$($pkg.name)" }
if (-not $pkg.version) { throw "package.json 缺少 version" }
foreach ($required in @('README.md', 'CHANGELOG.md', 'LICENSE.md', 'Third Party Notices.md')) {
    if (-not (Test-Path (Join-Path $PackagePath $required))) {
        Write-Warning "包内缺少 $required（不阻断编译，但发布前应补齐）"
    }
}
Write-Host "包标识 : $($pkg.name)@$($pkg.version)"
Write-Host "包目录 : $PackagePath"

if ([string]::IsNullOrWhiteSpace($WorkDir)) {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ('gear-upm-smoke-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
if (Test-Path $WorkDir) { Remove-Item -LiteralPath $WorkDir -Recurse -Force }
New-Item -ItemType Directory -Path $WorkDir | Out-Null

$createLog = Join-Path $WorkDir 'create.log'
$compileLog = Join-Path $WorkDir 'compile.log'
$packageFull = (Resolve-Path $PackagePath).Path.Replace('\', '/')

try {
    Write-Host "==> 创建空工程：$WorkDir"
    $createExit = Invoke-Unity @('-batchmode', '-quit', '-nographics', '-createProject', $WorkDir, '-logFile', $createLog)
    if ($createExit -ne 0) { throw "创建空工程失败（退出码 $createExit），日志: $createLog" }

    $manifestPath = Join-Path $WorkDir 'Packages/manifest.json'
    if (-not (Test-Path $manifestPath)) { throw "空工程没有 Packages/manifest.json：$manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $manifest.dependencies) {
        $manifest | Add-Member -NotePropertyName dependencies -NotePropertyValue (New-Object PSObject) -Force
    }
    $manifest.dependencies | Add-Member -NotePropertyName 'com.jackeastray.gear' `
        -NotePropertyValue "file:$packageFull" -Force
    $manifest | ConvertTo-Json -Depth 20 |
        ForEach-Object { [System.IO.File]::WriteAllText($manifestPath, $_, (New-Object System.Text.UTF8Encoding($false))) }
    Write-Host "==> manifest 已写入 file:$packageFull"

    Write-Host "==> 打开工程并编译（首次导入可能较慢）"
    $compileExit = Invoke-Unity @('-batchmode', '-quit', '-nographics', '-projectPath', $WorkDir, '-logFile', $compileLog)
    if ($compileExit -ne 0) { throw "工程编译失败（退出码 $compileExit），日志: $compileLog" }

    $log = Get-Content -LiteralPath $compileLog -Raw -Encoding UTF8
    if ($log -match 'error CS\d+' -or $log -match 'Scripts have compiler errors') {
        $matches = [regex]::Matches($log, 'error CS\d+[^\r\n]*')
        $matches | Select-Object -First 10 | ForEach-Object { Write-Host "  $($_.Value)" -ForegroundColor Red }
        throw "包安装后出现编译错误，日志: $compileLog"
    }

    Write-Host "=== UPM 烟测通过：空工程安装 + 编译 0 错误 ===" -ForegroundColor Green
}
finally {
    Write-Host "工程目录: $WorkDir（确认后可手动删除）"
}
