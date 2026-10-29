<#
.SYNOPSIS
    把导出的 UPM 包发布到一个只含包内容的 orphan 分支（默认 upm），供 Unity 用一条 git URL 安装。

.DESCRIPTION
    为什么要这个脚本：
      `Unity/Astray/Build/` 被 `.gitignore` 忽略（它是 Unity 的构建目录），
      所以"能看能改"的包目录永远不在 git 里，git URL 安装也就无从谈起。
      本脚本把导出结果放进一个独立分支，历史只有一个提交，包内容位于分支根目录：

          https://github.com/<owner>/Astray.git#upm

    用法（在仓库根目录执行）：

        # 1. 只重建本地 upm 分支，不推远端（安全，git branch -D upm 可撤销）
        powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1

        # 2. 先让 Unity 重新导出包，再重建分支
        powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -Export

        # 3. 推到远端（Unity 里就能用 git URL 装了）
        powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -Push

        # 4. 只校验包是否过期，不做任何 git 操作
        powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -CheckOnly

.PARAMETER Export
    先调用 Unity 批处理重新导出包。需要 -UnityPath，或 Unity 在 PATH 里。

.PARAMETER Push
    推到远端（强制覆盖 upm 分支）。不加时只更新本地分支。

.PARAMETER AllowStale
    允许发布"比源码旧"的包（只应在确认包内容确实没变时使用）。

.NOTES
    upm 分支每次发布都重建为单个提交（force push）。不要在该分支上手工提交，
    也不要用它做开发分支 —— 它只是发布产物。
#>
[CmdletBinding()]
param(
    [string]$PackagePath = "",
    [string]$Branch = "upm",
    [string]$Remote = "origin",
    [string]$CommitMessage = "",
    [string]$UnityPath = "",
    [switch]$Export,
    [switch]$Push,
    [switch]$CheckOnly,
    [switch]$AllowStale
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ------------------------------------------------------------------ 基础工具

function Invoke-Git {
    param([string[]]$GitArgs)

    # 原生命令的 stderr 在 $ErrorActionPreference='Stop' 下会被当成终止错误，
    # 这里局部放宽，只按退出码判断成败。
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & git @GitArgs 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($code -ne 0) {
        throw ("git {0} 失败（退出码 {1}）：{2}{3}" -f ($GitArgs -join ' '), $code, [Environment]::NewLine, ($output -join [Environment]::NewLine))
    }
    return $output
}

# PowerShell 5.1 读 UTF-8 无 BOM 的中文会变乱码；所有文本读取都显式指定 UTF-8。
function Read-Utf8 {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

$repoRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw "当前目录不是 git 仓库；请在 Astray 仓库内执行。"
}
$repoRoot = ([string]$repoRoot).Trim().Replace('\', '/')

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $repoRoot 'Unity/Astray/Build/Packages/com.jackeastray.astray'
}
if (-not (Test-Path $PackagePath)) {
    throw "找不到要发布的包目录：$PackagePath`n先执行 Unity → Astray → 打包 → 导出 UPM 包（运行时），或加 -Export。"
}

$packageJsonPath = Join-Path $PackagePath 'package.json'
if (-not (Test-Path $packageJsonPath)) {
    throw "包目录缺少 package.json：$packageJsonPath"
}
$packageJson = Read-Utf8 $packageJsonPath | ConvertFrom-Json
$packageName = $packageJson.name
$packageVersion = $packageJson.version

Write-Host "包目录 : $PackagePath"
Write-Host "包标识 : $packageName@$packageVersion"

# ------------------------------------------------------------------ 可选：重新导出

if ($Export) {
    $unity = $UnityPath
    if ([string]::IsNullOrWhiteSpace($unity)) {
        $candidate = Get-ChildItem "$env:ProgramFiles\Unity\Hub\Editor" -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($candidate) { $unity = $candidate }
    }
    if ([string]::IsNullOrWhiteSpace($unity)) {
        $command = Get-Command Unity -ErrorAction SilentlyContinue
        if ($command) { $unity = $command.Source }
    }
    if ([string]::IsNullOrWhiteSpace($unity) -or -not (Test-Path $unity)) {
        throw "找不到 Unity 可执行文件。用 -UnityPath 'C:\...\Unity.exe' 指定，或先在编辑器里手动导出包。"
    }

    $projectPath = Join-Path $repoRoot 'Unity/Astray'
    Write-Host "重新导出 : $unity -batchmode -executeMethod AstrayUpmExporter.ExportRuntimeBatch"
    & $unity -batchmode -quit -nographics -projectPath $projectPath `
        -executeMethod Astray.EditorTools.Package.AstrayUpmExporter.ExportRuntimeBatch `
        -logFile -
    if ($LASTEXITCODE -ne 0) { throw "Unity 导出失败（退出码 $LASTEXITCODE）。" }
}

# ------------------------------------------------------------------ 过期检查

# 包是否"新鲜"用最可靠的信号：源码比 package.json 新，就认为包可能过期。
$sourceRoot = Join-Path $repoRoot 'Unity/Astray/Assets/Astray'
$packageStamp = (Get-Item $packageJsonPath).LastWriteTimeUtc
$newer = @()
if (Test-Path $sourceRoot) {
    $newer = @(Get-ChildItem $sourceRoot -Recurse -File -Include *.cs, *.asmdef, *.asset |
        Where-Object { $_.FullName -notmatch '\\(Library|Temp|obj|bin)\\' -and $_.LastWriteTimeUtc -gt $packageStamp } |
        Select-Object -First 5)
}
if ($newer.Count -gt 0 -and -not $AllowStale) {
    $nl = [Environment]::NewLine
    $names = ($newer | ForEach-Object { $_.FullName.Replace($repoRoot + '/', '').Replace('\', '/') }) -join ($nl + '  ')
    throw "包可能已过期：以下源文件比 package.json 新。$nl  $names$nl先重新导出（加 -Export），或确认无实质变化后用 -AllowStale。"
}

# ------------------------------------------------------------------ 汇总

$files = Get-ChildItem $PackagePath -Recurse -File
$fileCount = ($files | Measure-Object).Count
$sizeMb = [math]::Round((($files | Measure-Object -Sum Length).Sum / 1MB), 1)
Write-Host "包内容 : $fileCount 个文件 / $sizeMb MB"

if ($CheckOnly) {
    Write-Host ""
    Write-Host "[CheckOnly] 校验通过，未做任何 git 操作。" -ForegroundColor Green
    return
}

# ------------------------------------------------------------------ 组装 orphan 提交

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('rm-upm-' + ([guid]::NewGuid().ToString('N').Substring(0, 8)))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    Invoke-Git @('init', '--quiet', $tempRoot) | Out-Null
    Invoke-Git @('-C', $tempRoot, 'checkout', '--quiet', '-b', $Branch) | Out-Null

    # 复制包内容（/XD .git：不复制临时仓库自己的元数据）
    & robocopy $PackagePath $tempRoot /E /NFL /NDL /NJH /NJS /NP /XD .git | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "复制包内容失败（robocopy 退出码 $LASTEXITCODE）。" }

    Invoke-Git @('-C', $tempRoot, 'add', '-A') | Out-Null

    if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
        $CommitMessage = "upm: $packageName@$packageVersion"
    }
    Invoke-Git @('-C', $tempRoot, '-c', 'user.name=upm-publish', '-c', 'user.email=upm-publish@localhost',
        'commit', '--quiet', '-m', $CommitMessage) | Out-Null

    $commit = ([string](Invoke-Git @('-C', $tempRoot, 'rev-parse', 'HEAD'))).Trim()
    Write-Host "提交    : $commit"

    # 写回当前仓库的本地分支（upm 不是当前检出分支，可以直接推；方向：临时仓库 → 本仓库）
    Invoke-Git @('-C', $tempRoot, 'push', '--quiet', '--force', $repoRoot, "HEAD:refs/heads/$Branch") | Out-Null
    Write-Host "本地分支: $Branch 已更新" -ForegroundColor Green

    $remoteUrl = [string](& git -C $repoRoot remote get-url $Remote 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
        throw "找不到远端 $Remote。"
    }
    $remoteUrl = $remoteUrl.Trim()

    # ssh 形式换成 https，方便直接粘进 manifest.json
    $installUrl = $remoteUrl
    if ($remoteUrl -match '^git@([^:]+):(.+?)(\.git)?$') {
        $installUrl = 'https://' + $Matches[1] + '/' + $Matches[2]
    }
    if (-not $installUrl.EndsWith('.git')) { $installUrl += '.git' }
    $installUrl = $installUrl + '#' + $Branch

    Write-Host ""
    Write-Host "安装地址（Packages/manifest.json）："
    Write-Host ('  "{0}": "{1}"' -f $packageName, $installUrl) -ForegroundColor Cyan

    if ($Push) {
        Write-Host ""
        Write-Host "推送   : $Remote → $Branch" -ForegroundColor Yellow
        Invoke-Git @('-C', $repoRoot, 'push', '--force', $Remote, "${Branch}:${Branch}") | Out-Null
        Write-Host "已推送。稍等片刻即可在 Unity 里用上面的 git URL 安装（也可用 #vX.Y.Z 固定版本）。" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "只更新了本地分支。确认无误后执行下面一行推远端：" -ForegroundColor Yellow
        Write-Host "  git push --force $Remote ${Branch}:${Branch}"
    }
}
finally {
    if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
