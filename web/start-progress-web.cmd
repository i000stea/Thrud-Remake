@echo off
setlocal
title Thrud Progress Web
cd /d "%~dp0"
echo [progress-web] Starting local progress web...
echo [progress-web] Browser will open automatically. Close this window to stop Node.
node "%~dp0server.js"
endlocal
