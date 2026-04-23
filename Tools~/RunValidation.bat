@echo off
setlocal enabledelayedexpansion

:: 1. PATH RESOLUTION
:: %~dp0 is the directory where this script lives
set "SCRIPT_DIR=%~dp0"

:: Move one level up to get to the Project Root
pushd "%SCRIPT_DIR%.."
set "PROJECT_ROOT=%CD%"
popd

echo ====================================================
echo  VISUAL VALIDATOR - AUTOMATION PIPELINE
echo ====================================================
echo [INFO] Script Path: %SCRIPT_DIR%
echo [INFO] Project Root: %PROJECT_ROOT%

:: 2. PROJECT VALIDATION
if not exist "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt" (
    echo [ERROR] ProjectSettings not found at: %PROJECT_ROOT%
    echo Please ensure this script is inside a subfolder of your Unity Project.
    pause
    exit /b 1
)

:: 3. LOCATE UNITY VERSION
for /f "tokens=2" %%a in ('findstr /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do (
    set "UNITY_VERSION=%%a"
)

set "UNITY_EXE="
:: Search in common drives
for %%d in (C D E F) do (
    if not defined UNITY_EXE (
        set "CHECK=%%d:\Program Files\Unity\Hub\Editor\!UNITY_VERSION!\Editor\Unity.exe"
        if exist "!CHECK!" set "UNITY_EXE=!CHECK!"
    )
)

if not defined UNITY_EXE (
    echo [ERROR] Unity !UNITY_VERSION! not found in Program Files.
    pause
    exit /b 1
)

:: 4. SCANNING PHASE
echo [1/3] Launching Batch Scan...
taskkill /f /im Unity.exe >nul 2>&1
"!UNITY_EXE!" -batchmode -projectPath "%PROJECT_ROOT%" -executeMethod VisualValidator.Editor.AutomatedSceneScanner.RunHeadlessScan -logFile "%PROJECT_ROOT%\Logs\AutomationLog.txt" -quit

:: 5. ANALYSIS PHASE
echo [2/3] Running Python Image Analysis...
set "CAP_DIR=%PROJECT_ROOT%\ValidationCaptures"
python "%SCRIPT_DIR%analyze_captures.py" "%CAP_DIR%"

:: 6. RESTORATION PHASE
echo [3/3] Restarting Unity Editor...
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%"

echo ====================================================
echo  PROCESS COMPLETE
echo ====================================================
pause
exit /b 0