@echo off
rem This file MUST stay pure ASCII. cmd.exe on Chinese Windows parses .cmd as GBK,
rem so UTF-8 Chinese text would turn into mojibake and break the if(...) blocks.
rem Chinese user-facing messages are printed by Node instead.
setlocal EnableExtensions
chcp 65001 >nul
title Thrud Progress Web
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo [progress-web] Node.js not found. Please install Node.js and add it to PATH.
  echo [progress-web] Download: https://nodejs.org/
  pause
  exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
  echo [progress-web] npm not found. Please check your Node.js installation.
  pause
  exit /b 1
)

echo [progress-web] Checking dependencies...
node "scripts\ensure-deps.js"
if errorlevel 1 (
  echo.
  echo [progress-web] Dependency check or install failed. See the log above.
  echo [progress-web] If offline, run "npm ci" on a networked machine, then copy node_modules.
  pause
  exit /b 1
)

echo [progress-web] Starting local service...
echo [progress-web] The browser opens when the service is ready. Close this window to stop Node.
node server.js %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
