@echo off
setlocal enabledelayedexpansion

:: 1. PATH RESOLUTION
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\"
set "PROJECT_ROOT=%CD%"
popd

:: Re-resolve if running from ProjectRoot/ValidationTools/
if exist "%PROJECT_ROOT%\ValidationTools" (
    pushd "%SCRIPT_DIR%..\"
    set "PROJECT_ROOT=%CD%"
    popd
)

echo ====================================================
echo  VISUAL VALIDATOR - AUTOMATION PIPELINE
echo ====================================================
echo [INFO] Project: %PROJECT_ROOT%

:: 2. LOCATE UNITY
for /f "tokens=2" %%a in ('findstr /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do (
    set "UNITY_VERSION=%%a"
)

set "UNITY_EXE="
for %%d in (C D E F G) do (
    if not defined UNITY_EXE (
        set "CHECK=%%d:\Program Files\Unity\Hub\Editor\!UNITY_VERSION!\Editor\Unity.exe"
        if exist "!CHECK!" set "UNITY_EXE=!CHECK!"
    )
)

if not defined UNITY_EXE (echo ERROR: Unity !UNITY_VERSION! not found. && pause && exit /b 1)

:: 3. SCANNING PHASE
echo [1/3] Running Headless Scan...
taskkill /f /im Unity.exe >nul 2>&1
"!UNITY_EXE!" -batchmode -projectPath "%PROJECT_ROOT%" -executeMethod VisualValidator.Editor.AutomatedSceneScanner.RunHeadlessScan -logFile "%PROJECT_ROOT%\Logs\AutomationLog.txt" -quit

:: 4. ANALYSIS PHASE
echo [2/3] Running Python Image Analysis...
set "CAP_DIR=%PROJECT_ROOT%\ValidationCaptures"
python "%SCRIPT_DIR%analyze_captures.py" "%CAP_DIR%"

:: 5. RESTORATION PHASE
echo [3/3] Restarting Unity Editor...
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%"

echo ====================================================
echo  PIPELINE FINISHED
echo ====================================================
pause