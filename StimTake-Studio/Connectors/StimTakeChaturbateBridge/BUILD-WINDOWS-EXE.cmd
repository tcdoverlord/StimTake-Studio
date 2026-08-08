@echo off
setlocal
cd /d "%~dp0"
echo OPTIONAL WINDOWS PACKAGING
echo This does not install anything automatically.
echo.
where pyinstaller >nul 2>nul
if errorlevel 1 (
  echo PyInstaller is not installed in this Python environment.
  echo Install/review it separately before using this build helper.
  pause
  exit /b 1
)
pyinstaller --onefile --name StimTake-Chaturbate-Bridge "%~dp0stimtake_chaturbate_bridge.py"
pause
