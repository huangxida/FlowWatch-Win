param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot

Write-Host "=== FlowWatch Build Script ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"

$slnPath = Join-Path $projectDir "FlowWatch.sln"

if (-not $SkipBuild) {
    # Restore NuGet packages
    Write-Host "`n--- Restoring NuGet packages ---" -ForegroundColor Yellow
    $nuget = Join-Path $projectDir "nuget.exe"
    & $nuget restore $slnPath
    if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed" }

    # Build
    Write-Host "`n--- Building ---" -ForegroundColor Yellow
    $msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
    & $msbuild $slnPath /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} else {
    Write-Host "`n--- Skipping restore & build (SkipBuild) ---" -ForegroundColor Yellow
}

# Package
$binDir = Join-Path $projectDir "FlowWatch\bin\$Platform\$Configuration"
$zipName = "FlowWatch-$Version-win-x64.zip"
$zipPath = Join-Path $projectDir $zipName

Write-Host "`n--- Packaging ---" -ForegroundColor Yellow

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Copy files into a staging folder so ZIP contains a top-level "FlowWatch" directory
$stageDir = Join-Path $projectDir "_stage\FlowWatch"
if (Test-Path (Join-Path $projectDir "_stage")) { Remove-Item (Join-Path $projectDir "_stage") -Recurse -Force }
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

$filesToPackage = Get-ChildItem -Path $binDir -File | Where-Object {
    $_.Extension -notin @('.pdb', '.xml', '.config') -or $_.Name -eq 'FlowWatch.exe.config'
}
$filesToPackage | ForEach-Object { Copy-Item $_.FullName -Destination $stageDir }

Compress-Archive -Path (Join-Path $projectDir "_stage\FlowWatch") -DestinationPath $zipPath -Force

Remove-Item (Join-Path $projectDir "_stage") -Recurse -Force

Write-Host "`n=== Build complete ===" -ForegroundColor Green
Write-Host "Output: $zipPath"
Write-Host "Size: $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB"
