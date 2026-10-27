# Convert ZyperWin++ ZyperData.xml into tweaks-zyperwin.json.
# The upstream XML ships inside the release zip and is NOT committed.
#   powershell -File tools/Sync-ZyperTweaks.ps1 -Xml <path-to-ZyperData.xml>
param(
    [string]$Xml = ''
)

$root = Split-Path -Parent $PSScriptRoot
$metaPath = Join-Path $root "tools/reference/zyper-meta.json"
$rulesDir = Join-Path $root "src/WinTools.Infrastructure/Rules"
$target = Join-Path $rulesDir "tweaks-zyperwin.json"

$meta = Get-Content $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
$excludeCats = @($meta.excludeCategories)
$excludeNames = @($meta.excludeNames)
$highRisk = @($meta.highRisk)
$mediumRisk = @($meta.mediumRisk)

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

    $rest = $key.Substring($idx + 1)
    return [pscustomobject]@{ Hive = $hive; Path = (Normalize-Path $rest) }
}

function Convert-Kind([string]$type) {
    $upper = $type.ToUpperInvariant()
    if ($upper -eq "REG_DWORD") { return "DWord" }
    if ($upper -eq "REG_QWORD") { return "QWord" }
    if ($upper -eq "REG_SZ") { return "String" }
    if ($upper -eq "REG_EXPAND_SZ") { return "ExpandString" }
    if ($upper -eq "REG_MULTI_SZ") { return "MultiString" }
    if ($upper -eq "REG_BINARY") { return "Binary" }
    return ""
}

function Convert-Start([string]$type) {
    if ($type -eq "2") { return "auto" }
    if ($type -eq "3") { return "manual" }
    if ($type -eq "4") { return "disabled" }
    return "manual"
}

function Convert-Ops($container) {
    $ops = New-Object System.Collections.ArrayList
    if ($null -eq $container) {
        return $ops
    }

    foreach ($op in @($container.ChildNodes)) {
        if ($op.NodeType -ne "Element") {
            continue
        }

        # 注意：PowerShell 会把 XML 属性暴露成同名属性，Name 属性可能是 <SetServiceStart Name="DPS"> 里的 DPS
        $opName = [string]$op.LocalName

        if ($opName -eq "RegWrite") {
            $split = Split-Hive $op.GetAttribute("Key")
            $kind = Convert-Kind $op.GetAttribute("Type")
            if ($null -eq $split -or $kind -eq "") {
                continue
            }

            [void]$ops.Add([ordered]@{
                type  = "RegSet"
                hive  = $split.Hive
                path  = $split.Path
                name  = $op.GetAttribute("Value")
                kind  = $kind
                value = $op.GetAttribute("Data")
            })
        }
        elseif ($opName -eq "RegDelete") {
            $split = Split-Hive $op.GetAttribute("Key")
            if ($null -eq $split) {
                continue
            }

            $value = $op.GetAttribute("Value")
            if ([string]::IsNullOrEmpty($value)) {
                [void]$ops.Add([ordered]@{
                    type = "RegDeleteKey"
                    hive = $split.Hive
                    path = $split.Path
                })
            }
            else {
                [void]$ops.Add([ordered]@{
                    type = "RegDeleteValue"
                    hive = $split.Hive
                    path = $split.Path
                    name = $value
                })
            }
        }
        elseif ($opName -eq "SetServiceStart") {
            [void]$ops.Add([ordered]@{
                type      = "ServiceStart"
                service   = $op.GetAttribute("Name")
                startMode = (Convert-Start $op.GetAttribute("Type"))
            })
        }
        elseif ($opName -eq "ExplorerNotify" -and $op.GetAttribute("Type") -eq "Cmd") {
            $cmd = $op.GetAttribute("Cmd")
            if ([string]::IsNullOrWhiteSpace($cmd)) {
                continue
            }

            $parts = $cmd.Trim() -split "\s+", 2
            $arguments = ""
            if ($parts.Count -gt 1) {
                $arguments = $parts[1]
            }

            [void]$ops.Add([ordered]@{
                type      = "Command"
                fileName  = $parts[0]
                arguments = $arguments
            })
        }
    }

    return $ops
}

$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in @("tweaks.json", "tweaks-extra.json", "tweaks-reference.json", "tweaks-optimizer.json")) {
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
            elseif ($op.type -eq "ServiceStart") {
                [void]$used.Add(("svc|" + $op.service).ToLowerInvariant())
            }
        }
    }
}

function Test-Excluded([string]$name) {
    foreach ($word in $excludeNames) {
        if ($name.Contains($word)) {
            return $true
        }
    }
    return $false
}

function Get-Risk([string]$name) {
    foreach ($word in $highRisk) {
        if ($name.Contains($word)) {
            return "High"
        }
    }
    foreach ($word in $mediumRisk) {
        if ($name.Contains($word)) {
            return "Medium"
        }
    }
    return "Low"
}

if ([string]::IsNullOrWhiteSpace($Xml) -or -not (Test-Path $Xml)) {
    throw "missing -Xml <path to ZyperData.xml>"
}

[xml]$doc = Get-Content $Xml -Raw -Encoding UTF8
$entries = New-Object System.Collections.ArrayList

foreach ($config in @($doc.ZyperConfigurations.Configuration)) {
    $category = [string]$config.category
    if ($excludeCats -contains $category) {
        continue
    }

    $group = ""
    $groupProp = $meta.categories.PSObject.Properties[$category]
    if ($null -ne $groupProp) {
        $group = [string]$groupProp.Value
    }

    if ([string]::IsNullOrWhiteSpace($group)) {
        continue
    }

    $index = 0
    foreach ($item in @($config.Item)) {
        $index++

        $name = ([string]$item.name) -replace "^\d+.", ""
        $name = $name.Trim()

        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        if (Test-Excluded $name) {
            continue
        }

        # @() 保证单条操作时仍是数组，避免 OrderedDictionary 被按位置取值
        $apply = @(Convert-Ops $item.Optimize)
        $revert = @(Convert-Ops $item.Restore)

        $keptApply = New-Object System.Collections.ArrayList
        $keptRevert = New-Object System.Collections.ArrayList

        $opIndex = 0
        foreach ($op in $apply) {
            $opIndex++
            $keep = $true

            if ($op.type -eq "RegSet") {
                $key = Get-Key $op.hive $op.path $op.name
                if ($used.Contains($key)) {
                    $keep = $false
                }
                else {
                    [void]$used.Add($key)
                }
            }
            elseif ($op.type -eq "ServiceStart") {
                $key = ("svc|" + $op.service).ToLowerInvariant()
                if ($used.Contains($key)) {
                    $keep = $false
                }
                else {
                    [void]$used.Add($key)
                }
            }

            if (-not $keep) {
                continue
            }

            [void]$keptApply.Add($op)

            if ($opIndex -le $revert.Count) {
                [void]$keptRevert.Add($revert[$opIndex - 1])
            }
        }

        if ($keptApply.Count -eq 0) {
            continue
        }

        $risk = Get-Risk $name

        [void]$entries.Add([ordered]@{
            id          = "zy-" + $category + "-" + $index.ToString("00")
            group       = $group
            recommended = ($risk -eq "Low")
            name        = [ordered]@{ "zh-Hans" = $name; "en-US" = $name }
            description = [ordered]@{ "zh-Hans" = $name; "en-US" = $name }
            risk        = $risk
            detect      = $keptApply.ToArray()
            apply       = $keptApply.ToArray()
            revert      = $keptRevert.ToArray()
        })
    }
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
