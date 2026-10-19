# Generates app.ico (multi-size) and docs/icon-preview.png for SsdTempHud / PanWen.
# Concept: dark rounded tile, cyan HUD bar on top, thermometer with warm bulb.

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$icoPath = Join-Path $root 'app.ico'
$previewPath = Join-Path $root 'docs\icon-preview.png'

function New-RoundedRectPath {
    param([single]$x, [single]$y, [single]$w, [single]$h, [single]$r)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $limit = [Math]::Min($w, $h) / 2.0
    if ($r -gt $limit) { $r = [single]$limit }
    if ($r -le 0.2) {
        $path.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h)))
        return $path
    }
    $d = [single]($r * 2.0)
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc([single]($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc([single]($x + $w - $d), [single]($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, [single]($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Icon {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [single]($size / 256.0)

    # Tile
    $margin = [single](10 * $s)
    $tileSize = [single](236 * $s)
    $tileRadius = [single](56 * $s)
    $tilePath = New-RoundedRectPath $margin $margin $tileSize $tileSize $tileRadius

    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF(0, [single]$size)),
        [System.Drawing.Color]::FromArgb(255, 0x18, 0x27, 0x40),
        [System.Drawing.Color]::FromArgb(255, 0x06, 0x0A, 0x11))
    $g.FillPath($bgBrush, $tilePath)

    if ($size -ge 32) {
        $borderPen = New-Object System.Drawing.Pen(
            [System.Drawing.Color]::FromArgb(56, 0xFF, 0xFF, 0xFF),
            [single][Math]::Max(1.0, 1.6 * $s))
        $g.DrawPath($borderPen, $tilePath)
        $borderPen.Dispose()
    }

    # HUD bar
    $barX = [single](40 * $s)
    $barY = [single](40 * $s)
    $barW = [single](176 * $s)
    $barH = [single][Math]::Max(1.6, 18 * $s)
    $barPath = New-RoundedRectPath $barX $barY $barW $barH ([single]($barH / 2.0))
    $barBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF($barX, 0)),
        (New-Object System.Drawing.PointF([single]($barX + $barW), 0)),
        [System.Drawing.Color]::FromArgb(255, 0x38, 0xBD, 0xF8),
        [System.Drawing.Color]::FromArgb(255, 0x22, 0xD3, 0xEE))
    $g.FillPath($barBrush, $barPath)

    # Thermometer stem
    $stemX = [single](112 * $s)
    $stemY = [single](74 * $s)
    $stemW = [single](32 * $s)
    $stemH = [single](112 * $s)
    $stemPath = New-RoundedRectPath $stemX $stemY $stemW $stemH ([single][Math]::Max(1.0, 16 * $s))
    $stemBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0xE8, 0xEE, 0xF6))
    $g.FillPath($stemBrush, $stemPath)

    # Mercury
    $merX = [single](120 * $s)
    $merY = [single](104 * $s)
    $merW = [single](16 * $s)
    $merH = [single](76 * $s)
    $merPath = New-RoundedRectPath $merX $merY $merW $merH ([single][Math]::Max(1.0, 8 * $s))
    $merBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, [single]($merY + $merH))),
        (New-Object System.Drawing.PointF(0, $merY)),
        [System.Drawing.Color]::FromArgb(255, 0xEF, 0x44, 0x44),
        [System.Drawing.Color]::FromArgb(255, 0xF5, 0x9E, 0x0B))
    $g.FillPath($merBrush, $merPath)

    # Bulb
    $bulbD = [single](64 * $s)
    $bulbX = [single](128 * $s - $bulbD / 2.0)
    $bulbY = [single](192 * $s - $bulbD / 2.0)
    $bulbBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, $bulbY)),
        (New-Object System.Drawing.PointF(0, [single]($bulbY + $bulbD))),
        [System.Drawing.Color]::FromArgb(255, 0xFB, 0x71, 0x85),
        [System.Drawing.Color]::FromArgb(255, 0xDC, 0x26, 0x26))
    $g.FillEllipse($bulbBrush, $bulbX, $bulbY, $bulbD, $bulbD)

    if ($size -ge 32) {
        $hlD = [single](14 * $s)
        $hlBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 0xFF, 0xFF, 0xFF))
        $g.FillEllipse($hlBrush, [single](116 * $s), [single](178 * $s), $hlD, $hlD)
        $hlBrush.Dispose()
    }

    $bgBrush.Dispose()
    $barBrush.Dispose()
    $stemBrush.Dispose()
    $merBrush.Dispose()
    $bulbBrush.Dispose()
    $tilePath.Dispose()
    $barPath.Dispose()
    $stemPath.Dispose()
    $merPath.Dispose()
    $g.Dispose()

    return $bmp
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @{}
$pngData = @{}

foreach ($size in $sizes) {
    $bmp = Draw-Icon $size
    $frames[$size] = $bmp
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$size] = $stream.ToArray()
    $stream.Dispose()
}

# Preview sheet
$previewW = 920
$previewH = 470
$preview = New-Object System.Drawing.Bitmap($previewW, $previewH)
$pg = [System.Drawing.Graphics]::FromImage($preview)
$pg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$pg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$pg.Clear([System.Drawing.Color]::FromArgb(255, 0x0B, 0x12, 0x20))

$lightBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0xF1, 0xF5, 0xF9))
$pg.FillRectangle($lightBrush, 0, 320, $previewW, 150)
$lightBrush.Dispose()

$pg.DrawImage($frames[256], 24, 28, 256, 256)
$pg.DrawImage($frames[128], 320, 90, 128, 128)
$pg.DrawImage($frames[64], 480, 122, 64, 64)
$pg.DrawImage($frames[32], 580, 138, 32, 32)
$pg.DrawImage($frames[16], 640, 146, 16, 16)

$pg.DrawImage($frames[64], 330, 352, 64, 64)
$pg.DrawImage($frames[32], 430, 368, 32, 32)
$pg.DrawImage($frames[16], 490, 376, 16, 16)

$font = New-Object System.Drawing.Font('Segoe UI', 10)
$labelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x8B, 0x93, 0xA7))
$pg.DrawString('256 px', $font, $labelBrush, 24, 292)
$pg.DrawString('128 px', $font, $labelBrush, 320, 224)
$pg.DrawString('64 px', $font, $labelBrush, 480, 190)
$pg.DrawString('32 px', $font, $labelBrush, 580, 176)
$pg.DrawString('16 px (tray)', $font, $labelBrush, 664, 146)
$pg.DrawString('light background', $font, $labelBrush, 330, 420)
$pg.Dispose()

$preview.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

# Assemble ICO with PNG-compressed frames
$file = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
foreach ($size in $sizes) {
    $dim = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$pngData[$size].Length)
    $writer.Write([UInt32]$offset)
    $offset += $pngData[$size].Length
}
foreach ($size in $sizes) {
    $writer.Write($pngData[$size])
}
$writer.Flush()
$writer.Close()
$file.Close()

foreach ($size in $sizes) {
    $frames[$size].Dispose()
}

Write-Host ('ICO: ' + $icoPath)
Write-Host ('Preview: ' + $previewPath)
Write-Host ('Bytes: ' + (Get-Item $icoPath).Length)
