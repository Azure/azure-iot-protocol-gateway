@echo off
cd %~dp0

SETLOCAL
SET CACHED_NUGET=%LocalAppData%\NuGet\NuGet.exe

IF EXIST %CACHED_NUGET% goto vsvarssetup
echo Downloading latest version of NuGet.exe...
IF NOT EXIST %LocalAppData%\NuGet md %LocalAppData%\NuGet
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest 'https://www.nuget.org/nuget.exe' -OutFile '%CACHED_NUGET%'"

:vsvarssetup
if not defined VS140COMNTOOLS goto build
if not exist "%VS140COMNTOOLS%\VsDevCmd.bat" goto build
call "%VS140COMNTOOLS%\VsDevCmd.bat"

:build
%CACHED_NUGET% restore ProtocolGateway.sln
msbuild ProtocolGateway.sln