@echo off
echo ========================================
echo WinCapture Build Script
echo ========================================
echo.

:: Check if dotnet is available
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

:: Show dotnet version
echo .NET SDK Version:
dotnet --version
echo.

:: Navigate to src directory
cd /d "%~dp0src"

:: Clean previous builds
echo Cleaning previous builds...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "..\publish" rmdir /s /q "..\publish"

:: Restore packages
echo.
echo Restoring packages...
dotnet restore
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore packages!
    pause
    exit /b 1
)

:: Build
echo.
echo Building...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

:: Publish single-file executable
echo.
echo Publishing portable executable...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "..\publish"
if %errorlevel% neq 0 (
    echo ERROR: Publish failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build successful!
echo.
echo Portable executable: publish\WinCapture.exe
echo ========================================
echo.

:: Show file size
for %%A in ("..\publish\WinCapture.exe") do echo File size: %%~zA bytes

echo.
pause
