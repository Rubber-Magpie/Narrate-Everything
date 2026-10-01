@echo off
setlocal EnableExtensions
rem Builds DateEverythingAccess-Setup.exe for testers with the C# compiler built into Windows.
cd /d "%~dp0"
set "PKG=%~dp0tester-package"
set "OUT=%~dp0DateEverythingAccess-Setup.exe"
set "LOG=%~dp0tester-package\setup-build-log.txt"
if not exist "%PKG%\payload.zip" goto missing
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PKG%\build-setup.ps1" -Package "%PKG%" -Out "%OUT%" -Log "%LOG%"
if errorlevel 1 goto failed
if not exist "%OUT%" goto failed
call :say "DateEverythingAccess-Setup.exe is ready in the Date Everything Access folder. Give that one file to your testers."
exit /b 0
:missing
call :say "The tester package is missing payload.zip. Tell Claude."
exit /b 1
:failed
call :say "Making the setup file failed. Tell Claude, and it will read setup-build-log.txt to see why."
exit /b 1
:say
powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('%~1','Date Everything Access')"
exit /b 0
