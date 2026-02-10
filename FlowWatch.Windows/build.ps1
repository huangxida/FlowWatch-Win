param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot

Write-Host "=== FlowWatch Build Script ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"

# Restore NuGet packages
Write-Host "`n--- Restoring NuGet packages ---" -ForegroundColor Yellow
$slnPath = Join-Path $projectDir "FlowWatch.sln"
nuget restore $slnPath
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed" }

# Build
Write-Host "`n--- Building ---" -ForegroundColor Yellow
msbuild $slnPath /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Package
$binDir = Join-Path $projectDir "FlowWatch\bin\$Platform\$Configuration"
$zipName = "FlowWatch-$Version-win-x64.zip"
$zipPath = Join-Path $projectDir $zipName

Write-Host "`n--- Packaging ---" -ForegroundColor Yellow

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Collect files to package (exclude debug symbols and xml docs)
$filesToPackage = Get-ChildItem -Path $binDir -File | Where-Object {
    $_.Extension -notin @('.pdb', '.xml', '.config') -or $_.Name -eq 'FlowWatch.exe.config'
}

Compress-Archive -Path ($filesToPackage | ForEach-Object { $_.FullName }) -DestinationPath $zipPath -Force

Write-Host "`n=== Build complete ===" -ForegroundColor Green
Write-Host "Output: $zipPath"
Write-Host "Size: $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB"
