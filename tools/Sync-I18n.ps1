# 把 tools/i18n/*.psv 合并进 src/WinTools.App/Assets/i18n/*.json，并检查各语言是否缺键。
#
# PSV 格式（竖线分隔，# 开头为注释，第一行有效行必须是表头）：
#   key|zh-Hans|zh-Hant|en-US|ja-JP|ko-KR|de-DE|fr-FR|es-ES|pt-BR|it-IT|ru-RU|ar-SA
# 值里不允许出现竖线；需要换行请写 \n。
#
# 用法：
#   powershell -File tools/Sync-I18n.ps1          合并并写回
#   powershell -File tools/Sync-I18n.ps1 -Check   只检查：列出各语言相对 zh-Hans 缺失的键，缺失则退出码为 1
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$i18nDir = Join-Path $root 'src\WinTools.App\Assets\i18n'
$psvDir = Join-Path $PSScriptRoot 'i18n'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$languages = @('zh-Hans', 'zh-Hant', 'en-US', 'ja-JP', 'ko-KR', 'de-DE', 'fr-FR', 'es-ES', 'pt-BR', 'it-IT', 'ru-RU', 'ar-SA')

function Read-Catalog([string]$path) {
    $map = [ordered]@{}
    $json = [IO.File]::ReadAllText($path, $utf8) | ConvertFrom-Json
    foreach ($p in $json.PSObject.Properties) {
        $map[$p.Name] = [string]$p.Value
    }
    return $map
}

function ConvertTo-JsonString([string]$value) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $value.ToCharArray()) {
        switch ($ch) {
            '"' { [void]$sb.Append('\"') }
            '\' { [void]$sb.Append('\\') }
            "`n" { [void]$sb.Append('\n') }
            "`r" { [void]$sb.Append('\r') }
            "`t" { [void]$sb.Append('\t') }
            default {
                if ([int]$ch -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', [int]$ch) }
                else { [void]$sb.Append($ch) }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Write-Catalog([string]$path, $map) {
    $lines = New-Object System.Collections.Generic.List[string]
    $previousGroup = $null
    foreach ($key in $map.Keys) {
        $group = $key.Split('.')[0]
        if ($null -ne $previousGroup -and $group -ne $previousGroup) { $lines.Add('') }
        $previousGroup = $group
        $lines.Add('  ' + (ConvertTo-JsonString $key) + ': ' + (ConvertTo-JsonString $map[$key]))
    }

    # 逗号放在行尾；空行不带逗号
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add('{')
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -eq '') { $out.Add(''); continue }
        $next = $null
        for ($j = $i + 1; $j -lt $lines.Count; $j++) { if ($lines[$j] -ne '') { $next = $lines[$j]; break } }
        $out.Add($lines[$i] + $(if ($null -ne $next) { ',' } else { '' }))
    }
    $out.Add('}')
    [IO.File]::WriteAllText($path, ($out -join "`r`n") + "`r`n", $utf8)
}

$catalogs = @{}
foreach ($lang in $languages) {
    $catalogs[$lang] = Read-Catalog (Join-Path $i18nDir "$lang.json")
}

if (-not $Check -and (Test-Path $psvDir)) {
    $added = 0
    foreach ($file in (Get-ChildItem $psvDir -Filter *.psv | Sort-Object Name)) {
        $header = $null
        $lineNo = 0
        foreach ($raw in [IO.File]::ReadAllLines($file.FullName, $utf8)) {
            $lineNo++
            $line = $raw.Trim()
            if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }

            $cells = $raw.Split('|')
            if ($null -eq $header) {
                $header = $cells | ForEach-Object { $_.Trim() }
                if ($header[0] -ne 'key') { throw "$($file.Name):$lineNo 表头必须以 key 开头" }
                continue
            }

            if ($cells.Count -ne $header.Count) {
                throw "$($file.Name):$lineNo 列数应为 $($header.Count)，实际 $($cells.Count)：$raw"
            }

            $key = $cells[0].Trim()
            for ($c = 1; $c -lt $header.Count; $c++) {
                $lang = $header[$c]
                if (-not $catalogs.ContainsKey($lang)) { throw "$($file.Name) 含未知语言列 $lang" }
                $value = $cells[$c].Trim().Replace('\n', "`n")
                if ($value.Length -eq 0) { throw "$($file.Name):$lineNo [$key] 的 $lang 为空" }
                if (-not $catalogs[$lang].Contains($key) -or $catalogs[$lang][$key] -ne $value) { $added++ }
                $catalogs[$lang][$key] = $value
            }
        }
    }

    foreach ($lang in $languages) {
        Write-Catalog (Join-Path $i18nDir "$lang.json") $catalogs[$lang]
    }

    Write-Host "已合并，新增或更新 $added 个值"
}

# 完整度检查：以 zh-Hans 为基准
$baseline = $catalogs['zh-Hans'].Keys
$missingTotal = 0
foreach ($lang in $languages) {
    if ($lang -eq 'zh-Hans') { continue }
    $missing = @($baseline | Where-Object { -not $catalogs[$lang].Contains($_) })
    $extra = @($catalogs[$lang].Keys | Where-Object { -not $catalogs['zh-Hans'].Contains($_) })
    $missingTotal += $missing.Count
    Write-Host ("{0,-8} 键 {1,4}  缺失 {2,3}  多余 {3,3}" -f $lang, $catalogs[$lang].Count, $missing.Count, $extra.Count)
    if ($Check -and $missing.Count -gt 0) {
        $missing | Select-Object -First 8 | ForEach-Object { Write-Host "         - $_" }
        if ($missing.Count -gt 8) { Write-Host "         ... 另有 $($missing.Count - 8) 个" }
    }
}

if ($Check -and $missingTotal -gt 0) { exit 1 }
