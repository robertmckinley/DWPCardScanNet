@echo off
rem ============================================================================
rem  package.cmd - build the Release app and stage a deployable folder.
rem  Run on a BUILD machine (needs the .NET SDK). Produces deploy\dist\DWPCardScanNet
rem  containing the exe and all managed dependencies (including the Aware wrappers).
rem ============================================================================
setlocal
set "HERE=%~dp0"
set "PROJ=%HERE%..\src\DWPCardScanNet\DWPCardScanNet.csproj"
set "OUT=%HERE%dist\DWPCardScanNet"

echo Publishing Release (x86) to "%OUT%" ...
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish "%PROJ%" -c Release -o "%OUT%" --nologo
if errorlevel 1 (
    echo.
    echo *** BUILD/PUBLISH FAILED ***
    exit /b 1
)

echo.
echo Package staged at: "%OUT%"
echo Copy this dist folder to the target machine, then run install.cmd there.
endlocal
