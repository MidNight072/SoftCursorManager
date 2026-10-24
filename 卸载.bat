@echo off
chcp 936 >nul
echo Uninstalling SoftCursorManager...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
echo.
echo Done. You can close this window.
pause
