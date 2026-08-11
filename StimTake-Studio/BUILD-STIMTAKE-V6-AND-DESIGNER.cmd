@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUTPUT_DIR=%SCRIPT_DIR%outputs\v6"
set "BUILD_LOG=%SCRIPT_DIR%Build-StimTake-V6.log"

if not exist "%CSC%" (
  >"%BUILD_LOG%" echo BUILD FAILED
  >>"%BUILD_LOG%" echo Windows C# compiler not found:
  >>"%BUILD_LOG%" echo %CSC%
  type "%BUILD_LOG%"
  pause
  exit /b 1
)

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
> "%BUILD_LOG%" echo StimTake Studio 6.0 + Designer build
>>"%BUILD_LOG%" echo %DATE% %TIME%

echo.
echo ==========================================
echo   Building StimTake Studio 6.0
echo ==========================================
echo.

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%OUTPUT_DIR%\StimTake-Studio-6.0.exe" ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.IO.Compression.dll ^
  /reference:System.IO.Compression.FileSystem.dll ^
  /reference:System.Web.Extensions.dll ^
  /resource:"%SCRIPT_DIR%CreatorCamPayload.zip",CreatorCamPayload.zip ^
  "%SCRIPT_DIR%CreatorCamLauncher.cs" ^
  "%SCRIPT_DIR%CreatorStudioV3.cs" ^
  "%SCRIPT_DIR%StimTakePlatformRuntime.cs" ^
  "%SCRIPT_DIR%StimTakeShowPack.cs" ^
  "%SCRIPT_DIR%StimTakeStudioV6.cs" >>"%BUILD_LOG%" 2>&1

if errorlevel 1 (
  echo STIMTAKE STUDIO 6.0 BUILD FAILED
  echo.
  type "%BUILD_LOG%"
  pause
  exit /b 1
)

echo.
echo ==========================================
echo   Building StimTake Designer 1.0
echo ==========================================
echo.

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%OUTPUT_DIR%\StimTake-Designer-1.0.exe" ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.IO.Compression.dll ^
  /reference:System.IO.Compression.FileSystem.dll ^
  /reference:System.Web.Extensions.dll ^
  "%SCRIPT_DIR%StimTakeShowPack.cs" ^
  "%SCRIPT_DIR%StimTakeDesigner.cs" >>"%BUILD_LOG%" 2>&1

if errorlevel 1 (
  echo STIMTAKE DESIGNER BUILD FAILED
  echo.
  type "%BUILD_LOG%"
  pause
  exit /b 1
)

>>"%BUILD_LOG%" echo BUILD SUCCEEDED
>>"%BUILD_LOG%" echo %OUTPUT_DIR%\StimTake-Studio-6.0.exe
>>"%BUILD_LOG%" echo %OUTPUT_DIR%\StimTake-Designer-1.0.exe

echo.
echo BUILD SUCCEEDED
echo.
echo Model app:
echo   %OUTPUT_DIR%\StimTake-Studio-6.0.exe
echo.
echo Developer app:
echo   %OUTPUT_DIR%\StimTake-Designer-1.0.exe
echo.
echo The existing Creator-Cam-Overlay-Kit.exe was not overwritten.
echo.
pause
