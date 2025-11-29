# PakViewer Publish Script
# Version format: 1.YY.MMDDHHMM (e.g., 1.24.11291530)

param(
    [switch]$NoZip,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# Generate version
$now = Get-Date
$version = "1.{0:yy}.{0:MMddHHmm}" -f $now
Write-Host "Publishing PakViewer v$version" -ForegroundColor Cyan

# Paths
$projectDir = $PSScriptRoot
$projectFile = Join-Path $projectDir "PakViewer.csproj"
$publishDir = Join-Path $projectDir "publish"
$outputDir = Join-Path $publishDir "PakViewer-v$version"
$zipFile = Join-Path $publishDir "PakViewer-v$version.zip"

# Clean previous builds
if ($Clean -or (Test-Path $publishDir)) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }
}

# Create publish directory
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Update version in csproj (optional - creates AssemblyVersion)
Write-Host "Building with version $version..." -ForegroundColor Yellow

# Publish for Windows (platform-dependent, smaller size)
$publishArgs = @(
    "publish",
    $projectFile,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "false",
    "-p:PublishSingleFile=false",
    "-p:Version=$version",
    "-p:AssemblyVersion=$version.0",
    "-p:FileVersion=$version.0",
    "-o", $outputDir
)

Write-Host "Running: dotnet $($publishArgs -join ' ')" -ForegroundColor Gray
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Remove unnecessary files
$removePatterns = @("*.pdb", "*.xml", "*.deps.json")
foreach ($pattern in $removePatterns) {
    Get-ChildItem -Path $outputDir -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force
}

# Rename exe to include version
$oldExe = Join-Path $outputDir "PakViewer.exe"
$newExe = Join-Path $outputDir "PakViewer-v$version.exe"
if (Test-Path $oldExe) {
    Rename-Item -Path $oldExe -NewName "PakViewer-v$version.exe"
    Write-Host "Renamed: PakViewer.exe -> PakViewer-v$version.exe" -ForegroundColor Gray
}

# Count files
$fileCount = (Get-ChildItem -Path $outputDir -File).Count
$totalSize = (Get-ChildItem -Path $outputDir -File | Measure-Object -Property Length -Sum).Sum / 1MB

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "  Output: $outputDir" -ForegroundColor White
Write-Host "  Files: $fileCount" -ForegroundColor White
Write-Host "  Size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor White

# Create ZIP
if (-not $NoZip) {
    Write-Host ""
    Write-Host "Creating ZIP archive..." -ForegroundColor Yellow

    if (Test-Path $zipFile) {
        Remove-Item -Force $zipFile
    }

    Compress-Archive -Path "$outputDir\*" -DestinationPath $zipFile -CompressionLevel Optimal

    $zipSize = (Get-Item $zipFile).Length / 1MB
    Write-Host "  ZIP: $zipFile" -ForegroundColor White
    Write-Host "  ZIP Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Published PakViewer v$version ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output files:" -ForegroundColor White
Write-Host "  Folder: $outputDir" -ForegroundColor Gray
if (-not $NoZip) {
    Write-Host "  ZIP:    $zipFile" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Requirements: .NET 7.0 Runtime (Windows)" -ForegroundColor Yellow
Write-Host "Download: https://dotnet.microsoft.com/download/dotnet/7.0" -ForegroundColor Yellow
