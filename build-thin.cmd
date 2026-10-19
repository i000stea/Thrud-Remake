@echo off
setlocal EnableExtensions

rem ===========================================================================
rem  Thrud - thin APK build (command line, no editor interaction)
rem
rem  Prereq:
rem    1) Close the Unity Editor that has this project open (project lock).
rem    2) Build target is forced to Android below.
rem
rem  Usage:
rem    build-thin.cmd            guard + rebuild AB + thin APK + publish OTA
rem    build-thin.cmd apk        thin APK only (no AB rebuild)
rem    build-thin.cmd ab         rebuild ResKit AssetBundles only
rem    build-thin.cmd publish    publish current artifacts as OTA content
rem    build-thin.cmd dev        thin Development build
rem    build-thin.cmd fat        fat APK (baseline AB inside, for comparison)
rem
rem  Exit code: 0 = ok, non-zero = failed (log tail is printed).
rem ===========================================================================

set "PROJECT_ROOT=%~dp0"
if "%PROJECT_ROOT:~-1%"=="\" set "PROJECT_ROOT=%PROJECT_ROOT:~0,-1%"

if "%UNITY_EDITOR%"=="" set "UNITY_EDITOR=D:\Program Files\Unity\Editor\6000.3.24f1\Editor\Unity.exe"
if not exist "%UNITY_EDITOR%" goto no_editor

set "TAG=all-thin"
set "ENTRY=TeaGame.Tools.Editor.AndroidBuildCli.BuildAllThin"
if /i "%~1"=="apk" goto sel_apk
if /i "%~1"=="ab" goto sel_ab
if /i "%~1"=="publish" goto sel_publish
if /i "%~1"=="dev" goto sel_dev
if /i "%~1"=="fat" goto sel_fat
goto run

:sel_apk
set "TAG=apk-thin"
set "ENTRY=TeaGame.Tools.Editor.AndroidBuildCli.BuildThinApkOnly"
goto run

:sel_ab
set "TAG=ab"
set "ENTRY=TeaGame.Tools.Editor.AndroidBuildCli.BuildAssetBundles"
goto run

:sel_publish
set "TAG=publish"
set "ENTRY=TeaGame.Tools.Editor.AndroidBuildCli.PublishOtaContent"
goto run

:sel_dev
set "TAG=all-thin-dev"
set "ENTRY=TeaGame.Tools.Editor.AndroidBuildCli.BuildAllThinDev"
goto run

:sel_fat
set "TAG=apk-fat"
set "ENTRY=TeaGame.Tools.Editor.AndroidBuildCli.BuildFatApk"
goto run

:run
set "LOG_DIR=%PROJECT_ROOT%\Build\Android\logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG=%LOG_DIR%\build-%TAG%.log"

echo [build-thin] project = %PROJECT_ROOT%
echo [build-thin] entry   = %ENTRY%
echo [build-thin] log     = %LOG%

"%UNITY_EDITOR%" -batchmode -quit -nographics -projectPath "%PROJECT_ROOT%" -buildTarget Android -executeMethod %ENTRY% -logFile "%LOG%"
set "RC=%ERRORLEVEL%"

if not "%RC%"=="0" goto failed

echo [build-thin] OK. APK is under Build\Android\
exit /b 0

:failed
echo [build-thin] FAILED, exit code %RC%. Log tail:
powershell -NoProfile -Command "Get-Content -Path '%LOG%' -Tail 60"
exit /b %RC%

:no_editor
echo [build-thin] Unity editor not found: %UNITY_EDITOR%
echo [build-thin] Set env var UNITY_EDITOR to Unity.exe and retry.
exit /b 2
