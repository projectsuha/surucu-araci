@echo off
:: Programi (SurucuAraci.exe) ve kurulum dosyasini (SurucuAraciKurulum.exe) Windows'un kendi C# derleyicisiyle derler
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set ORTAK=/nologo /target:winexe /codepage:65001 /optimize /win32icon:SurucuAraci.ico /resource:SurucuAraci.ico,SurucuAraci.Simge.ico ^
  /r:System.Management.dll /r:Microsoft.CSharp.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:System.Xml.dll /r:System.Xml.Linq.dll

%CSC% %ORTAK% /out:SurucuAraci.exe /main:SurucuAraci.Program ^
  Program.cs AnaForm.cs AyarlarFormu.cs Sistem.cs Veri.cs Modeller.cs Gorunum.cs Dil.cs Ortak.cs
if errorlevel 1 exit /b 1

:: Kurulum dosyasi programi icinde tasir
%CSC% %ORTAK% /out:SurucuAraciKurulum.exe /resource:SurucuAraci.exe,SurucuAraci.Uygulama.exe ^
  Kurulum.cs Gorunum.cs Dil.cs Ortak.cs
