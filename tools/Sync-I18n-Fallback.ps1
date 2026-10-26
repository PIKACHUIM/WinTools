# 多语言收尾：
#   1) 删除已不再使用的文案键（界面里没有任何引用）
#   2) 其余缺键用英文兜底填充，保证切换语言时不会出现空白
# 用法：pwsh tools/Sync-I18n-Fallback.ps1 [-Check]
param([switch]$Check)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$i18nDir = Join-Path $root 'src/WinTools.App/Assets/i18n'

# 这些键在代码与 XAML 中已无引用（概览页重构后遗留）
$obsolete = @(
    'Dashboard.Os', 'Dashboard.Disk', 'Dashboard.QuickActions', 'Dashboard.GoCleanup',
    'Dashboard.GoOptimize', 'Dashboard.FreeSpace', 'Dashboard.Elevated', 'Dashboard.NotElevated',
    'Dashboard.Threads', 'Dashboard.DiskFree', 'Dashboard.Usage', 'Dashboard.Storage',
    'Dashboard.Uptime', 'Dashboard.NetDown', 'Dashboard.NetUp', 'Dashboard.DiskActivity',
    'Dashboard.Bios', 'Dashboard.Socket', 'Dashboard.Cores', 'Dashboard.Clock', 'Dashboard.Architecture'
)

function Read-Json([string]$path) {
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) | ConvertFrom-Json
}

function Write-Json([string]$path, $object) {
    $json = ConvertTo-Json -InputObject $object -Depth 6
    [IO.File]::WriteAllText($path, $json, (New-Object Text.UTF8Encoding($false)))
}

$files = Get-ChildItem $i18nDir -Filter '*.json'
$reference = Read-Json (Join-Path $i18nDir 'en-US.json')
$refKeys = $reference.PSObject.Properties.Name | Where-Object { $obsolete -notcontains $_ }

$totalFilled = 0
$totalRemoved = 0

foreach ($file in $files) {
    $json = Read-Json $file.FullName
    $props = [ordered]@{}

    # 按参考顺序重建，保证各语言键序一致
    foreach ($key in $refKeys) {
        $value = $json.PSObject.Properties[$key]
        if ($null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value.Value)) {
            $props[$key] = $value.Value
        }
        else {
            if (-not $Check) {
                $props[$key] = $reference.PSObject.Properties[$key].Value
                $totalFilled++
            }
            else {
                $props[$key] = ''
            }
        }
    }

    $removed = @($json.PSObject.Properties.Name | Where-Object { $obsolete -contains $_ }).Count
    $totalRemoved += $removed

    if (-not $Check) {
        Write-Json $file.FullName ([pscustomobject]$props)
        Write-Host ("{0,-10} 键 {1}  删除废弃 {2}" -f $file.BaseName, $props.Count, $removed)
    }
}

if ($Check) {
    Write-Host '检查模式：未写入任何文件'
}
else {
    Write-Host "完成：删除废弃键 $totalRemoved 处，英文兜底 $totalFilled 处"
}
