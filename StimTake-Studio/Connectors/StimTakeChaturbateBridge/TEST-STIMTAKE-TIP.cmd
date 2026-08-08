@echo off
setlocal
cd /d "%~dp0"
echo This sends a LOCAL fake tip to StimTake. It does not contact Chaturbate.
py -3 "%~dp0stimtake_chaturbate_bridge.py" --test-tip --test-username BridgeTest --test-amount 25 --test-message "!dice"
pause
