// Ortak.cs - Program ve kurulum dosyasinin paylastigi bilgiler
using System;
using System.Collections.Generic;
using System.IO;

namespace SurucuAraci
{
    static class Ortak
    {
        // Yeni surum yayinlarken bu sayi ile git etiketi (v2.1 gibi) ayni olmalidir
        public const string Surum = "2.2";
        public const string Depo = "projectsuha/surucu-araci";

        // Depo gizli oldugu icin program GitHub'a sifresiz erisemez; kendi kendini guncelleme bu yuzden kapali.
        // Depo herkese acik yapilirsa true yapmak yeterlidir.
        public static readonly bool KendiniGuncelle = false;
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
