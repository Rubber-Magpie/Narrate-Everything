@echo off
setlocal EnableExtensions
rem Builds dist\NarrateEverythingInstaller.exe, the one file players download and run, with
rem the Rubber Magpie installer (scripts\Package-Installer.ps1). The version comes from
rem installer\Cargo.toml. The game folder is found the same way as in Build-and-Install.bat,
rem or give it as the first argument.
cd /d "%~dp0"
set "LOG=%~dp0installer-build-log.txt"
set "GAMEARG="
if not "%~1"=="" set "GAMEARG=-Game "%~1""
echo Installer build started %date% %time% > "%LOG%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Package-Installer.ps1" %GAMEARG% >> "%LOG%" 2>&1
if errorlevel 1 goto failed
call :say "NarrateEverythingInstaller.exe is ready in the dist folder. That one file is what players download and run."
exit /b 0
:failed
call :say "Making the installer failed. installer-build-log.txt in the project folder says why."
exit /b 1
:say
powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('%~1','Narrate Everything')"
exit /b 0
