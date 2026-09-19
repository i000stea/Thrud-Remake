@echo off
setlocal EnableExtensions
title Thrud Progress Web
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo [progress-web] 未找到 node。请先安装 Node.js 并确保它在 PATH 中。
  echo [progress-web] 下载地址：https://nodejs.org/
  pause
  exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
  echo [progress-web] 未找到 npm。请检查 Node.js 安装是否完整。
  pause
  exit /b 1
)

echo [progress-web] 检查依赖...
node "scripts\ensure-deps.js"
if errorlevel 1 (
  echo.
  echo [progress-web] 依赖检查或安装失败，请查看上方日志。
  echo [progress-web] 若网络不通，可先在有网络的机器上执行 npm ci，再拷贝 node_modules。
  pause
  exit /b 1
)

echo [progress-web] 启动本地服务...
echo [progress-web] 服务就绪后浏览器会自动打开。关闭此窗口即可停止 Node。
node server.js
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
