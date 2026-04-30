# build-icon.ps1 — Rasterizes agent/PrintAgent/Resources/icon.svg into a multi-size icon.ico.
# Usage: ./build-icon.ps1
# Requires: ImageMagick (https://imagemagick.org/) on PATH (`magick` command).

$ErrorActionPreference = "Stop"

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw "ImageMagick not found on PATH. Install from https://imagemagick.org/ (Windows installer ships the `magick` CLI)."
}

$RepoRoot = Resolve-Path "$PSScriptRoot/.."
$SvgPath = Join-Path $RepoRoot "agent/PrintAgent/Resources/icon.svg"
$IcoPath = Join-Path $RepoRoot "agent/PrintAgent/Resources/icon.ico"

if (-not (Test-Path $SvgPath)) { throw "Source SVG not found: $SvgPath" }

$WorkDir = Join-Path $env:TEMP "printagent-icon-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

try {
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $pngs = @()

    foreach ($size in $sizes) {
        $png = Join-Path $WorkDir "icon-$size.png"
        Write-Host "Rasterizing $size x $size..."
        magick -background none -density 384 $SvgPath -resize "${size}x${size}" $png
        if ($LASTEXITCODE -ne 0) { throw "magick rasterize failed at size $size" }
        $pngs += $png
    }

    Write-Host "Assembling icon.ico..."
    magick @pngs $IcoPath
    if ($LASTEXITCODE -ne 0) { throw "magick ICO assembly failed" }

    $info = Get-Item $IcoPath
    Write-Host "Done: $IcoPath ($([math]::Round($info.Length / 1KB, 1)) KB)"
}
finally {
    Remove-Item -Path $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
}
