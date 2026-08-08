@echo off
setlocal
color 0A
echo =========================================================
echo   STIMTAKE V1 - BUILD UPDATED TOGGLE ACTIONS
ECHO =========================================================
echo.
echo Action behavior in this build:
echo   OFF = dark button
ECHO   ON  = green button
ECHO   Click same Action again = OFF
ECHO   NO Action timer / NO 120-second limit
ECHO.
call "%~dp0Build-CreatorCam.cmd"
if errorlevel 1 (
  echo.
  echo Build failed. Read Build-CreatorCam.log in this folder.
  pause
  exit /b 1
)
if exist "%~dp0Creator-Cam-Overlay-Kit.exe" (
  echo.
  echo Starting updated StimTake Studio...
  start "" "%~dp0Creator-Cam-Overlay-Kit.exe"
)
endlocal
