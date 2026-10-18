@echo off
rem Builds dist\BigBootyCheems.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK, Visual Studio or downloads needed.
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find csc.exe from .NET Framework 4.x.
  exit /b 1
)

if not exist dist mkdir dist

rem every picture and sound under art\ is embedded in the exe, named by its path inside art\
rem (art\clothes\hat\crown.png becomes "clothes/hat/crown.png"). The list goes in a response file
rem so the command line never gets too long, however many items are added.
set "ROOT=%CD%\art\"
> dist\resources.rsp (
  for /r art %%f in (*.png *.wav) do (
    set "FULL=%%f"
    set "REL=!FULL:%ROOT%=!"
    if /i "!REL:~0,4!"=="art\" set "REL=!REL:~4!"
    set "NAME=!REL:\=/!"
    echo /resource:"art\!REL!",!NAME!
  )
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:dist\BigBootyCheems.exe /win32icon:art\icon.ico ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ^
  @dist\resources.rsp src\*.cs
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)

echo.
echo Built dist\BigBootyCheems.exe
certutil -hashfile dist\BigBootyCheems.exe SHA256 | findstr /v ":"
endlocal
