@echo off
title SpireAdvisor Build
echo ================================================
echo   Spire Advisor - Build
echo ================================================
echo.
cd /d "%~dp0"
if not exist "local.props" (
    echo [ERROR] local.props not found.
    echo Copy local.props.example to local.props and edit paths.
    cmd /k
    exit /b 1
)
echo [INFO] Building...
echo.
dotnet build --configuration Release
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.
) else (
    echo.
    echo [OK] Deployed to game mods folder.
    echo Start STS2 to test.
)
echo.
cmd /k echo Done.
