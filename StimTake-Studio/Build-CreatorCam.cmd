@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUTPUT_DIR=%SCRIPT_DIR%outputs"
if exist "%SCRIPT_DIR%..\..\work\creator-cam-exe\CreatorCamLauncher.cs" set "OUTPUT_DIR=%SCRIPT_DIR%..\..\outputs"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
set "OUTPUT=%OUTPUT_DIR%\Creator-Cam-Overlay-Kit.exe"
set "ROOT_OUTPUT=%SCRIPT_DIR%Creator-Cam-Overlay-Kit.exe"
set "BUILD_OUTPUT=%OUTPUT_DIR%\Creator-Cam-Overlay-Kit.building.exe"
set "BUILD_LOG=%SCRIPT_DIR%Build-CreatorCam.log"

if not exist "%CSC%" (
  >"%BUILD_LOG%" echo BUILD FAILED
  >>"%BUILD_LOG%" echo The Windows C# compiler was not found at:
  >>"%BUILD_LOG%" echo %CSC%
  type "%BUILD_LOG%"
  echo.
  pause
  exit /b 1
)

echo Building StimTake Studio 6.0 to the preserved Creator-Cam output path...
echo Close Creator-Cam-Overlay-Kit.exe before continuing.
echo.

if exist "%BUILD_OUTPUT%" del /q "%BUILD_OUTPUT%"

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%BUILD_OUTPUT%" ^
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
  "%SCRIPT_DIR%StimTakeStudioV6.cs" >"%BUILD_LOG%" 2>&1

if errorlevel 1 (
  echo BUILD FAILED
  echo.
  type "%BUILD_LOG%"
  echo.
  echo The details are saved in:
  echo %BUILD_LOG%
  pause
  exit /b 1
)

copy /y "%BUILD_OUTPUT%" "%OUTPUT%" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  echo BUILD COMPILED, BUT THE OLD EXE COULD NOT BE REPLACED.
  echo Close Creator-Cam-Overlay-Kit.exe and run this builder again.
  echo.
  type "%BUILD_LOG%"
  pause
  exit /b 1
)

copy /y "%OUTPUT%" "%ROOT_OUTPUT%" >>"%BUILD_LOG%" 2>&1
if errorlevel 1 (
  echo BUILD SUCCEEDED, BUT THE ROOT EXE COULD NOT BE UPDATED.
  echo Close Creator-Cam-Overlay-Kit.exe and run this builder again.
  pause
  exit /b 1
)

del /q "%BUILD_OUTPUT%"
>>"%BUILD_LOG%" echo BUILD SUCCEEDED
>>"%BUILD_LOG%" echo %OUTPUT%

echo Built successfully:
echo %OUTPUT%
echo.
echo You can close this build window now.
pause
