# Build tweaks-reference.json from the vendored winutil snapshot.
# The snapshot is committed under tools/reference so the app never reads the network.
# Only entries whitelisted in winutil-meta.json are converted; registry/service ops only.
param(
    [switch]$Check
)

$root = Split-Path -Parent $PSScriptRoot
$snapshotPath = Join-Path $root 'tools/reference/winutil-tweaks.json'
$metaPath = Join-Path $root 'tools/reference/winutil-meta.json'
$rulesDir = Join-Path $root 'src/WinTools.Infrastructure/Rules'
$target = Join-Path $rulesDir 'tweaks-reference.json'

if (-not (Test-Path $snapshotPath)) { throw "missing snapshot: $snapshotPath" }
if (-not (Test-Path $metaPath)) { throw "missing meta: $metaPath" }

$snapshot = Get-Content $snapshotPath -Raw -Encoding UTF8 | ConvertFrom-Json
# PS 5.1 wraps a root JSON array in a single object; enumerate to get real items
$meta = @(Get-Content $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ })

function Normalize-Path([string]$path) {
    $value = $path.Replace('\\', '\')
    while ($value.Contains('\\')) { $value = $value.Replace('\\', '\') }
    return $value.TrimStart('\')
}

function Get-Key([string]$hive, [string]$path, [string]$name) {
    return (($hive + '\' + $path + '|' + $name).ToLowerInvariant())
}

# Keys already covered by the hand-written rule files.
$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in @('tweaks.json', 'tweaks-extra.json')) {
    $full = Join-Path $rulesDir $file
    if (-not (Test-Path $full)) { continue }
    foreach ($item in (Get-Content $full -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ })) {
        foreach ($op in $item.apply) {
            if ($op.type -eq 'RegSet') {
                [void]$used.Add((Get-Key $op.hive $op.path $op.name))
            }
            elseif ($op.type -eq 'ServiceStart') {
                [void]$used.Add(('svc|' + $op.service).ToLowerInvariant())
            }
        }
    }
}

function Convert-Kind([string]$kind) {
    switch ($kind) {
        'DWord' { return 'DWord' }
        'QWord' { return 'QWord' }
        'String' { return 'String' }
        'ExpandString' { return 'ExpandString' }
        'Binary' { return 'Binary' }
        'MultiString' { return 'MultiString' }
        default { return 'String' }
    }
}

function Convert-StartMode([string]$type) {
    switch ($type) {
        'Disabled' { return 'disabled' }
        'Manual' { return 'manual' }
        'Automatic' { return 'auto' }
        default { return 'manual' }
    }
}

function New-Id([string]$raw) {
    $name = $raw -replace '^WPFTweaks', '' -replace '^WPFToggle', '' -replace '^WPF', ''
    $name = [regex]::Replace($name, '([a-z0-9])([A-Z])', '$1-$2')
    return 'wu-' + $name.ToLowerInvariant()
}

$entries = New-Object System.Collections.ArrayList
$skipped = New-Object System.Collections.ArrayList

foreach ($item in $meta) {
    $config = $snapshot.($item.winutilId)
    if ($null -eq $config) {
        [void]$skipped.Add($item.winutilId + ' (not in snapshot)')
        continue
    }

    $apply = New-Object System.Collections.Generic.List[object]
    $revert = New-Object System.Collections.Generic.List[object]

    foreach ($reg in @($config.registry)) {
        if ($null -eq $reg -or [string]::IsNullOrWhiteSpace($reg.Path)) { continue }

        $idx = $reg.Path.IndexOf(':')
        if ($idx -lt 0) { continue }

        $hive = $reg.Path.Substring(0, $idx).ToUpperInvariant()
        $path = Normalize-Path $reg.Path.Substring($idx + 1)
        $key = Get-Key $hive $path $reg.Name
        if ($used.Contains($key)) { continue }

        $kind = Convert-Kind $reg.Type
        $apply.Add([ordered]@{
            type  = 'RegSet'
            hive  = $hive
            path  = $path
            name  = $reg.Name
            kind  = $kind
            value = [string]$reg.Value
        })

        $original = [string]$reg.OriginalValue
        if ($original -eq '<RemoveEntry>' -or [string]::IsNullOrEmpty($original)) {
            $revert.Add([ordered]@{
                type = 'RegDeleteValue'
                hive = $hive
                path = $path
                name = $reg.Name
            })
        }
        else {
            $revert.Add([ordered]@{
                type  = 'RegSet'
                hive  = $hive
                path  = $path
                name  = $reg.Name
                kind  = $kind
                value = $original
            })
        }

        [void]$used.Add($key)
    }

    foreach ($svc in @($config.service)) {
        if ($null -eq $svc -or [string]::IsNullOrWhiteSpace($svc.Name)) { continue }

        $svcKey = ('svc|' + $svc.Name).ToLowerInvariant()
        if ($used.Contains($svcKey)) { continue }

        $op = [ordered]@{
            type      = 'ServiceStart'
            service   = $svc.Name
            startMode = (Convert-StartMode $svc.StartupType)
        }

        $apply.Add($op)
        $revert.Add([ordered]@{
            type      = 'ServiceStart'
            service   = $svc.Name
            startMode = (Convert-StartMode $svc.OriginalType)
        })

        [void]$used.Add($svcKey)
    }

    if ($apply.Count -eq 0) {
        [void]$skipped.Add($item.winutilId + ' (already covered)')
        continue
    }

    [void]$entries.Add([ordered]@{
        id          = (New-Id $item.winutilId)
        group       = $item.group
        recommended = [bool]$item.recommended
        name        = [ordered]@{ 'zh-Hans' = $item.zh; 'en-US' = $item.en }
        description = [ordered]@{ 'zh-Hans' = $item.descZh; 'en-US' = $item.descEn }
        risk        = $item.risk
        detect      = $apply.ToArray()
        apply       = $apply.ToArray()
        revert      = $revert.ToArray()
    })
}

$json = if ($entries.Count -eq 0) {
    '[]'
}
else {
    ConvertTo-Json -InputObject ([object[]]$entries.ToArray()) -Depth 8
}

$json = $json -replace "`r`n", "`n"

if ($Check) {
    $existing = if (Test-Path $target) { Get-Content $target -Raw -Encoding UTF8 } else { '' }
    if (($existing -replace "`r`n", "`n") -ne $json) {
        Write-Host 'tweaks-reference.json is out of date; run tools/Sync-ReferenceTweaks.ps1'
        exit 1
    }

    Write-Host "tweaks-reference.json up to date ($($entries.Count) entries)"
    exit 0
}

[IO.File]::WriteAllText($target, $json, (New-Object Text.UTF8Encoding($false)))

Write-Host "generated $($entries.Count) entries -> $target"
if ($skipped.Count -gt 0) {
    Write-Host ("skipped: " + ($skipped -join '; '))
}
