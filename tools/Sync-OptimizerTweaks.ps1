# Convert selected Optimizer methods into tweaks-optimizer.json.
# The upstream C# source is NOT committed; pass its path.
#   powershell -File tools/Sync-OptimizerTweaks.ps1 -Source <path-to-OptimizeHelper.cs>
param(
    [string]$Source = ''
)

$root = Split-Path -Parent $PSScriptRoot
$metaPath = Join-Path $root "tools/reference/optimizer-meta.json"
$rulesDir = Join-Path $root "src/WinTools.Infrastructure/Rules"
$target = Join-Path $rulesDir "tweaks-optimizer.json"

$meta = Get-Content $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ }

function Normalize-Path([string]$path) {
    return $path.Replace("\\", "\").TrimStart("\")
}

function Get-Key([string]$hive, [string]$path, [string]$name) {
    return (($hive + "\" + $path + "|" + $name).ToLowerInvariant())
}

function Split-Hive([string]$key) {
    $idx = $key.IndexOf("\")
    if ($idx -lt 0) {
        return $null
    }

    $hive = $null
    $head = $key.Substring(0, $idx).ToUpperInvariant()
    if ($head -eq "HKEY_CURRENT_USER") { $hive = "HKCU" }
    elseif ($head -eq "HKEY_LOCAL_MACHINE") { $hive = "HKLM" }
    elseif ($head -eq "HKEY_CLASSES_ROOT") { $hive = "HKCR" }
    elseif ($head -eq "HKEY_USERS") { $hive = "HKU" }
    elseif ($head -eq "HKEY_CURRENT_CONFIG") { $hive = "HKCC" }

    if (-not $hive) {
        return $null
    }

    return [pscustomobject]@{ Hive = $hive; Path = (Normalize-Path $key.Substring($idx + 1)) }
}

function Convert-Kind([string]$kind) {
    if ($kind -eq "DWord") { return "DWord" }
    if ($kind -eq "QWord") { return "QWord" }
    if ($kind -eq "String") { return "String" }
    if ($kind -eq "ExpandString") { return "ExpandString" }
    if ($kind -eq "MultiString") { return "MultiString" }
    return ""
}

# Collect method bodies by name.
$lines = Get-Content $Source -Encoding UTF8
$bodies = @{}

$i = 0
while ($i -lt $lines.Count) {
    if ($lines[$i] -match "internal static void (\w+)\(\)") {
        $methodName = $Matches[1]
        $depth = 0
        $started = $false
        $j = $i
        $body = New-Object System.Collections.ArrayList

        while ($j -lt $lines.Count) {
            $cur = $lines[$j]
            if ($j -eq $i -or $started) {
                [void]$body.Add($cur)
            }

            $opens = ([regex]::Matches($cur, "\{")).Count
            $closes = ([regex]::Matches($cur, "\}")).Count
            if ($opens -gt 0) {
                $started = $true
            }

            $depth += $opens - $closes
            if ($started -and $depth -le 0) {
                break
            }

            $j++
        }

        $bodies[$methodName] = $body
        $i = $j
    }

    $i++
}

function Get-Ops([string]$methodName) {
    $ops = New-Object System.Collections.ArrayList
    if (-not $bodies.ContainsKey($methodName)) {
        return $ops
    }

    $rx = [regex]'Registry\.SetValue\(\s*@?"(?<path>HKEY_[^"]*)"\s*,\s*@?"(?<name>[^"]*)"\s*,\s*(?<value>[^,)]+?)\s*(?:,\s*RegistryValueKind\.(?<kind>\w+))?\s*\)\s*;'
    $seen = New-Object System.Collections.Generic.HashSet[string]

    foreach ($line in $bodies[$methodName]) {
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith("//")) {
            continue
        }

        $match = $rx.Match($trimmed)
        if (-not $match.Success) {
            continue
        }

        $split = Split-Hive $match.Groups["path"].Value
        if ($null -eq $split) {
            continue
        }

        $value = $match.Groups["value"].Value.Trim()
        if ($value -match "Assembly|Environment\.|Convert\.|\+") {
            continue
        }

        $kind = $match.Groups["kind"].Value
        if ($kind -eq "") {
            if ($value.StartsWith('"')) { $kind = "String" } else { $kind = "DWord" }
        }

        $kind = Convert-Kind $kind
        if ($kind -eq "") {
            continue
        }

        if ($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $key = Get-Key $split.Hive $split.Path $match.Groups["name"].Value
        if (-not $seen.Add($key)) {
            continue
        }

        [void]$ops.Add([ordered]@{
            type  = "RegSet"
            hive  = $split.Hive
            path  = $split.Path
            name  = $match.Groups["name"].Value
            kind  = $kind
            value = $value
        })
    }

    return $ops
}

# Keys already covered by the other rule files.
$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in @("tweaks.json", "tweaks-extra.json", "tweaks-reference.json", "tweaks-zyperwin.json")) {
    $full = Join-Path $rulesDir $file
    if (-not (Test-Path $full)) {
        continue
    }

    $items = Get-Content $full -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ }
    foreach ($item in $items) {
        foreach ($op in $item.apply) {
            if ($op.type -eq "RegSet") {
                [void]$used.Add((Get-Key $op.hive $op.path $op.name))
            }
        }
    }
}

$entries = New-Object System.Collections.ArrayList

foreach ($item in $meta) {
    # @() 保证单条操作时仍是数组
    $apply = @(Get-Ops $item.apply)
    $revert = @(Get-Ops $item.revert)

    $keptApply = New-Object System.Collections.ArrayList
    $keptRevert = New-Object System.Collections.ArrayList

    foreach ($op in $apply) {
        $key = Get-Key $op.hive $op.path $op.name
        if ($used.Contains($key)) {
            continue
        }

        [void]$used.Add($key)
        [void]$keptApply.Add($op)
    }

    # revert 只保留与 apply 同键的操作，避免把已由其它规则覆盖的键回滚
    foreach ($op in $revert) {
        $key = Get-Key $op.hive $op.path $op.name
        $applyKey = $keptApply | Where-Object { (Get-Key $_.hive $_.path $_.name) -eq $key } | Select-Object -First 1
        if ($null -ne $applyKey) {
            [void]$keptRevert.Add($op)
        }
    }

    if ($keptApply.Count -eq 0) {
        continue
    }

    [void]$entries.Add([ordered]@{
        id          = $item.id
        group       = $item.group
        recommended = [bool]$item.recommended
        name        = [ordered]@{ "zh-Hans" = $item.zh; "en-US" = $item.en }
        description = [ordered]@{ "zh-Hans" = $item.descZh; "en-US" = $item.descEn }
        risk        = $item.risk
        detect      = $keptApply.ToArray()
        apply       = $keptApply.ToArray()
        revert      = $keptRevert.ToArray()
    })
}

if ($entries.Count -eq 0) {
    $json = "[]"
}
else {
    $json = ConvertTo-Json -InputObject ([object[]]$entries.ToArray()) -Depth 8
}

$json = $json -replace "`r`n", "`n"
[IO.File]::WriteAllText($target, $json, (New-Object Text.UTF8Encoding($false)))
Write-Output ("generated " + $entries.Count + " entries -> " + $target)
