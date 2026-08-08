@echo off
setlocal
call "%~dp0Build-CreatorCam.cmd"
if errorlevel 1 exit /b 1
if exist "%~dp0Creator-Cam-Overlay-Kit.exe" start "" "%~dp0Creator-Cam-Overlay-Kit.exe"
endlocal
