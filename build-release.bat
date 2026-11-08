@echo off
setlocal

cd /d "%~dp0"

if exist "publish" rmdir /s /q "publish"

echo Building LiteSS WITH bundled .NET runtime...
dotnet publish LiteSS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "publish\build-with-runtime"
if errorlevel 1 goto :failed
move /y "publish\build-with-runtime\LiteSS.exe" "publish\LiteSS.exe" >nul
rmdir /s /q "publish\build-with-runtime"

echo.
echo Building LiteSS WITHOUT .NET runtime (requires .NET 8 Desktop Runtime)...
dotnet publish LiteSS.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "publish\build-without-runtime"
if errorlevel 1 goto :failed
move /y "publish\build-without-runtime\LiteSS.exe" "publish\LiteSS-nr.exe" >nul
rmdir /s /q "publish\build-without-runtime"

echo.
echo Release builds complete:
echo   %~dp0publish\LiteSS.exe      (with runtime)
echo   %~dp0publish\LiteSS-nr.exe   (no runtime)
exit /b 0

:failed
echo.
echo Release build failed.
exit /b 1
