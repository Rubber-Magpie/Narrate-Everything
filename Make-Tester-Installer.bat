@echo off
setlocal EnableExtensions
rem Builds DateEverythingAccess-Setup.exe for testers with Windows' built-in IExpress.
rem IExpress is fussy about spaces in paths, so it works in a plain temporary folder.
cd /d "%~dp0"
set "PKG=%~dp0tester-package\"
set "WORK=%TEMP%\DEASetup"
set "LOG=%~dp0tester-package\setup-build-log.txt"
set "OUT=%~dp0DateEverythingAccess-Setup.exe"
if not exist "%PKG%payload.zip" goto missing
echo Setup build started %date% %time% > "%LOG%"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%WORK%" >> "%LOG%" 2>&1
copy /y "%PKG%install.cmd" "%WORK%\" >> "%LOG%" 2>&1
copy /y "%PKG%install.ps1" "%WORK%\" >> "%LOG%" 2>&1
copy /y "%PKG%payload.zip" "%WORK%\" >> "%LOG%" 2>&1
set "SED=%WORK%\setup.sed"
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
echo TargetName=%WORK%\DEASetup.exe
echo FriendlyName=Date Everything Access Setup
echo AppLaunched=cmd.exe /c install.cmd
echo PostInstallCmd=^<None^>
echo AdminQuietInstCmd=
echo UserQuietInstCmd=
echo FILE0="install.cmd"
echo FILE1="install.ps1"
echo FILE2="payload.zip"
echo [SourceFiles]
echo SourceFiles0=%WORK%\
echo [SourceFiles0]
echo %%FILE0%%=
echo %%FILE1%%=
echo %%FILE2%%=
) > "%SED%"
copy /y "%SED%" "%PKG%setup.sed.txt" >nul
echo Running IExpress >> "%LOG%"
start "" /wait "%SystemRoot%\System32\iexpress.exe" /N /Q "%SED%"
echo IExpress exit code %errorlevel% >> "%LOG%"
dir "%WORK%" >> "%LOG%" 2>&1
if not exist "%WORK%\DEASetup.exe" goto failed
copy /y "%WORK%\DEASetup.exe" "%OUT%" >> "%LOG%" 2>&1
if not exist "%OUT%" goto failed
echo Setup build finished OK >> "%LOG%"
call :say "DateEverythingAccess-Setup.exe is ready in the Date Everything Access folder. Give that one file to your testers."
exit /b 0
:missing
call :say "The tester package is missing payload.zip. Tell Claude."
exit /b 1
:failed
echo Setup build FAILED >> "%LOG%"
call :say "Making the setup file failed. Tell Claude, and it will read setup-build-log.txt to see why."
exit /b 1
:say
powershell -NoProfile -Command "Add-Type -AssemblyName PresentationFramework; [void][System.Windows.MessageBox]::Show('%~1','Date Everything Access')"
exit /b 0
