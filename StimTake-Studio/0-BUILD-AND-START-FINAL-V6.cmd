@echo off
setlocal
cd /d "%~dp0"

echo ==========================================
echo   StimTake Studio 6.0 Final PoC Build
echo ==========================================
echo.
echo This does NOT delete the previous build.
echo It rebuilds the current V6 source and starts Studio only if the build succeeds.
echo.

call "%~dp0BUILD-STIMTAKE-V6-AND-DESIGNER.cmd"
if errorlevel 1 (
  echo.
  echo Build failed. Nothing was started.
  pause
  exit /b 1
)

if not exist "%~dp0outputs\v6\StimTake-Studio-6.0.exe" (
  echo Build reported success but StimTake-Studio-6.0.exe is missing.
  pause
  exit /b 1
)

start "" "%~dp0outputs\v6\StimTake-Studio-6.0.exe"
exit /b 0
