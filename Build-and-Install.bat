@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "GAMEDIR=C:\Program Files (x86)\Steam\steamapps\common\Date Everything"
set "PLUGINS=%GAMEDIR%\BepInEx\plugins"
set "LOG=%~dp0build-log.txt"

tasklist /FI "IMAGENAME eq Date Everything.exe" 2>nul | find /I "Date Everything.exe" >nul
if not errorlevel 1 goto gamerunning

if not exist "%~dp0backup\DateEverythingAccess-original.dll" call :backup

echo Build started %date% %time% > "%LOG%"
dotnet build "%~dp0DateEverythingAccess.csproj" -c Release -p:GameDir="%GAMEDIR%" -nologo >> "%LOG%" 2>&1
if errorlevel 1 goto failed

echo Build finished OK %date% %time% >> "%LOG%"
call :say "Build succeeded and the new mod is installed. You can start the game."
exit /b 0

:failed
echo Build FAILED %date% %time% >> "%LOG%"
call :say "The build failed. Nothing in the game was changed. Tell Claude, and it will read build-log.txt to see why."
exit /b 1

:gamerunning
call :say "Date Everything is still running. Close the game completely, then run this file again."
exit /b 1

:backup
mkdir "%~dp0backup" 2>nul
copy /y "%PLUGINS%\DateEverythingAccess.dll" "%~dp0backup\DateEverythingAccess-original.dll" >nul
exit /b 0

:say
powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('%~1','Date Everything Access')"
exit /b 0
