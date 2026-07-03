@echo off
echo ============================================
echo   Publish Blacksmith DSL Editor
echo ============================================

cd /d "%~dp0"

echo.
echo [1/3] Building C# validator...
call build-validator.cmd --no-pause
if %ERRORLEVEL% neq 0 (
    echo ERROR: Validator build failed!
    pause
    exit /b %ERRORLEVEL%
)

REM build-validator.cmd cd's into language-server; restore working dir
cd /d "%~dp0"

echo.
echo [2/3] Installing vsce...
call npm install -g @vscode/vsce
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to install vsce!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [3/3] Packaging VSIX...
call vsce package
if %ERRORLEVEL% neq 0 (
    echo ERROR: Package failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ============================================
echo   Done! Output: *.vsix
echo ============================================

echo.
echo To install locally:
echo   code --install-extension blacksmith-dsl-editor-*.vsix
echo.
echo To publish to marketplace:
echo   vsce publish
echo.
pause
