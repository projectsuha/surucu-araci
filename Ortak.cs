// Ortak.cs - Program ve kurulum dosyasinin paylastigi bilgiler
using System;
using System.Collections.Generic;
using System.IO;

namespace SurucuAraci
{
    static class Ortak
    {
        public const string Surum = "2.0";
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
