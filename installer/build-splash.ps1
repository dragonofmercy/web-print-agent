# build-splash.ps1 -- Rasterizes installer/splash.svg into splash.png for vpk --splashImage.
# Usage: ./build-splash.ps1
# Requires: ImageMagick (`magick` command on PATH).

$ErrorActionPreference = "Stop"

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw "ImageMagick not found on PATH. Install from https://imagemagick.org/."
}

$SvgPath = Join-Path $PSScriptRoot "splash.svg"
$PngPath = Join-Path $PSScriptRoot "splash.png"

if (-not (Test-Path $SvgPath)) { throw "Source SVG not found: $SvgPath" }

Write-Host "Rasterizing $SvgPath -> $PngPath..."
magick -background none -density 192 $SvgPath -resize 640x360 $PngPath
if ($LASTEXITCODE -ne 0) { throw "magick rasterize failed (exit $LASTEXITCODE)" }

$info = Get-Item $PngPath
Write-Host "Done: $PngPath ($([math]::Round($info.Length / 1KB, 1)) KB)"
