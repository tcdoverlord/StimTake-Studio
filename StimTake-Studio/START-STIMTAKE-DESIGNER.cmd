@echo off
setlocal
set "ROOT=%~dp0"
set "APP=%ROOT%outputs\v6\StimTake-Designer-1.0.exe"
if not exist "%APP%" (
  echo StimTake Designer has not been built yet.
  echo.
  echo Run BUILD-STIMTAKE-V6-AND-DESIGNER.cmd first.
  echo.
  pause
  exit /b 1
)
start "" "%APP%"
