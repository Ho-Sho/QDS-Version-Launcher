@echo off
setlocal
cd /d "%~dp0"

echo Building portable, self-contained QDSVersionLauncher.exe ...
dotnet publish QDSVersionLauncher.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o publish

if errorlevel 1 (
    echo.
    echo Build failed. Make sure the .NET 8 SDK is installed.
    pause
    exit /b 1
)

echo.
echo Done. Portable EXE is at: publish\QDSVersionLauncher.exe
echo Copy that single file anywhere (USB stick, network share, etc.) - no installer needed.
pause
