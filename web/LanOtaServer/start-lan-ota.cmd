@echo off
rem This file MUST stay pure ASCII. cmd.exe on Chinese Windows parses .cmd as GBK,
rem so UTF-8 Chinese text would turn into mojibake and break the if(...) blocks.
rem All Chinese output is printed by Node and by the web console page.
setlocal EnableExtensions
chcp 65001 >nul
title Thrud LAN OTA Console
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo [lan-ota] Node.js not found. Please install Node.js and add it to PATH.
  echo [lan-ota] Download: https://nodejs.org/
  pause
  exit /b 1
)

echo [lan-ota] Starting LAN OTA server + console...
echo [lan-ota] The console page opens in your browser. Keep this window open.
echo [lan-ota] Press Ctrl+C or close this window to stop.
echo.

node server.js %*
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
  echo.
  echo [lan-ota] Server stopped with exit code %EXITCODE%.
  echo [lan-ota] Common causes: port already in use, or no permission to bind the port.
  echo [lan-ota] Change port:  start-lan-ota.cmd --port=8090
  echo [lan-ota] Change root:  start-lan-ota.cmd --root=D:\ota-content
  echo.
  pause
)
endlocal & exit /b %EXITCODE%
