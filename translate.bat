@echo off
echo DeepLX Translation Tool
echo.

REM Check if Docker is running
docker ps >nul 2>&1
if errorlevel 1 (
    echo Docker is not running!
    echo Please start Docker Desktop first.
    pause
    exit /b 1
)

REM Check if docker-compose.yml exists
if not exist "docker-compose.deeplx.yml" (
    echo docker-compose.deeplx.yml not found!
    echo Starting with docker run instead...
    docker stop deeplx 2>nul
    docker rm deeplx 2>nul
    docker run -d --name deeplx -p 1188:1188 ghcr.io/owo-network/deeplx:latest
    if errorlevel 1 (
        echo Failed to start DeepLX!
        pause
        exit /b 1
    )
    echo Waiting for DeepLX to initialize...
    timeout /t 10 /nobreak >nul
    goto run_translation
)

REM Check if DeepLX is running
docker ps | findstr deeplx >nul
if errorlevel 1 (
    echo Starting DeepLX container...
    docker-compose -f docker-compose.deeplx.yml up -d
    
    if errorlevel 1 (
        echo Failed to start DeepLX with compose!
        echo Trying docker run instead...
        docker rm -f deeplx 2>nul
        docker run -d --name deeplx -p 1188:1188 ghcr.io/owo-network/deeplx:latest
    )
    
    echo Waiting for DeepLX to initialize...
    timeout /t 10 /nobreak >nul
)

:run_translation
echo DeepLX is running!
echo.

REM Run translation
python Scripts/Translations/translate_deeplx.py

if errorlevel 1 (
    echo.
    echo Translation failed!
    pause
    exit /b 1
)

echo.
echo Translation complete!
pause