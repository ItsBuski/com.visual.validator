@echo off
setlocal enabledelayedexpansion

:: 1. PROJECT RESOLUTION
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\"
set "PROJECT_ROOT=%CD%"
popd

echo ====================================================
echo  VISUAL VALIDATOR PIPELINE
echo ====================================================
echo [INFO] Project Root: %PROJECT_ROOT%

:: 2. LOCATE UNITY
for /f "tokens=2" %%a in ('findstr /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do (
    set "UNITY_VERSION=%%a"
)

set "UNITY_EXE="
if exist "C:\Program Files\Unity\Hub\Editor\%UNITY_VERSION!\Editor\Unity.exe" set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\%UNITY_VERSION!\Editor\Unity.exe"
if not defined UNITY_EXE if exist "D:\Program Files\Unity\Hub\Editor\%UNITY_VERSION!\Editor\Unity.exe" set "UNITY_EXE=D:\Program Files\Unity\Hub\Editor\%UNITY_VERSION!\Editor\Unity.exe"

if not defined UNITY_EXE (echo ERROR: Unity %UNITY_VERSION% not found. && pause && exit /b 1)

:: 3. SCANNING
echo [1/3] Launching Batch Scan...
taskkill /f /im Unity.exe >nul 2>&1
"!UNITY_EXE!" -batchmode -projectPath "%PROJECT_ROOT%" -executeMethod AutomatedSceneScanner.RunHeadlessScan -logFile "%PROJECT_ROOT%\Logs\AutomationLog.txt" -quit

:: 4. ANALYSIS
echo [2/3] Running Python Analysis...
set "CAP_DIR=%PROJECT_ROOT%\ValidationCaptures"
python "%SCRIPT_DIR%analyze_captures.py" "%CAP_DIR%"

:: 5. RESTART
echo [3/3] Restoring Unity Editor...
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%"

echo ====================================================
echo  PROCESS COMPLETE
echo ====================================================
pause
exit /b 0