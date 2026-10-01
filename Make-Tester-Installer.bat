@echo off
setlocal EnableExtensions
rem Builds DateEverythingAccess-Setup.exe for testers with Windows' built-in IExpress.
cd /d "%~dp0"
set "PKG=%~dp0tester-package\"
set "OUT=%~dp0DateEverythingAccess-Setup.exe"
set "SED=%TEMP%\DateEverythingAccess-Setup.sed"
if not exist "%PKG%payload.zip" goto missing
if exist "%OUT%" del /q "%OUT%"
(
echo [Version]
echo Class=IEXPRESS
echo SEDVersion=3
echo [Options]
echo PackagePurpose=InstallApp
echo ShowInstallProgramWindow=1
echo HideExtractAnimation=1
echo UseLongFileName=1
echo InsideCompressed=0
echo CAB_FixedSize=0
echo CAB_ResvCodeSigning=0
echo RebootMode=N
echo InstallPrompt=%%InstallPrompt%%
echo DisplayLicense=%%DisplayLicense%%
echo FinishMessage=%%FinishMessage%%
echo TargetName=%%TargetName%%
echo FriendlyName=%%FriendlyName%%
echo AppLaunched=%%AppLaunched%%
echo PostInstallCmd=%%PostInstallCmd%%
echo AdminQuietInstCmd=%%AdminQuietInstCmd%%
echo UserQuietInstCmd=%%UserQuietInstCmd%%
echo SourceFiles=SourceFiles
echo [Strings]
echo InstallPrompt=
echo DisplayLicense=
echo FinishMessage=
echo TargetName=%OUT%
echo FriendlyName=Date Everything Access Setup
echo AppLaunched=cmd.exe /c install.cmd
echo PostInstallCmd=^<None^>
echo AdminQuietInstCmd=
echo UserQuietInstCmd=
echo FILE0="install.cmd"
echo FILE1="install.ps1"
echo FILE2="payload.zip"
echo [SourceFiles]
echo SourceFiles0=%PKG%
echo [SourceFiles0]
echo %%FILE0%%=
echo %%FILE1%%=
echo %%FILE2%%=
) > "%SED%"
iexpress.exe /N /Q "%SED%"
if not exist "%OUT%" goto failed
call :say "DateEverythingAccess-Setup.exe is ready in the Date Everything Access folder. Give that one file to your testers."
exit /b 0
:missing
call :say "The tester package is missing payload.zip. Tell Claude."
exit /b 1
:failed
call :say "Making the setup file failed. Tell Claude."
exit /b 1
:say
powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('%~1','Date Everything Access')"
exit /b 0
