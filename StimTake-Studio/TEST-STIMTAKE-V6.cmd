@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUTPUT_DIR=%SCRIPT_DIR%outputs\v6"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

"%CSC%" /nologo /target:exe /optimize+ /platform:anycpu ^
  /main:CreatorCamOverlayKit.StimTakeLocalTests ^
  /out:"%OUTPUT_DIR%\StimTake-V6-Local-Tests.exe" ^
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
  "%SCRIPT_DIR%StimTakeStudioV6.cs" ^
  "%SCRIPT_DIR%StimTakeLocalTests.cs"

if errorlevel 1 exit /b 1
"%OUTPUT_DIR%\StimTake-V6-Local-Tests.exe"
exit /b %errorlevel%
