<#
.SYNOPSIS
Render the vector master into a multi-resolution Windows application icon.
.DESCRIPTION
Uses Windows WPF and the single, unfilled SVG path in monitor.svg; no external
renderer is required. Each size is rendered independently. Small frames use
32-bit BGRA DIBs with transparency masks, and the 256px frame uses PNG.
.EXAMPLE
powershell.exe -NoProfile -STA -File tools\Generate-ApplicationIcon.ps1
#>
[CmdletBinding()]
param(
    [string]$SourcePath,
    [string]$OutputPath,
    [string]$PreviewDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptDirectory = [IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
if (-not $SourcePath) { $SourcePath = Join-Path $scriptDirectory '..\MonitorEdgeTunnelApp\Resources\monitor.svg' }
if (-not $OutputPath) { $OutputPath = Join-Path $scriptDirectory '..\MonitorEdgeTunnelApp\Resources\monitor.ico' }
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run this script with powershell.exe -STA.'
}
Add-Type -AssemblyName WindowsBase, PresentationCore
[xml]$source = [IO.File]::ReadAllText([IO.Path]::GetFullPath($SourcePath))
$paths = $source.SelectNodes("/*[local-name()='svg']/*[local-name()='path']")
if ($paths.Count -ne 1 -or $paths[0].GetAttribute('fill') -ne 'none') {
    throw 'The vector master must contain one unfilled SVG path.'
}
$viewBox = $source.DocumentElement.GetAttribute('viewBox') -split '\s+'
if ($viewBox.Count -ne 4 -or $viewBox[0] -ne '0' -or $viewBox[1] -ne '0' -or $viewBox[2] -ne $viewBox[3]) {
    throw 'The vector master must have a square viewBox starting at the origin.'
}
$culture = [Globalization.CultureInfo]::InvariantCulture
$extent = [double]::Parse($viewBox[2], $culture)
$geometry = [Windows.Media.Geometry]::Parse($paths[0].GetAttribute('d'))
$brush = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($paths[0].GetAttribute('stroke')))
$pen = [Windows.Media.Pen]::new($brush, [double]::Parse($paths[0].GetAttribute('stroke-width'), $culture))
$pen.StartLineCap = [Enum]::Parse([Windows.Media.PenLineCap], $paths[0].GetAttribute('stroke-linecap'), $true)
$pen.EndLineCap = $pen.StartLineCap
$pen.LineJoin = [Enum]::Parse([Windows.Media.PenLineJoin], $paths[0].GetAttribute('stroke-linejoin'), $true)
$geometry.Freeze()
$pen.Freeze()
if ($PreviewDirectory) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($PreviewDirectory))
}

# Cover the common notification-area, taskbar and Explorer DPI sizes.
$sizes = @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 128, 256)
$frames = [Collections.Generic.List[object]]::new()
foreach ($size in $sizes) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size / $extent, $size / $extent))
        $drawing.DrawGeometry($null, $pen, $geometry)
        $drawing.Pop()
    }
    finally { $drawing.Close() }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $png = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $pngStream = [IO.MemoryStream]::new()
    try {
        $png.Save($pngStream)
        $pngData = $pngStream.ToArray()
    }
    finally { $pngStream.Dispose() }
    if ($PreviewDirectory) {
        [IO.File]::WriteAllBytes((Join-Path $PreviewDirectory ('monitor-' + $size + '.png')), $pngData)
    }
    $frameData = $pngData
    if ($size -lt 256) {
        # ICO DIBs use bottom-up straight-alpha BGRA rows followed by an AND mask.
        $straight = [Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
        $stride = $size * 4
        $pixels = [byte[]]::new($stride * $size)
        $straight.CopyPixels($pixels, $stride, 0)
        $maskStride = [int]([Math]::Floor(($size + 31) / 32) * 4)
        $mask = [byte[]]::new($maskStride * $size)
        for ($y = 0; $y -lt $size; $y++) {
            for ($x = 0; $x -lt $size; $x++) {
                if ($pixels[$y * $stride + $x * 4 + 3] -eq 0) {
                    $index = ($size - 1 - $y) * $maskStride + [int][Math]::Floor($x / 8)
                    $mask[$index] = $mask[$index] -bor (128 -shr ($x % 8))
                }
            }
        }
        $dibStream = [IO.MemoryStream]::new()
        $dib = [IO.BinaryWriter]::new($dibStream)
        try {
            $dib.Write([int]40)
            $dib.Write([int]$size)
            $dib.Write([int]($size * 2))
            $dib.Write([uint16]1)
            $dib.Write([uint16]32)
            $dib.Write([int]0)
            $dib.Write([int]$pixels.Length)
            for ($field = 0; $field -lt 4; $field++) { $dib.Write([int]0) }
            for ($y = $size - 1; $y -ge 0; $y--) { $dib.Write($pixels, $y * $stride, $stride) }
            $dib.Write($mask)
            $dib.Flush()
            $frameData = $dibStream.ToArray()
        }
        finally { $dib.Dispose() }
    }
    $frames.Add(@{ Size = $size; Data = $frameData })
}

# Keep the existing resource path so EXE, shortcuts and the tray share one ICO.
$iconStream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
    $writer.Flush()
    [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputPath), $iconStream.ToArray())
}
finally { $writer.Dispose() }
Write-Output ('Generated ' + $OutputPath + ' with ' + $frames.Count + ' sizes: ' + ($sizes -join ', '))
