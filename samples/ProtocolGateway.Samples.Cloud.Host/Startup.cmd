REM *********************************************************
REM
REM     Copyright (c) Microsoft. All rights reserved.
REM     This code is licensed under the Microsoft Public License.
REM     THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
REM     ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
REM     IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
REM     PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
REM
REM *********************************************************
 
REM Check if the script is running in the Azure emulator and if so do not run
if "%IsEmulated%"=="true" goto :PastGC
 
if "%UseServerGC%"=="False" goto :ValidateBackground
if "%UseServerGC%"=="0" goto :ValidateBackground
set UseServerGC="True"
 
:ValidateBackground
if "%UseBackgroundGC%"=="False" goto :SetGC
if "%UseBackgroundGC%"=="0" goto :SetGC
set UseBackgroundGC="True"
 
:SetGC
 PowerShell.exe -executionpolicy unrestricted -command ".\GCSettingsManagement.ps1" -serverGC %UseServerGC% -backgroundGC %UseBackgroundGC%

:PastGC
netsh int ipv4 set dynamicport tcp start=1025 num=64511
netsh int ipv4 show dynamicport tcp
 
exit /b