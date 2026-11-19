// Ortak.cs - Program ve kurulum dosyasinin paylastigi bilgiler
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// Exe dosyasinin Ozellikler > Ayrintilar sekmesinde gorunen ortak bilgiler (program ve kurulum dosyasi).
// Dosya adi ve aciklamasi her exe'nin kendi dosyasinda (Program.cs, Kurulum.cs) verilir.
[assembly: AssemblyProduct("Sürücü Aracı")]
[assembly: AssemblyCompany("projectsuha")]
[assembly: AssemblyCopyright("© 2026 projectsuha · MIT Lisansı")]
[assembly: AssemblyVersion(SurucuAraci.Ortak.Surum)]
[assembly: AssemblyFileVersion(SurucuAraci.Ortak.Surum)]
[assembly: AssemblyInformationalVersion(SurucuAraci.Ortak.Surum)]

namespace SurucuAraci
{
    static class Ortak
    {
        // Yeni surum yayinlarken bu sayi ile git etiketi (v2.1 gibi) ayni olmalidir; exe'nin dosya surumu de buradan gelir
        public const string Surum = "2.8";
        public const string Depo = "projectsuha/surucu-araci";

        // Program yeni surumleri GitHub Releases sayfasindan denetler (depo herkese acik olmalidir)
        public static readonly bool KendiniGuncelle = true;
        public const string KaldirmaAnahtari = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SurucuAraci";
        public const string BaslangicAnahtari = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string BaslangicDegeri = "SurucuAraci";

        public static readonly string KurulumKlasoru =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SurucuAraci");
        public static readonly string VeriKlasoru =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SurucuAraci");

        public static string Ad
        {
            get { return Dil.M("Sürücü Aracı", "Driver Tool"); }
        }

        public static string BaslatKisayolu
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Ad + ".lnk"); }
        }

        public static string MasaustuKisayolu
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Ad + ".lnk"); }
        }

        // Kisayollar kurulumdaki dile gore iki adla olusturulmus olabilir
        public static IEnumerable<string> TumKisayollar()
        {
            foreach (string ad in new[] { "Sürücü Aracı", "Driver Tool" })
            {
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ad + ".lnk");
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ad + ".lnk");
            }
        }
    }
}
