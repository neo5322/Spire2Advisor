@echo off
setlocal

:: Read version from csproj (single source of truth)
for /f "tokens=2 delims=<>" %%a in ('findstr "ModVersion" QuestceSpire\QuestceSpire.csproj') do set VERSION=%%a
set MOD_NAME=SpireAdvisor
set RELEASE_DIR=%~dp0release\%MOD_NAME%
set ZIP_NAME=%MOD_NAME%-v%VERSION%.zip

echo ================================================
echo   %MOD_NAME% v%VERSION% - Release Package
echo ================================================

:: Build first
echo [1/5] Building...
cd /d "%~dp0QuestceSpire"
call build.bat
if errorlevel 1 (
    echo BUILD FAILED!
    pause
    exit /b 1
)

:: Find game mods folder from local.props
if not exist local.props (
    echo ERROR: local.props not found. Copy local.props.example and configure paths.
    pause
    exit /b 1
)
for /f "tokens=*" %%a in ('findstr "STS2GamePath" local.props') do set GAME_LINE=%%a
:: Extract path (rough parse)
set MODS_DIR=
for /f "delims=<> tokens=2" %%a in ('findstr "STS2GamePath" local.props') do set GAME_PATH=%%a
set MODS_SRC=%GAME_PATH%\mods\%MOD_NAME%

echo [2/5] Collecting files from %MODS_SRC%...

:: Clean and create release dir
if exist "%~dp0release" rd /s /q "%~dp0release"
mkdir "%RELEASE_DIR%"

:: Copy DLL
copy "%MODS_SRC%\%MOD_NAME%.dll" "%RELEASE_DIR%\" >nul
:: Copy PCK
copy "%MODS_SRC%\%MOD_NAME%.pck" "%RELEASE_DIR%\" >nul
:: Copy mod manifest
copy "%MODS_SRC%\mod_manifest.json" "%RELEASE_DIR%\" >nul
:: Copy all NuGet/native DLLs
for %%f in ("%MODS_SRC%\*.dll") do (
    if /i not "%%~nxf"=="%MOD_NAME%.dll" copy "%%f" "%RELEASE_DIR%\" >nul
)
:: Copy Data
xcopy "%MODS_SRC%\Data" "%RELEASE_DIR%\Data\" /e /i /q >nul

:: Validate release artifacts
echo [3/5] Validating...
set VALID=1
if not exist "%RELEASE_DIR%\%MOD_NAME%.dll" (
    echo ERROR: Missing %MOD_NAME%.dll
    set VALID=0
)
if not exist "%RELEASE_DIR%\%MOD_NAME%.pck" (
    echo ERROR: Missing %MOD_NAME%.pck
    set VALID=0
)
if not exist "%RELEASE_DIR%\mod_manifest.json" (
    echo ERROR: Missing mod_manifest.json
    set VALID=0
)
if not exist "%RELEASE_DIR%\Data" (
    echo ERROR: Missing Data directory
    set VALID=0
)
if "%VALID%"=="0" (
    echo Release validation failed!
    pause
    exit /b 1
)
echo All required files present.

echo [4/5] Creating %ZIP_NAME%...
cd /d "%~dp0release"

:: Use PowerShell to zip
powershell -Command "Compress-Archive -Path '%MOD_NAME%' -DestinationPath '%~dp0%ZIP_NAME%' -Force"

echo [5/5] Done!
echo.
echo Release: %~dp0%ZIP_NAME%
echo.
echo Upload this to GitHub Releases.
echo.

dir "%~dp0%ZIP_NAME%"
pause
