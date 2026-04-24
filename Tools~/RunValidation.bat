@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "TARGET_METHOD=%~1"

if "%TARGET_METHOD%"=="" set "TARGET_METHOD=VisualValidator.Editor.AutomatedSceneScanner.RunStandardScan"

pushd "%SCRIPT_DIR%.."
set "PROJECT_ROOT=%CD%"
popd

echo [INFO] Running: %TARGET_METHOD%

for /f "tokens=2" %%a in ('findstr /C:"m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do set "UNITY_VERSION=%%a"
set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\!UNITY_VERSION!\Editor\Unity.exe"

taskkill /f /im Unity.exe >nul 2>&1

"!UNITY_EXE!" -batchmode -projectPath "%PROJECT_ROOT%" -executeMethod %TARGET_METHOD% -logFile "%PROJECT_ROOT%\Logs\AutomationLog.txt" -quit

python "%SCRIPT_DIR%analyze_captures.py" "%PROJECT_ROOT%\ValidationCaptures"
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%"
exit /b 0