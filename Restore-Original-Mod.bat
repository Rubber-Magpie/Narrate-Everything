@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "PLUGINS=C:\Program Files (x86)\Steam\steamapps\common\Date Everything\BepInEx\plugins"

tasklist /FI "IMAGENAME eq Date Everything.exe" 2>nul | find /I "Date Everything.exe" >nul
if not errorlevel 1 goto gamerunning
if not exist "%~dp0backup\DateEverythingAccess-original.dll" goto nobackup

copy /y "%~dp0backup\DateEverythingAccess-original.dll" "%PLUGINS%\DateEverythingAccess.dll" >nul
if errorlevel 1 goto copyfailed
call :say "The original version 1.2 of the mod has been put back."
exit /b 0

:gamerunning
call :say "Date Everything is still running. Close the game completely, then run this file again."
exit /b 1
:nobackup
call :say "There is no backup yet, so there is nothing to restore. The original mod is still installed."
exit /b 1
:copyfailed
call :say "Restoring failed. The game folder may be write-protected. Tell Claude."
exit /b 1

:say
powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('%~1','Date Everything Access')"
exit /b 0
