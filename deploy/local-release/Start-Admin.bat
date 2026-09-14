@echo off
cd /d "%~dp0Admin"
echo Starting ColdNet Admin - open http://localhost:5202 in your browser once it says "Application started".
echo Press Ctrl+C to stop.
ColdNet.Admin.exe
pause
