# 把 docs/readme 下各语言 README 的「功能介绍」改成：正文常显 + 截图折叠
# 用法：pwsh tools/Fix-ReadmeLayout.ps1 [-DryRun]
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$files = Get-ChildItem (Join-Path $root 'docs/readme') -Filter '*.md'

# 各语言里那句「点标题展开」的提示，直接删掉
$prompts = @(
    'Click a title to expand that module; collapsed, each takes one line.',
    '點選標題展開對應模組，收合後只佔一行。',
    'タイトルをクリックすると展開します。折りたたむと 1 行に収まります。',
    '제목을 클릭하면 펼쳐집니다. 접으면 한 줄만 차지합니다.',
    'Titel anklicken zum Aufklappen; zugeklappt belegt jeder Modul eine Zeile.',
    'Cliquez sur un titre pour déplier le module ; replié, il tient sur une ligne.',
    'Pulsa un título para desplegar el módulo; plegado ocupa una línea.',
    'Clique no título para expandir o módulo; recolhido ocupa uma linha.',
    'Clicca un titolo per espandere il modulo; chiuso occupa una riga.',
    'Нажмите на заголовок, чтобы раскрыть модуль; в свёрнутом виде он занимает одну строку.',
    'اضغط على العنوان لتوسيع القسم؛ وعند الطي يشغل سطرًا واحدًا.'
)

# summary 里的「截图」按语言本地化
$shotLabels = @{
    'README.zh-TW.md' = '截圖'
    'README.en.md'    = 'Screenshot'
    'README.ja.md'    = 'スクリーンショット'
    'README.ko.md'    = '스크린샷'
    'README.de.md'    = 'Screenshot'
    'README.fr.md'    = "Capture d'écran"
    'README.es.md'    = 'Captura de pantalla'
    'README.pt.md'    = 'Captura de tela'
    'README.it.md'    = 'Schermata'
    'README.ru.md'    = 'Скриншот'
    'README.ar.md'    = 'لقطة الشاشة'
}

$pattern = '(?s)<details[^>]*>\s*<summary><b>(?<title>[^<]+)</b>[^<]*</summary>(?<body>.*?)</details>'

foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)

    foreach ($prompt in $prompts) {
        $text = $text.Replace($prompt, '')
    }

    $label = $shotLabels[$file.Name]
    if (-not $label) { $label = 'Screenshot' }

    $ms = [regex]::Matches($text, $pattern)
    for ($i = $ms.Count - 1; $i -ge 0; $i--) {
        $m = $ms[$i]
        $title = $m.Groups['title'].Value.Trim()
        $body = $m.Groups['body'].Value

        $img = [regex]::Match($body, '(?s)!\[(?<alt>[^\]]*)\]\((?<path>[^)]+)\)')
        if (-not $img.Success) { continue }

        $prose = $body.Substring(0, $img.Index).Trim()
        $block = "### $title`r`n`r`n$prose`r`n`r`n<details>`r`n<summary>$label</summary>`r`n`r`n$($img.Value)`r`n`r`n</details>"

        $text = $text.Substring(0, $m.Index) + $block + $text.Substring($m.Index + $m.Length)
    }

    # 删掉提示语后可能留下连续空行，收成一行
    $text = [regex]::Replace($text, "(\r?\n){3,}", "`r`n`r`n")

    if ($DryRun) {
        Write-Host "=== $($file.Name) 预览 ==="
        ($text -split "`n" | Select-String -Pattern '^### |<summary>|<details|!#' | Select-Object -First 6) | ForEach-Object { $_.Line }
    }
    else {
        [IO.File]::WriteAllText($file.FullName, $text, (New-Object Text.UTF8Encoding($false)))
        Write-Host "$($file.Name)  模块 $($ms.Count) 个"
    }
}
