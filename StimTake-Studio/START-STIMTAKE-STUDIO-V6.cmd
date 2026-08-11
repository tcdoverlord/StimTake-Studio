@echo off
setlocal
set "ROOT=%~dp0"
set "APP=%ROOT%outputs\v6\StimTake-Studio-6.0.exe"
if not exist "%APP%" (
  echo StimTake Studio 6.0 has not been built yet.
  echo.
  echo Run BUILD-STIMTAKE-V6-AND-DESIGNER.cmd first.
  echo.
  pause
  exit /b 1
)
start "" "%APP%"
