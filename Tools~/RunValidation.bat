@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "TARGET_METHOD=%~1"

if "%TARGET_METHOD%"=="" (
    echo [ERROR] No execution method provided.
    pause
    exit /b 1
)

pushd "%SCRIPT_DIR%.."
set "PROJECT_ROOT=%CD%"
popd

echo ====================================================
echo  VISUAL VALIDATOR - AUTOMATION PIPELINE
echo ====================================================
echo [INFO] Project Root: %PROJECT_ROOT%
echo [INFO] Target Method: %TARGET_METHOD%

if exist "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt" goto :project_ok
echo [ERROR] ProjectSettings not found at: %PROJECT_ROOT%
pause
exit /b 1

:project_ok
for /f "tokens=2" %%a in ('findstr /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do (
    set "UNITY_VERSION=%%a"
)

set "UNITY_EXE="
for %%d in (C D E F) do (
    if not defined UNITY_EXE (
        set "CHECK=%%d:\Program Files\Unity\Hub\Editor\!UNITY_VERSION!\Editor\Unity.exe"
        if exist "!CHECK!" set "UNITY_EXE=!CHECK!"
    )
)

if defined UNITY_EXE goto :unity_found
echo [ERROR] Unity !UNITY_VERSION! not found.
pause
exit /b 1

:unity_found
echo [1/3] Launching Batch Scan...
taskkill /f /im Unity.exe >nul 2>&1
"!UNITY_EXE!" -batchmode -projectPath "%PROJECT_ROOT%" -executeMethod %TARGET_METHOD% -logFile "%PROJECT_ROOT%\Logs\AutomationLog.txt" -quit

echo [2/3] Running Python Image Analysis...
set "CAP_DIR=%PROJECT_ROOT%\ValidationCaptures"
python "%SCRIPT_DIR%analyze_captures.py" "%CAP_DIR%"

echo [3/3] Restarting Unity Editor...
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%"
exit /b 0