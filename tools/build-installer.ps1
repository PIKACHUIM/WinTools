# 用 Inno Setup 打包安装程序
# 依赖：Inno Setup 6（CI 里由 chocolatey 安装，本地需自行安装并确保 ISCC.exe 在 PATH 或默认目录）
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$SourceDir = "",

    [string]$OutputDir = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $SourceDir) { $SourceDir = Join-Path $root 'artifacts/publish/framework' }
if (-not $OutputDir) { $OutputDir = Join-Path $root 'artifacts/publish' }

if (-not (Test-Path (Join-Path $SourceDir 'WinTools.exe'))) {
    throw "源目录里没有 WinTools.exe：$SourceDir（先运行 tools/publish.ps1 -Profile framework）"
}

$iscc = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}

if (-not $iscc) {
    throw '找不到 ISCC.exe，请先安装 Inno Setup 6'
}

$normalized = $Version.Trim().TrimStart('v', 'V')
$iss = Join-Path $root 'installer/WinTools.iss'

# 简体中文语言包随仓库提供；缺失时 .iss 会退回只用英文
$zhIsl = Join-Path $root 'installer/languages/ChineseSimplified.isl'
$languages = if (Test-Path $zhIsl) { '简体中文 + English' } else { '仅 English（缺少 languages/ChineseSimplified.isl）' }

Write-Host "==> 打包安装程序：$normalized（界面语言：$languages）" -ForegroundColor Cyan

& $iscc `
    "/DAppVersion=$normalized" `
    "/DSourceDir=$SourceDir" `
    "/DOutputDir=$OutputDir" `
    $iss

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup 编译失败（退出码 $LASTEXITCODE）"
}

$setup = Join-Path $OutputDir "WinTools-$normalized-setup-win-x64.exe"
if (-not (Test-Path $setup)) {
    throw "未生成安装包：$setup"
}

$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "==> 已生成 $setup（$size MB）" -ForegroundColor Green
Write-Output $setup
