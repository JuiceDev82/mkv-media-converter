@echo off
setlocal

REM Point APP at your built MediaMkvConverter.dll. Copy this file into any
REM media folder and run it there to convert that folder.
set "APP=%USERPROFILE%\OneDrive\Code\Media-MKV-Converter\Media MKV Converter\bin\Debug\net9.0\MediaMkvConverter.dll"
set "TARGET=%~dp0"
set "SKIP=\Processing\"

if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"

echo Media MKV Converter
echo.
echo App    : "%APP%"
echo Target : "%TARGET%"
echo Skip   : "%SKIP%"
echo.

dotnet "%APP%" "%TARGET%" --skip "%SKIP%"
set "EXITCODE=%ERRORLEVEL%"

echo.
if "%EXITCODE%"=="0" (
    echo Finished successfully.
) else (
    echo Finished with errors. Exit code: %EXITCODE%
)

pause
exit /b %EXITCODE%
