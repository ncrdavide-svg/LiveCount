@echo off
rem Compila LiveCount.exe con il compilatore C# incluso in Windows (.NET Framework 4.x).
rem Se trova la libreria NDI la incorpora nell'exe (che la estrae da solo all'avvio).
setlocal
set ROOT=%~dp0
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set NDIDLL=%ROOT%lib\Processing.NDI.Lib.x64.dll

if not exist "%NDIDLL%" call :copyndi "%NDI_RUNTIME_DIR_V6%"
if not exist "%NDIDLL%" call :copyndi "%ProgramFiles%\NDI\NDI 6 Tools\Runtime"
if not exist "%NDIDLL%" call :copyndi "%ProgramFiles%\NDI\NDI 6 Runtime\v6"
if not exist "%NDIDLL%" call :copyndi "%ProgramFiles%\NDI\NDI 6 SDK\Bin\x64"

set RES=
if exist "%NDIDLL%" (
  set RES=/resource:"%NDIDLL%",ndi.x64.dll
  echo NDI: libreria incorporata nell'exe
) else (
  echo NDI: libreria non trovata - l'exe usera' il runtime NDI installato, se presente
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:x64 /out:"%ROOT%LiveCount.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll %RES% /resource:"%ROOT%assets\BuyMeACoffeeQR.png",BuyMeACoffeeQR.png /resource:"%ROOT%assets\LiveCount.ico",LiveCount.ico /win32icon:"%ROOT%assets\LiveCount.ico" "%ROOT%src\*.cs"
exit /b %ERRORLEVEL%

:copyndi
if "%~1"=="" exit /b 0
if not exist "%~1\Processing.NDI.Lib.x64.dll" exit /b 0
if not exist "%ROOT%lib" mkdir "%ROOT%lib"
copy /y "%~1\Processing.NDI.Lib.x64.dll" "%NDIDLL%" >nul
exit /b 0
