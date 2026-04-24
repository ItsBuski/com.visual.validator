@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "TARGET_METHOD=%~1"

if "%TARGET_METHOD%"=="" set "TARGET_METHOD=VisualValidator.Editor.AutomatedSceneScanner.RunHDRPScan"

pushd "%SCRIPT_DIR%.."
set "PROJECT_ROOT=%CD%"
popd

set "LOG_FILE=%PROJECT_ROOT%\Logs\AutomationLog.txt"
if exist "%LOG_FILE%" del "%LOG_FILE%"

echo ====================================================
echo  VISUAL VALIDATOR - AUTOMATION PIPELINE (WINDOWED)
echo ====================================================

for /f "tokens=2" %%a in ('findstr /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do set "UNITY_VERSION=%%a"
set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\!UNITY_VERSION!\Editor\Unity.exe"

taskkill /f /im Unity.exe >nul 2>&1

echo [1/3] Launching Validation Scan (%TARGET_METHOD%)...

:: AQUI ESTA LA CLAVE: Ejecutamos sin -batchmode.
"!UNITY_EXE!" -projectPath "%PROJECT_ROOT%" -executeMethod %TARGET_METHOD% -logFile "%LOG_FILE%"

echo [2/3] Running Python Image Analysis...
python "%SCRIPT_DIR%analyze_captures.py" "%PROJECT_ROOT%\ValidationCaptures"

echo [3/3] Restarting Unity Editor...
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%"
exit /b 0