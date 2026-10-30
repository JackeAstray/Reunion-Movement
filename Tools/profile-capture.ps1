<#
.SYNOPSIS
    Capture engine Profiler markers into JSON (no editor GUI required).

.DESCRIPTION
    Instead of "open Unity -> enter Play mode -> open Profiler -> screenshot -> guess",
    this script records the markers emitted by EngineProfiler (per module + engine loops)
    through ProfilerRecorder, runs a fixed number of frames, writes
    Logs/profiler/module-costs.json and prints a top-N table.

    Unlike unity-verify.ps1 this does NOT pass -nographics: rendering, canvas rebuilds
    and particles are part of the per-frame cost, so stripping graphics makes the
    numbers meaningless.

    The script is intentionally ASCII-only: it is stored without a BOM and some shells
    decode it as ANSI, which would corrupt non-ASCII string literals at parse time.

.PARAMETER UnityPath
    Full path to Unity.exe. Falls back to the UNITY_EDITOR environment variable.

.PARAMETER Filter
    PlayMode test filter, default "ProfilerCapture".

.PARAMETER NoGraphics
    Run headless (loses rendering cost, keeps logic + engine driving).

.PARAMETER Scene
    Optional scene name from Build Settings (e.g. "UI Example") to load additively
    before sampling, so modules actually do work instead of idling.

.EXAMPLE
    pwsh Tools/profile-capture.ps1 -UnityPath "E:\UnityEditor\6000.3.10f1\Editor\Unity.exe"
#>
[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$Filter = 'ProfilerCapture',
    [string]$Scene,
    [switch]$NoGraphics
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'Unity\Gear'
$resultDir = Join-Path $projectPath 'Logs\verify'
$testResults = Join-Path $resultDir 'playmode-profiler-results.xml'
$testLog = Join-Path $resultDir 'playmode-profiler.log'
$jsonPath = Join-Path $projectPath 'Logs\profiler\module-costs.json'

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "    $text" -ForegroundColor Green }
function Write-Bad($text) { Write-Host "    $text" -ForegroundColor Red }

function Invoke-UnityBatch {
    param([string[]]$UnityArgs)
    $joined = ($UnityArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    & cmd /c "`"$UnityPath`" $joined"
    return $LASTEXITCODE
}

if (-not $UnityPath) { $UnityPath = $env:UNITY_EDITOR }
if (-not $UnityPath -or -not (Test-Path $UnityPath)) {
    Write-Bad 'Unity.exe not found. Pass -UnityPath or set UNITY_EDITOR.'
    exit 2
}

$lockFile = Join-Path $projectPath 'Temp\UnityLockfile'
if (Test-Path $lockFile) {
    Write-Bad 'Temp\UnityLockfile exists: the editor has this project open.'
    Write-Host '    PlayMode batch tests need exclusive access, close the editor first.' -ForegroundColor Yellow
    exit 3
}

New-Item -ItemType Directory -Force -Path $resultDir | Out-Null
if (Test-Path $testResults) { Remove-Item $testResults -Force }
if (Test-Path $jsonPath) { Remove-Item $jsonPath -Force }

Write-Step "Unity: $UnityPath"
Write-Step "Capturing PlayMode (filter=$Filter, scene=$($(if ($Scene) { $Scene } else { '(engine only)' })))..."

# The scene request goes through a file, not an env var: on the
# PowerShell -> cmd -> Unity chain the environment is not reliably inherited
# (passing $env:GEAR_PROFILE_SCENE kept producing "(engine only)" in the JSON).
$profilerDir = Join-Path $projectPath 'Logs\profiler'
New-Item -ItemType Directory -Force -Path $profilerDir | Out-Null
$requestFile = Join-Path $profilerDir 'capture-request.txt'
Set-Content -LiteralPath $requestFile -Value ($(if ($Scene) { $Scene } else { '' })) -Encoding ASCII -NoNewline

# Do NOT pass -quit: it makes Unity exit before the tests actually start
# (exit code 0, no results xml). Unity exits on its own once tests finish.
$unityArgs = @('-batchmode', '-projectPath', $projectPath,
    '-runTests', '-testPlatform', 'PlayMode',
    '-testFilter', $Filter,
    '-testResults', $testResults, '-logFile', $testLog)
if ($NoGraphics) { $unityArgs += '-nographics' }

$unityExit = Invoke-UnityBatch $unityArgs

if (-not (Test-Path $jsonPath)) {
    Write-Bad "No capture produced (Unity exit $unityExit). Log: $testLog"
    if (Test-Path $testLog) {
        Select-String -LiteralPath $testLog -Pattern 'ProfilerCapture|Exception|error CS|error:' |
            Select-Object -Last 20 |
            ForEach-Object { Write-Host "      $($_.Line.Trim())" -ForegroundColor DarkGray }
    }
    exit 1
}

$data = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

Write-Step ("frame avg={0}ms  max={1}ms  recorders={2}/{3}" -f $data.frameAvgMs, $data.frameMaxMs, $data.recordersStarted, $data.samplers.Count)
Write-Host ''
Write-Host ('{0,-46} {1,10} {2,10}  {3}' -f 'marker', 'avg(ms)', 'max(ms)', 'category') -ForegroundColor Cyan
foreach ($s in $data.samplers) {
    if ($s.samples -eq 0) { continue }
    Write-Host ('{0,-46} {1,10:F3} {2,10:F3}  {3}' -f $s.name, $s.avgMs, $s.maxMs, $s.category)
}

if ($data.counters) {
    Write-Host ''
    Write-Host ('{0,-30} {1,14} {2,14}  {3}' -f 'counter', 'avg', 'max', 'unit') -ForegroundColor Cyan
    foreach ($c in $data.counters) {
        if ($c.samples -eq 0) { continue }
        Write-Host ('{0,-30} {1,14:F2} {2,14}  {3}' -f $c.name, $c.avg, $c.max, $c.unit)
    }
}

Write-Host ''
Write-Ok "Raw data: $jsonPath"Write-Host ''
Write-Host 'NOTE: in batchmode some modules do not tick (no scene / no canvas), so this is the'
Write-Host '      "engine idle" baseline, not the absolute cost of a real match.'
exit 0
