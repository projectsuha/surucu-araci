@echo off
:: Programi (SurucuAraci.exe) ve kurulum dosyasini (SurucuAraciKurulum.exe) Windows'un kendi C# derleyicisiyle derler
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set ORTAK=/nologo /target:winexe /codepage:65001 /optimize /win32icon:SurucuAraci.ico /resource:SurucuAraci.ico,SurucuAraci.Simge.ico ^
  /r:System.Management.dll /r:Microsoft.CSharp.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:System.Xml.dll /r:System.Xml.Linq.dll /r:System.Web.Extensions.dll

%CSC% %ORTAK% /out:SurucuAraci.exe /main:SurucuAraci.Program ^
  Program.cs AnaForm.cs AyarlarFormu.cs Sistem.cs Bakim.cs Programlar.cs FormatKurulumu.cs Guncelleyici.cs Veri.cs Modeller.cs Gorunum.cs Dil.cs Ortak.cs
if errorlevel 1 exit /b 1

:: Kurulum dosyasi programi icinde tasir (veri modellerinin hepsini kullanmadigi icin CS0649 uyarisi kapatilir)
%CSC% %ORTAK% /out:SurucuAraciKurulum.exe /resource:SurucuAraci.exe,SurucuAraci.Uygulama.exe /nowarn:649 ^
  Kurulum.cs Gorunum.cs Modeller.cs Dil.cs Ortak.cs
