# 由 tools/rules/*.txt 生成 cleanup-rules-extra.json
# 每个「路径」生成一条独立规则：既方便用户逐项勾选，也让体积统计更准确。
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$rulesDir = Join-Path $PSScriptRoot 'rules'
$outFile = Join-Path $root 'src\WinTools.Infrastructure\Rules\cleanup-rules-extra.json'
$baseFile = Join-Path $root 'src\WinTools.Infrastructure\Rules\cleanup-rules.json'

# 常见中文后缀 → 英文，用于自动生成 en-US 名称
$suffixMap = [ordered]@{
    '缓存文件'   = 'cache files'
    '缓存目录'   = 'cache folder'
    '升级包'     = 'update packages'
    '更新包'     = 'update packages'
    '安装文件'   = 'installer files'
    '安装残留'   = 'install leftovers'
    '驱动安装残留' = 'driver install leftovers'
    '漏洞补丁'   = 'security patches'
    '临时文件'   = 'temp files'
    '临时目录'   = 'temp folder'
    '日志文件'   = 'log files'
    '日志'       = 'logs'
    '记录'       = 'records'
    '历史记录'   = 'history'
    '缩略图缓存' = 'thumbnail cache'
    '字体缓存'   = 'font cache'
    '扫描记录'   = 'scan records'
    '病毒库备份' = 'definition backups'
    '接收文件'   = 'received files'
    '下载缓存'   = 'download cache'
    '下载临时文件' = 'download temp files'
    '着色器缓存' = 'shader cache'
    '缓存'       = 'cache'
    '残留'       = 'leftovers'
    '文件'       = 'files'
}

function ConvertTo-EnglishName([string]$name) {
    $result = $name
    foreach ($key in $suffixMap.Keys) {
        if ($result.Contains($key)) {
            $result = $result.Replace($key, ' ' + $suffixMap[$key])
        }
    }

    $result = ($result -replace '\s+', ' ').Trim()
    if ($result -match '^[\u4e00-\u9fa5]+$') {
        return $name
    }

    return $result
}

function Get-PathHint([string]$path) {
    $trimmed = $path.TrimEnd('\', '/')
    $last = ($trimmed -split '\\')[-1]
    if ([string]::IsNullOrWhiteSpace($last)) {
        $last = $trimmed
    }

    return $last
}

# 已存在的路径（避免与老规则重复扫描）
$existingPaths = @{}
if (Test-Path $baseFile) {
    $base = Get-Content $baseFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($rule in $base) {
        foreach ($p in $rule.paths) {
            $existingPaths[$p.ToLowerInvariant()] = $true
        }
    }
}

$seen = @{}
$rules = New-Object System.Collections.ArrayList
$now = 0

foreach ($file in (Get-ChildItem $rulesDir -Filter *.txt | Where-Object { $_.Name -ne 'products.txt' } | Sort-Object Name)) {
    foreach ($line in (Get-Content $file.FullName -Encoding UTF8)) {
        $text = $line.Trim()
        if ($text.Length -eq 0 -or $text.StartsWith('#')) {
            continue
        }

        $parts = $text.Split('|')
        if ($parts.Count -lt 7) {
            Write-Warning "跳过格式不正确的行（$($file.Name)）：$text"
            continue
        }

        $baseId = $parts[0].Trim()
        $name = $parts[1].Trim()
        $category = $parts[2].Trim()
        $safety = $parts[3].Trim()
        $match = $parts[4].Trim()
        $strategy = $parts[5].Trim()
        $paths = $parts[6].Split(';')
        $note = if ($parts.Count -ge 8) { $parts[7].Trim() } else { '' }

        $index = 0
        foreach ($rawPath in $paths) {
            $path = $rawPath.Trim()
            if ($path.Length -eq 0) {
                continue
            }

            $key = $path.ToLowerInvariant()
            if ($seen.ContainsKey($key) -or $existingPaths.ContainsKey($key)) {
                continue
            }

            $seen[$key] = $true
            $index++
            $now++

            $needsAdmin = $path -match '^%(SystemRoot|ProgramData|ProgramFiles|ProgramFiles\(x86\)|SystemDrive)%' -or
                          $path -match '^\*:'

            $effectiveStrategy = $strategy
            if ($match -eq 'GlobFile' -or $match -eq 'SingleFile') {
                $effectiveStrategy = 'DeleteFile'
            }

            $displayName = $name
            if ($paths.Count -gt 1) {
                $displayName = "$name · $(Get-PathHint $path)"
            }

            $rule = [ordered]@{
                id                  = '{0}-{1:D2}' -f $baseId, $index
                category            = $category
                name                = [ordered]@{
                    'zh-Hans' = $displayName
                    'en-US'   = (ConvertTo-EnglishName $displayName)
                }
                paths               = @($path)
                match               = $match
                strategy            = $effectiveStrategy
                safety              = $safety
                requiresAdmin       = [bool]$needsAdmin
                requiresProcessStop = $false
                requiresConfirm     = ($safety -eq 'S3')
                ageDays             = 7
                enabled             = $true
                note                = [ordered]@{
                    'zh-Hans' = $note
                    'en-US'   = ''
                }
            }

            [void]$rules.Add($rule)
        }
    }
}

# ---- 产品缓存展开：每个产品都会生成「缓存 / 临时 / 日志 / 更新包 / 下载」等候选路径 ----
$productFile = Join-Path $rulesDir 'products.txt'
$suffixes = @(
    @{ Label = '缓存'; Sub = 'Cache'; Safety = 'S1'; Category = 'AppCache' },
    @{ Label = '缓存 2'; Sub = 'Cache2'; Safety = 'S1'; Category = 'AppCache' },
    @{ Label = '临时文件'; Sub = 'Temp'; Safety = 'S1'; Category = 'AppCache' },
    @{ Label = '日志'; Sub = 'Log'; Safety = 'S1'; Category = 'AppLog' },
    @{ Label = '日志'; Sub = 'Logs'; Safety = 'S1'; Category = 'AppLog' },
    @{ Label = '更新包'; Sub = 'Update'; Safety = 'S2'; Category = 'AppCache' },
    @{ Label = '下载缓存'; Sub = 'Download'; Safety = 'S1'; Category = 'AppCache' },
    @{ Label = '备份文件'; Sub = 'Backup'; Safety = 'S3'; Category = 'AppCache' }
)

if (Test-Path $productFile) {
    $productIndex = 0

    foreach ($line in (Get-Content $productFile -Encoding UTF8)) {
        $text = $line.Trim()
        if ($text.Length -eq 0 -or $text.StartsWith('#')) { continue }

        $parts = $text.Split('|')
        if ($parts.Count -lt 2) { continue }

        $productIndex++
        $productName = $parts[0].Trim()
        $productRoot = $parts[1].Trim().TrimEnd('\')

        for ($i = 0; $i -lt $suffixes.Count; $i++) {
            $suffix = $suffixes[$i]
            $path = "$productRoot\$($suffix.Sub)"
            $key = $path.ToLowerInvariant()

            if ($seen.ContainsKey($key) -or $existingPaths.ContainsKey($key)) { continue }
            $seen[$key] = $true
            $now++

            $displayName = "$productName $($suffix.Label)"

            $rule = [ordered]@{
                id                  = 'PRD-{0:D3}-{1}' -f $productIndex, ($i + 1)
                category            = $suffix.Category
                name                = [ordered]@{
                    'zh-Hans' = $displayName
                    'en-US'   = "$productName $($suffix.Sub.ToLowerInvariant())"
                }
                paths               = @($path)
                match               = 'DirectoryRecursive'
                strategy            = 'DeleteContents'
                safety              = $suffix.Safety
                requiresAdmin       = [bool]($path -match '^%(ProgramData|ProgramFiles|ProgramFiles\(x86\))%')
                requiresProcessStop = $false
                requiresConfirm     = ($suffix.Safety -eq 'S3')
                ageDays             = 7
                enabled             = $true
                note                = [ordered]@{ 'zh-Hans' = ''; 'en-US' = '' }
            }

            [void]$rules.Add($rule)
        }
    }
}

$json = ConvertTo-Json -InputObject @($rules) -Depth 6
[IO.File]::WriteAllText($outFile, $json, (New-Object Text.UTF8Encoding($false)))

Write-Host "已生成 $($rules.Count) 条规则 -> $outFile"

$byCategory = $rules | Group-Object { $_.category } | Sort-Object Count -Descending
foreach ($group in $byCategory) {
    Write-Host ("  {0,-16} {1}" -f $group.Name, $group.Count)
}
