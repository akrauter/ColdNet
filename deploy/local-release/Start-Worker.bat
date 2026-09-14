@echo off
cd /d "%~dp0Worker"
echo Starting ColdNet Worker (background processing loop).
echo Press Ctrl+C to stop.
ColdNet.Worker.exe
pause
