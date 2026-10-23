# 把 tools/rules/legacy/F盘缓存垃圾清理清单.csv 转成引擎用的声明式规则 JSON
# 用法: powershell -ExecutionPolicy Bypass -File tools/Convert-CleanupRules.ps1
$ErrorActionPreference = 'Stop'

$root   = Split-Path -Parent $PSScriptRoot
$csv    = Join-Path $root 'tools\rules\legacy\F盘缓存垃圾清理清单.csv'
$names  = Join-Path $root 'tools\rule-names-en.csv'
$outDir = Join-Path $root 'src\WinTools.Infrastructure\Rules'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$catMap = @{
    '系统临时'='SystemTemp'; '系统更新'='SystemUpdate'; '系统日志'='SystemLog'; '系统组件'='SystemComponent'
    '崩溃转储'='CrashDump'; '系统缓存'='SystemCache'; '页面文件'='PageFile'; '回收站'='RecycleBin'
    '浏览器'='Browser'; '开发缓存'='DevCache'; '开发日志'='DevLog'; '开发构建'='DevBuild'
    '应用缓存'='AppCache'; '应用日志'='AppLog'; '盘根杂项'='RootMisc'
}
$matchMap = @{
    '目录递归'='DirectoryRecursive'; '单文件'='SingleFile'; '用户目录内文件(通配)'='GlobFile'
    '系统文件'='SystemFile'; '扩展名扫描'='ExtensionScan'
}
$stratMap = @{
    '删除内容保留目录'='DeleteContents'; '整目录删除'='DeleteDirectory'; '按时间阈值删除'='DeleteByAge'
    '删除文件'='DeleteFile'; '禁止清理'='Disabled'; '禁止自动清理'='Disabled'; '提示用户确认'='DeleteContents'
}

$en = @{}
Import-Csv $names -Header id, name -Encoding UTF8 | ForEach-Object { $en[$_.id] = $_.name }

# 过于宽泛、必须靠项目级识别才能处理的规则，默认关闭
$forceDisabled = @('DEV-31','DEV-32','DEV-33')

function Convert-Template([string]$t) {
    $t = $t.Trim()
    if ($t -match '^[A-Za-z]:\\Users\\\*\\(.*)$') { return @("{AllUsers}\$($Matches[1])") }
    if ($t -match '^%LOCALAPPDATA%\\(.*)$')  { return @($t, "{AllUsers}\AppData\Local\$($Matches[1])") }
    if ($t -match '^%APPDATA%\\(.*)$')       { return @($t, "{AllUsers}\AppData\Roaming\$($Matches[1])") }
    if ($t -match '^%USERPROFILE%\\(.*)$')   { return @($t, "{AllUsers}\$($Matches[1])") }
    if ($t -match '^[A-Za-z]:\\(.*)$')       { return @("{Drive}\$($Matches[1])") }
    return @($t)
}

$rules = New-Object System.Collections.Generic.List[object]
foreach ($row in (Import-Csv $csv -Encoding UTF8)) {
    $id = $row.'规则ID'
    if ([string]::IsNullOrWhiteSpace($id)) { continue }

    $templates = @()
    foreach ($t in (Convert-Template $row.'环境变量模板')) { if ($templates -notcontains $t) { $templates += $t } }

    $strategy = $stratMap[$row.'清理策略']
    if (-not $strategy) { $strategy = 'Disabled' }

    $rules.Add([ordered]@{
        id          = $id
        category    = $catMap[$row.'分类']
        name        = [ordered]@{ 'zh-Hans' = $row.'规则名称'; 'en-US' = $(if ($en[$id]) { $en[$id] } else { $row.'规则名称' }) }
        paths       = $templates
        match       = $matchMap[$row.'匹配方式']
        strategy    = $strategy
        safety      = $row.'安全等级'
        requiresAdmin      = ($row.'需管理员' -eq 'Y')
        requiresProcessStop = ($row.'需关闭进程' -eq 'Y')
        requiresConfirm    = ($row.'清理策略' -eq '提示用户确认')
        ageDays     = 7
        enabled     = -not ($forceDisabled -contains $id)
        note        = [ordered]@{ 'zh-Hans' = $row.'备注'; 'en-US' = '' }
    })
}

$json = $rules | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText((Join-Path $outDir 'cleanup-rules.json'), $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "cleanup-rules.json: $($rules.Count) rules"
