<#
.SYNOPSIS
    生成两种发行形态：绿色便携单文件（自包含）与框架依赖包。
.DESCRIPTION
    portable  —— 自包含单文件 exe，解压即用，体积较大（含 .NET 运行时）。
    framework —— 框架依赖，体积小，需目标机安装 .NET 10 Desktop Runtime。
.EXAMPLE
    pwsh tools/publish.ps1 -Profile both
#>
[CmdletBinding()]
param(
    [ValidateSet('portable', 'framework', 'both')]
    [string]$Profile = 'both',

    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet/dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$project = Join-Path $root 'src/WinTools.App/WinTools.App.csproj'
$outRoot = Join-Path $root 'artifacts/publish'

function Invoke-Publish {
    param([string]$Name, [string[]]$PublishArgs)

    $out = Join-Path $outRoot $Name
    Write-Host "==> 发布 $Name ($Runtime)" -ForegroundColor Cyan

    # 必须清空：publish 不会删除上一版残留文件，否则体积统计与产物都会失真
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    & $dotnet publish $project -c Release -r $Runtime -o $out --nologo @PublishArgs
    if ($LASTEXITCODE -ne 0) { throw "发布失败: $Name" }

    $bytes = (Get-ChildItem $out -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Host ("    {0}: {1:N1} MB / {2} 个文件" -f $Name, ($bytes / 1MB), (Get-ChildItem $out -Recurse -File).Count) -ForegroundColor Green
    return $bytes
}

if ($Profile -in @('portable', 'both')) {
    Invoke-Publish -Name 'portable' -PublishArgs @(
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:IncludeAllContentForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:PublishTrimmed=false',
        '-p:DebugType=none',
        '-p:DebugSymbols=false'
    ) | Out-Null
}

if ($Profile -in @('framework', 'both')) {
    Invoke-Publish -Name 'framework' -PublishArgs @(
        '--self-contained', 'false',
        '-p:DebugType=none',
        '-p:DebugSymbols=false'
    ) | Out-Null
}

Write-Host "完成，输出目录: $outRoot" -ForegroundColor Cyan
