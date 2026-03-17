@echo off
setlocal

set VERSION=0.8.0
set MOD_NAME=SpireAdvisor
set RELEASE_DIR=%~dp0release\%MOD_NAME%
set ZIP_NAME=%MOD_NAME%-v%VERSION%.zip

echo ================================================
echo   %MOD_NAME% v%VERSION% - Release Package
echo ================================================

:: Build first
echo [1/4] Building...
cd /d "%~dp0QuestceSpire"
call build.bat
if errorlevel 1 (
    echo BUILD FAILED!
    pause
    exit /b 1
)

:: Find game mods folder from local.props
for /f "tokens=*" %%a in ('findstr "STS2GamePath" local.props') do set GAME_LINE=%%a
:: Extract path (rough parse)
set MODS_DIR=
for /f "delims=<> tokens=2" %%a in ('findstr "STS2GamePath" local.props') do set GAME_PATH=%%a
set MODS_SRC=%GAME_PATH%\mods\%MOD_NAME%

echo [2/4] Collecting files from %MODS_SRC%...

:: Clean and create release dir
if exist "%~dp0release" rd /s /q "%~dp0release"
mkdir "%RELEASE_DIR%"

:: Copy DLL
copy "%MODS_SRC%\%MOD_NAME%.dll" "%RELEASE_DIR%\" >nul
:: Copy PCK
copy "%MODS_SRC%\%MOD_NAME%.pck" "%RELEASE_DIR%\" >nul
:: Copy all NuGet/native DLLs
for %%f in ("%MODS_SRC%\*.dll") do (
    if /i not "%%~nxf"=="%MOD_NAME%.dll" copy "%%f" "%RELEASE_DIR%\" >nul
)
:: Copy Data
xcopy "%MODS_SRC%\Data" "%RELEASE_DIR%\Data\" /e /i /q >nul

echo [3/4] Creating %ZIP_NAME%...
cd /d "%~dp0release"

:: Use PowerShell to zip
powershell -Command "Compress-Archive -Path '%MOD_NAME%' -DestinationPath '%~dp0%ZIP_NAME%' -Force"

echo [4/4] Done!
echo.
echo Release: %~dp0%ZIP_NAME%
echo.
echo Upload this to GitHub Releases.
echo.

dir "%~dp0%ZIP_NAME%"
pause
