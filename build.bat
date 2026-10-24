@echo off
rem Build SoftCursorManager.exe with the .NET Framework compiler (ships with Windows).
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /out:SoftCursorManager.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll SoftCursorManager.cs
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Built SoftCursorManager.exe
