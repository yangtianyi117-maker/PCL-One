@echo off
chcp 65001 >nul
echo PCL Earth installer
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-earth.ps1" %*
echo.
pause
