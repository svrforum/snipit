# WinCapture Build Script (PowerShell)
# Run: .\build.ps1

Write-Host "========================================"
Write-Host "WinCapture Build Script"
Write-Host "========================================"
Write-Host ""

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK Version: $dotnetVersion"
} catch {
    Write-Host "ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK from https://dotnet.microsoft.com/download"
    exit 1
}

# Set working directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $scriptDir "src"
$publishDir = Join-Path $scriptDir "publish"

# Clean previous builds
Write-Host ""
Write-Host "Cleaning previous builds..."
if (Test-Path (Join-Path $srcDir "bin")) { Remove-Item -Recurse -Force (Join-Path $srcDir "bin") }
if (Test-Path (Join-Path $srcDir "obj")) { Remove-Item -Recurse -Force (Join-Path $srcDir "obj") }
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

# Restore packages
Write-Host ""
Write-Host "Restoring packages..."
Set-Location $srcDir
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore packages!" -ForegroundColor Red
    exit 1
}

# Build
Write-Host ""
Write-Host "Building..."
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

# Publish single-file executable
Write-Host ""
Write-Host "Publishing portable executable..."
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit 1
}

# Success
Write-Host ""
Write-Host "========================================"
Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""
$exePath = Join-Path $publishDir "WinCapture.exe"
$fileInfo = Get-Item $exePath
Write-Host "Portable executable: $exePath"
Write-Host "File size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
Write-Host "========================================"

Set-Location $scriptDir
