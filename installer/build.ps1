# build.ps1 -- Build the Velopack installer for PrintAgent.
# Usage (from the installer/ directory or the repo root):
#   ./installer/build.ps1
#   ./installer/build.ps1 -Version 0.2.0
#
# Prerequisites:
#   - .NET 8 SDK
#   - SumatraPDF.exe placed at agent/PrintAgent/Resources/SumatraPDF.exe
#   - vpk dotnet tool (auto-installed globally if missing)

param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$RepoRoot    = Resolve-Path "$PSScriptRoot/.."
$AgentDir    = Join-Path $RepoRoot "agent"
$ProjectFile = Join-Path $AgentDir "PrintAgent/PrintAgent.csproj"
$PublishDir  = Join-Path $AgentDir "PrintAgent/bin/Release/net8.0-windows/win-x64/publish"
$IconFile    = Join-Path $AgentDir "PrintAgent/Resources/icon.ico"
$OutputDir   = Join-Path $PSScriptRoot "Output"

Write-Host "==> Publishing PrintAgent $Version..."
dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    -p:PublishSingleFile=true `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "==> vpk not found -- installing dotnet tool globally..."
    dotnet tool install -g vpk
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool install vpk failed (exit $LASTEXITCODE)" }
}

Write-Host "==> Packing with Velopack..."
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

vpk pack `
    --packId PrintAgent `
    --packVersion $Version `
    --packTitle "Web Print Agent" `
    --packAuthors "DragonOfMercy" `
    --packDir $PublishDir `
    --mainExe PrintAgent.exe `
    --icon $IconFile `
    --outputDir $OutputDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack failed (exit $LASTEXITCODE)" }

Write-Host ""
Write-Host "Done. Installer output: $OutputDir"
Write-Host "  PrintAgentSetup.exe  -- run on the target machine to install"
Write-Host "  RELEASES             -- Velopack release feed (for auto-update)"
Write-Host "  *.nupkg              -- delta-update package"
