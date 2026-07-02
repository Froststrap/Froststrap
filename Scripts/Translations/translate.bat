@echo off
echo DeepLX Translation Tool
echo.

REM Change to the script directory
cd /d "%~dp0"

REM Check if Docker is running
docker ps >nul 2>&1
if errorlevel 1 (
    echo Docker is not running!
    echo Please start Docker Desktop first.
    pause
    exit /b 1
)

REM Run translation (will auto-start DeepLX if needed)
python translate_deeplx.py

if errorlevel 1 (
    echo.
    echo Translation failed!
    pause
    exit /b 1
)

echo.
echo Translation complete!
pause