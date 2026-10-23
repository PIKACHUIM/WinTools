# 生成文档站 Logo（docs/public/logo.png，512x512，透明背景）
# 与 tools/Make-Icon.ps1 使用同一套绘制规则，保证视觉一致。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outFile = Join-Path $root 'docs\public\logo.png'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outFile) | Out-Null

$size = 512
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::Transparent)

$pad = [Math]::Max(1, [int]($size * 0.045))
$rect = New-Object System.Drawing.Rectangle($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
$radius = [int]($size * 0.22)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$d = $radius * 2
$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()

$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 0x3F, 0xA9, 0xF5),
    [System.Drawing.Color]::FromArgb(255, 0x0B, 0x4C, 0x8C),
    45.0)
$g.FillPath($brush, $path)

$fontSize = [float]($size * 0.50)
$font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'
$fmt.LineAlignment = 'Center'
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.DrawString('W', $font, $white, (New-Object System.Drawing.RectangleF(0, ($size * 0.02), $size, $size)), $fmt)

$g.Dispose()
$bmp.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "logo written: $outFile ($((Get-Item $outFile).Length) bytes)"
