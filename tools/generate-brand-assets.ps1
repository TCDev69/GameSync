<#
.SYNOPSIS
    Renders the GameSync logo into every raster asset the app, installer and docs need.

.DESCRIPTION
    The vector master lives in docs/assets/gamesync-logo.svg. This script redraws the same
    geometry with GDI+ so the repository can regenerate PNG/ICO assets without external
    tooling (ImageMagick / Inkscape are not required).

    Run after changing the logo design:
        pwsh -File tools/generate-brand-assets.ps1
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$AssetsDir = Join-Path $RepositoryRoot 'src/GameSync.App/Assets'
$DocsAssetsDir = Join-Path $RepositoryRoot 'docs/assets'

function New-RoundedRectPath {
    param(
        [single]$X, [single]$Y, [single]$Width, [single]$Height, [single]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Add-Logo {
    param(
        [System.Drawing.Graphics]$Graphics,
        [single]$Size,
        [switch]$WithPlate
    )

    # Design space is 512x512; every coordinate below is scaled by $f.
    $f = $Size / 512.0
    $scale = { param([single]$v) return [single]($v * $f) }

    $syncRect = New-Object System.Drawing.RectangleF(
        (& $scale 106), (& $scale 106), (& $scale 300), (& $scale 300))
    $syncBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $syncRect,
        [System.Drawing.ColorTranslator]::FromHtml('#63E6FF'),
        [System.Drawing.ColorTranslator]::FromHtml('#4B79FF'),
        45.0)

    if ($WithPlate) {
        $plateRect = New-Object System.Drawing.RectangleF(
            (& $scale 24), (& $scale 24), (& $scale 464), (& $scale 464))
        $plateBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $plateRect,
            [System.Drawing.ColorTranslator]::FromHtml('#1E2436'),
            [System.Drawing.ColorTranslator]::FromHtml('#0B0F18'),
            45.0)
        $platePath = New-RoundedRectPath -X $plateRect.X -Y $plateRect.Y `
            -Width $plateRect.Width -Height $plateRect.Height -Radius (& $scale 112)
        $Graphics.FillPath($plateBrush, $platePath)
        $platePath.Dispose()
        $plateBrush.Dispose()
    }

    # Two 140 degree arcs of the r=150 ring centred at (256,256).
    $pen = New-Object System.Drawing.Pen($syncBrush, (& $scale 34))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $Graphics.DrawArc($pen, $syncRect, 200, 140)
    $Graphics.DrawArc($pen, $syncRect, 20, 140)
    $pen.Dispose()

    # Arrow heads sit tangent to each arc end point.
    $heads = @(
        @(408.58, 236.65, 371.58, 213.93, 422.32, 195.47),
        @(103.42, 275.35, 140.42, 298.07, 89.68, 316.53)
    )
    foreach ($h in $heads) {
        $points = @(
            (New-Object System.Drawing.PointF((& $scale $h[0]), (& $scale $h[1]))),
            (New-Object System.Drawing.PointF((& $scale $h[2]), (& $scale $h[3]))),
            (New-Object System.Drawing.PointF((& $scale $h[4]), (& $scale $h[5])))
        )
        $Graphics.FillPolygon($syncBrush, $points)
    }
    $syncBrush.Dispose()

    # Gamepad silhouette.
    $bodyBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.ColorTranslator]::FromHtml('#C6D4FF'))
    $bodyPath = New-RoundedRectPath -X (& $scale 176) -Y (& $scale 222) `
        -Width (& $scale 160) -Height (& $scale 84) -Radius (& $scale 34)
    $Graphics.FillPath($bodyBrush, $bodyPath)
    $bodyPath.Dispose()
    $bodyBrush.Dispose()

    $detailBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.ColorTranslator]::FromHtml('#22325A'))

    $dpadH = New-RoundedRectPath -X (& $scale 197) -Y (& $scale 258.5) `
        -Width (& $scale 34) -Height (& $scale 11) -Radius (& $scale 5.5)
    $Graphics.FillPath($detailBrush, $dpadH)
    $dpadH.Dispose()

    $dpadV = New-RoundedRectPath -X (& $scale 208.5) -Y (& $scale 247) `
        -Width (& $scale 11) -Height (& $scale 34) -Radius (& $scale 5.5)
    $Graphics.FillPath($detailBrush, $dpadV)
    $dpadV.Dispose()

    foreach ($button in @(@(298, 252), @(318, 274))) {
        $r = & $scale 9
        $Graphics.FillEllipse(
            $detailBrush,
            (& $scale $button[0]) - $r,
            (& $scale $button[1]) - $r,
            $r * 2,
            $r * 2)
    }
    $detailBrush.Dispose()
}

function New-LogoBitmap {
    param(
        [int]$Width,
        [int]$Height,
        [switch]$WithPlate,
        [single]$Coverage = 1.0
    )

    $bitmap = New-Object System.Drawing.Bitmap(
        $Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $size = [single]([Math]::Min($Width, $Height) * $Coverage)
    $graphics.TranslateTransform(($Width - $size) / 2.0, ($Height - $size) / 2.0)
    Add-Logo -Graphics $graphics -Size $size -WithPlate:$WithPlate
    $graphics.Dispose()

    return $bitmap
}

function Save-Png {
    param([System.Drawing.Bitmap]$Bitmap, [string]$Path)

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "  wrote $([IO.Path]::GetFileName($Path)) ($($Bitmap.Width)x$($Bitmap.Height))"
}

function Get-BgraRows {
    param([System.Drawing.Bitmap]$Bitmap)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $Bitmap.Width, $Bitmap.Height)
    $data = $Bitmap.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $buffer = New-Object byte[] ($data.Stride * $Bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
        return @{ Buffer = $buffer; Stride = $data.Stride }
    }
    finally {
        $Bitmap.UnlockBits($data)
    }
}

function Save-Ico {
    param([string]$Path, [int[]]$Sizes)

    # Uncompressed 32bpp DIB entries keep the icon readable by the .NET SDK resource
    # writer and Inno Setup, which both reject some PNG-compressed .ico files.
    $images = @()
    foreach ($size in $Sizes) {
        $bitmap = New-LogoBitmap -Width $size -Height $size -WithPlate
        $pixels = Get-BgraRows -Bitmap $bitmap
        $stride = $pixels.Stride
        $buffer = $pixels.Buffer

        $xor = New-Object byte[] ($size * $size * 4)
        for ($y = 0; $y -lt $size; $y++) {
            $sourceOffset = ($size - 1 - $y) * $stride
            [Array]::Copy($buffer, $sourceOffset, $xor, $y * $size * 4, $size * 4)
        }

        $maskStride = [Math]::Ceiling($size / 32.0) * 4
        $mask = New-Object byte[] ($maskStride * $size)

        $stream = New-Object System.IO.MemoryStream
        $writer = New-Object System.IO.BinaryWriter($stream)
        $writer.Write([uint32]40)
        $writer.Write([int32]$size)
        $writer.Write([int32]($size * 2))
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]0)
        $writer.Write([uint32]($xor.Length + $mask.Length))
        $writer.Write([int32]0)
        $writer.Write([int32]0)
        $writer.Write([uint32]0)
        $writer.Write([uint32]0)
        $writer.Write($xor)
        $writer.Write($mask)
        $writer.Flush()

        $images += @{ Size = $size; Data = $stream.ToArray() }
        $writer.Dispose()
        $stream.Dispose()
        $bitmap.Dispose()
    }

    $output = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($output)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    $offset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Data.Length
    }

    foreach ($image in $images) {
        $writer.Write($image.Data)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $output.ToArray())
    $writer.Dispose()
    $output.Dispose()
    Write-Host "  wrote $([IO.Path]::GetFileName($Path)) ($($images.Count) sizes)"
}

Write-Host 'Generating GameSync brand assets...'

$plated = @{
    'StoreLogo.png'                    = @(50, 50)
    'LockScreenLogo.scale-200.png'     = @(48, 48)
    'Square44x44Logo.scale-200.png'    = @(88, 88)
    'Square150x150Logo.scale-200.png'  = @(300, 300)
}

foreach ($entry in $plated.GetEnumerator()) {
    $bitmap = New-LogoBitmap -Width $entry.Value[0] -Height $entry.Value[1] -WithPlate
    Save-Png -Bitmap $bitmap -Path (Join-Path $AssetsDir $entry.Key)
    $bitmap.Dispose()
}

$unplated = @{
    'Square44x44Logo.targetsize-24_altform-unplated.png'      = @(24, 24)
    'Square44x44Logo.targetsize-48_altform-lightunplated.png' = @(48, 48)
}

foreach ($entry in $unplated.GetEnumerator()) {
    $bitmap = New-LogoBitmap -Width $entry.Value[0] -Height $entry.Value[1]
    Save-Png -Bitmap $bitmap -Path (Join-Path $AssetsDir $entry.Key)
    $bitmap.Dispose()
}

# Wide tile and splash screen keep the logo centred with breathing room.
$wide = New-LogoBitmap -Width 620 -Height 300 -WithPlate -Coverage 0.86
Save-Png -Bitmap $wide -Path (Join-Path $AssetsDir 'Wide310x150Logo.scale-200.png')
$wide.Dispose()

$splash = New-LogoBitmap -Width 1240 -Height 600 -WithPlate -Coverage 0.62
Save-Png -Bitmap $splash -Path (Join-Path $AssetsDir 'SplashScreen.scale-200.png')
$splash.Dispose()

# In-app logo used by the shell header and onboarding.
$appLogo = New-LogoBitmap -Width 256 -Height 256 -WithPlate
Save-Png -Bitmap $appLogo -Path (Join-Path $AssetsDir 'GameSyncLogo.png')
$appLogo.Dispose()

Save-Ico -Path (Join-Path $AssetsDir 'AppIcon.ico') -Sizes @(16, 24, 32, 48, 64, 128, 256)

# Documentation / README artwork.
$docsLogo = New-LogoBitmap -Width 512 -Height 512 -WithPlate
Save-Png -Bitmap $docsLogo -Path (Join-Path $DocsAssetsDir 'gamesync-logo.png')
$docsLogo.Dispose()

Copy-Item `
    -Path (Join-Path $DocsAssetsDir 'gamesync-logo.svg') `
    -Destination (Join-Path $AssetsDir 'GameSyncLogo.svg') `
    -Force

Write-Host 'Done.'
