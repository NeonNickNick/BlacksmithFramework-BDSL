@echo off
echo ============================================
echo   Building BDSL Validator (C# -^> exe)
echo ============================================
cd /d "%~dp0language-server"

echo.
echo [1/2] dotnet publish...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "../bin"

if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/2] Verify...
echo [skill](Shield)-^>{[check]()-^>{^<require^> Iron ^}^}^} | "%~dp0bin\bdsl-validator.exe"

echo.
echo ============================================
echo   Done! bin\bdsl-validator.exe is ready.
echo ============================================
if "%~1"=="" pause
