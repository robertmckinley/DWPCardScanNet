@echo off
rem ============================================================================
rem  install.cmd [targetDir] - xcopy the staged app to the target machine and
rem  create a Desktop shortcut. Run on the TARGET machine with the dist folder
rem  present alongside this script. Default target: C:\MugConfig\DWPCardScanNet
rem
rem  Prerequisites on the target (see DEPLOYMENT.md):
rem    - .NET Framework 4.8 runtime
rem    - Aware AccuScan + NISTPack installed (provides the native DLLs; x86)
rem    - After install: create the real DWPCardScan.ini in the install folder
rem      (the package ships only DWPCardScan_Sample.ini, which is never loaded)
rem ============================================================================
setlocal
set "HERE=%~dp0"
set "SRC=%HERE%dist\DWPCardScanNet"
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=C:\MugConfig\DWPCardScanNet"

if not exist "%SRC%\DWPCardScanNet.exe" (
    echo Package not found at "%SRC%".
    echo Run package.cmd on a build machine first, then copy the dist folder here.
    exit /b 1
)

echo Installing DWPCardScanNet to "%TARGET%" ...

rem Never overwrite a live site config. Preserve any existing DWPCardScan.ini across
rem the copy and restore it afterwards, regardless of what the package contains.
set "CFG=%TARGET%\DWPCardScan.ini"
set "CFGBAK=%TEMP%\DWPCardScan.ini.installbak"
set "CFGKEPT="
if exist "%CFG%" (
    copy /Y "%CFG%" "%CFGBAK%" >nul
    set "CFGKEPT=1"
)

xcopy "%SRC%\*" "%TARGET%\" /E /I /Y >nul
set "RC=%ERRORLEVEL%"

if defined CFGKEPT (
    copy /Y "%CFGBAK%" "%CFG%" >nul
    del "%CFGBAK%" >nul 2>&1
    echo Preserved existing DWPCardScan.ini.
)

if not "%RC%"=="0" (
    echo *** COPY FAILED - check write permission to the target folder ***
    exit /b 1
)

echo Creating Desktop shortcut ...
powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\DWP Card Scan.lnk'); $s.TargetPath='%TARGET%\DWPCardScanNet.exe'; $s.WorkingDirectory='%TARGET%'; $s.IconLocation='%TARGET%\DWPCardScanNet.exe,0'; $s.Save()" 2>nul

echo.
echo Installed. Launch: "%TARGET%\DWPCardScanNet.exe"
endlocal
