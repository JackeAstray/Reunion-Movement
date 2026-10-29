<#
.SYNOPSIS
    一条命令跑完 Unity 侧的全部验证：EditMode 测试 + 架构守卫。

.DESCRIPTION
    dotnet build 只能证明"能编译"，行为正确性得靠 Unity 跑测试。
    这个脚本把"打开 Unity → 开 Test Runner → 选 EditMode → Run All → 再手动点菜单跑守卫"
    压缩成一条命令，并把结果汇总成退出码，方便本地自查或接进 CI。

    注意：Unity 同一工程只允许一个实例。编辑器开着时本脚本会直接报错退出，
    因为批量模式会另有日志，不会干扰你正在改的场景。

.PARAMETER UnityPath
    Unity.exe 的完整路径。不传则自动探测（环境变量 UNITY_EDITOR，或按 ProjectVersion.txt 找常见安装位置）。

.PARAMETER SkipTests
    跳过 EditMode 测试，只跑架构守卫。

.PARAMETER SkipGuard
    跳过架构守卫，只跑 EditMode 测试。

.EXAMPLE
    pwsh Tools/unity-verify.ps1

.EXAMPLE
    pwsh Tools/unity-verify.ps1 -UnityPath "D:\Unity\6000.3.10f1\Editor\Unity.exe"
#>
[CmdletBinding()]
param(
    [string]$UnityPath,
    [switch]$SkipTests,
    [switch]$SkipGuard
)

$ErrorActionPreference = 'Stop'

# ---- 路径 ----
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'Unity\ReunionMovement'
$resultDir = Join-Path $projectPath 'Logs\verify'
$testResults = Join-Path $resultDir 'editmode-results.xml'
$testLog = Join-Path $resultDir 'editmode.log'
$guardLog = Join-Path $resultDir 'guard.log'

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "    $text" -ForegroundColor Green }
function Write-Bad($text) { Write-Host "    $text" -ForegroundColor Red }

# Unity.exe 是 GUI 子系统程序：PowerShell 的 `&` 不会等待它，也不会设置 $LASTEXITCODE。
# 必须经 cmd /c 调用（cmd 会等进程结束），否则脚本在 Unity 还没跑完时就进入下一步，
# 两个实例抢同一个工程锁、其中一个直接退出 —— 表现为"退出码为空 + 没有生成测试结果"。
function Invoke-UnityBatch {
    param([string[]]$UnityArgs)
    $joined = ($UnityArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    & cmd /c "`"$UnityPath`" $joined"
    return $LASTEXITCODE
}

if (-not (Test-Path $projectPath)) {
    Write-Bad "找不到 Unity 工程目录: $projectPath"
    exit 2
}


# ---- 找 Unity.exe ----
if (-not $UnityPath) {
    if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) {
        $UnityPath = $env:UNITY_EDITOR
    }
    else {
        $versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
        $version = if (Test-Path $versionFile) {
            (Select-String -LiteralPath $versionFile -Pattern 'm_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
        } else { $null }

        $candidates = @()
        if ($version) {
            $candidates += "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
            $candidates += "D:\Unity\$version\Editor\Unity.exe"
            $candidates += "C:\Program Files\Unity\Editor\Unity.exe"
        }
        $candidates += (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' })

        foreach ($c in $candidates) {
            if ($c -and (Test-Path $c)) { $UnityPath = $c; break }
        }
    }
}

if (-not $UnityPath -or -not (Test-Path $UnityPath)) {
    Write-Bad '没找到 Unity.exe。请用 -UnityPath 指定，或设置环境变量 UNITY_EDITOR。'
    exit 2
}

# ---- 编辑器占用检查 ----
$lockFile = Join-Path $projectPath 'Temp\UnityLockfile'
if (Test-Path $lockFile) {
    Write-Bad '检测到 Temp\UnityLockfile：Unity 编辑器正开着这个工程。'
    Write-Host '    批量模式必须独占工程，请先关闭编辑器再运行。' -ForegroundColor Yellow
    exit 3
}
New-Item -ItemType Directory -Force -Path $resultDir | Out-Null
Write-Step "Unity: $UnityPath"
Write-Host "    版本: 见 ProjectSettings\ProjectVersion.txt"

$exitCode = 0

# ---- 1. EditMode 测试 ----
if (-not $SkipTests) {
    Write-Step '运行 EditMode 测试…'
    if (Test-Path $testResults) { Remove-Item $testResults -Force }

    $unityExit = Invoke-UnityBatch @(
        '-batchmode', '-nographics', '-projectPath', $projectPath,
        '-runTests', '-testPlatform', 'EditMode',
        '-testResults', $testResults, '-logFile', $testLog
    )

    if (-not (Test-Path $testResults)) {
        Write-Bad "没有生成测试结果，Unity 退出码 $unityExit。日志: $testLog"
        $exitCode = 1
    }
    else {
        [xml]$xml = Get-Content -LiteralPath $testResults -Raw -Encoding UTF8
        $run = $xml.'test-run'
        $total = [int]$run.total
        $passed = [int]$run.passed
        $failed = [int]$run.failed
        $skipped = [int]$run.skipped

        if ($failed -gt 0 -or $total -eq 0) {
            Write-Bad "测试失败：共 $total，通过 $passed，失败 $failed，跳过 $skipped"
            foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
                Write-Bad "  × $($case.fullname)"
                if ($case.failure.message.'#cdata-section') {
                    Write-Host "      $($case.failure.message.'#cdata-section'.Trim())" -ForegroundColor DarkGray
                }
            }
            $exitCode = 1
        }
        else {
            Write-Ok "全部通过：$total 个用例（跳过 $skipped）"
        }
    }
}

# ---- 2. 架构守卫 ----
if (-not $SkipGuard) {
    Write-Step '运行架构守卫…'
    $guardExit = Invoke-UnityBatch @(
        '-batchmode', '-nographics', '-quit', '-projectPath', $projectPath,
        '-executeMethod', 'ReunionMovement.Editor.Guards.ArchitectureGuard.RunAllBatch',
        '-logFile', $guardLog
    )

    if ($guardExit -ne 0) {
        Write-Bad "守卫未通过（退出码 $guardExit）。日志: $guardLog"
        if (Test-Path $guardLog) {
            Select-String -LiteralPath $guardLog -Pattern '架构守卫|超过|缺少|违规' |
                Select-Object -Last 12 |
                ForEach-Object { Write-Host "      $($_.Line.Trim())" -ForegroundColor DarkGray }
        }
        $exitCode = 1
    }
    else {
        Write-Ok '守卫通过'
    }
}

# ---- 汇总 ----
Write-Host ''
if ($exitCode -eq 0) {
    Write-Host '=== 全部通过 ===' -ForegroundColor Green
} else {
    Write-Host '=== 有失败项 ===' -ForegroundColor Red
}
Write-Host "日志目录: $resultDir"
exit $exitCode
