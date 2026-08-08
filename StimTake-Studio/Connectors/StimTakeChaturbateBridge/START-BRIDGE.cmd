@echo off
setlocal
cd /d "%~dp0"
echo StimTake Chaturbate Bridge
echo.
echo Start StimTake Studio FIRST, then enter this model's Chaturbate username.
echo The API token is requested privately by Python and is NOT saved by this script.
echo.
py -3 "%~dp0stimtake_chaturbate_bridge.py"
pause
