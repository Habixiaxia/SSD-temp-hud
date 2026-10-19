@echo off
setlocal
cd /d "%~dp0"

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FW%\WPF"

if not exist "%FW%\csc.exe" (
    echo ERROR: csc.exe not found at %FW%
    exit /b 1
)
if not exist "%WPF%\PresentationFramework.dll" (
    echo ERROR: WPF assemblies not found at %WPF%
    exit /b 1
)

if not exist "bin" mkdir "bin"

"%FW%\csc.exe" /nologo /target:winexe /platform:anycpu /optimize+ ^
    /out:"bin\SsdTempHud.exe" ^
    /win32manifest:"app.manifest" ^
    /win32icon:"app.ico" ^
    /codepage:65001 ^
    /r:"%WPF%\PresentationFramework.dll" ^
    /r:"%WPF%\PresentationCore.dll" ^
    /r:"%WPF%\WindowsBase.dll" ^
    /r:"%FW%\System.Xaml.dll" ^
    /r:"%FW%\System.Core.dll" ^
    /r:"%FW%\System.Windows.Forms.dll" ^
    /r:"%FW%\System.Drawing.dll" ^
    /r:"%FW%\System.Management.dll" ^
    /r:"%FW%\System.Runtime.Serialization.dll" ^
    src\*.cs src\Sensors\*.cs

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo BUILD OK: bin\SsdTempHud.exe
