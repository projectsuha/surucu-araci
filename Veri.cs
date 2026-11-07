// Veri.cs - Ayarlar ve islem gecmisi (%APPDATA%\SurucuAraci)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SurucuAraci
{
    static class Ayarlar
    {
        static readonly string Dosya = Path.Combine(Ortak.VeriKlasoru, "ayarlar.txt");
        static readonly Dictionary<string, string> degerler = new Dictionary<string, string>();
        public static bool SadeceBellek = false;  // true ise degisiklikler diske yazilmaz (onizleme ve testler)

        public static void Yukle()
        {
            try
            {
                if (!File.Exists(Dosya)) return;
                foreach (string satir in File.ReadAllLines(Dosya, Encoding.UTF8))
                {
                    int i = satir.IndexOf('=');
                    if (i > 0) degerler[satir.Substring(0, i)] = satir.Substring(i + 1);
                }
            }
            catch (Exception) { }
        }

        static void Kaydet()
        {
            if (SadeceBellek) return;
            try
            {
                Directory.CreateDirectory(Ortak.VeriKlasoru);
                File.WriteAllLines(Dosya, degerler.Select(d => d.Key + "=" + d.Value), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        static string Al(string ad, string varsayilan)
        {
            string deger;
            return degerler.TryGetValue(ad, out deger) ? deger : varsayilan;
        }

        // Dosya once yeniden okunur: tepsideki program ile zamanlanmis otomatik koruma ayni dosyaya yazabilir
        static void Yaz(string ad, string deger)
        {
            if (!SadeceBellek) Yukle();
            degerler[ad] = deger;
            Kaydet();
        }

        // sistem, acik, koyu
        public static string TemaSecimi { get { return Al("tema", "sistem"); } set { Yaz("tema", value); } }

        // sistem, tr, en
        public static string DilSecimi { get { return Al("dil", "sistem"); } set { Yaz("dil", value); } }

        // Kac gunde bir otomatik kontrol yapilacagi; 0 = kapali
        public static int KontrolGunu
        {
            get { int gun; return int.TryParse(Al("kontrol", "7"), out gun) ? gun : 7; }
            set { Yaz("kontrol", value.ToString(CultureInfo.InvariantCulture)); }
        }

        public static bool TepsideCalis { get { return Al("tepsi", "1") == "1"; } set { Yaz("tepsi", value ? "1" : "0"); } }
        public static bool Bildirim { get { return Al("bildirim", "1") == "1"; } set { Yaz("bildirim", value ? "1" : "0"); } }
        public static bool TepsiIpucuGosterildi { get { return Al("tepsiipucu", "0") == "1"; } set { Yaz("tepsiipucu", value ? "1" : "0"); } }

        public static DateTime SonKontrol
        {
            get { return Tarih("sonkontrol"); }
            set { Yaz("sonkontrol", value.Ticks.ToString(CultureInfo.InvariantCulture)); }
        }

        // Programin kendi yeni surumunu GitHub'dan denetleme
        public static bool ProgramGuncellemesi { get { return Al("programguncelleme", "1") == "1"; } set { Yaz("programguncelleme", value ? "1" : "0"); } }

        public static DateTime SonProgramDenetimi
        {
            get { return Tarih("sonprogramdenetimi"); }
            set { Yaz("sonprogramdenetimi", value.Ticks.ToString(CultureInfo.InvariantCulture)); }
        }

        // Haftalik geri yukleme noktasi ve aylik surucu yedegi icin zamanlanmis gorev kuruldu mu
        public static bool OtomatikKoruma { get { return Al("otomatikkoruma", "0") == "1"; } set { Yaz("otomatikkoruma", value ? "1" : "0"); } }

        // Otomatik koruma (zamanlanmis gorev) en son ne zaman nokta olusturdu ve yedek aldi
        public static DateTime SonOtomatikNokta
        {
            get { return Tarih("sonotomatiknokta"); }
            set { Yaz("sonotomatiknokta", value.Ticks.ToString(CultureInfo.InvariantCulture)); }
        }

        public static DateTime SonOtomatikYedek
        {
            get { return Tarih("sonotomatikyedek"); }
            set { Yaz("sonotomatikyedek", value.Ticks.ToString(CultureInfo.InvariantCulture)); }
        }

        static DateTime Tarih(string ad)
        {
            long t;
            return long.TryParse(Al(ad, "0"), out t) ? new DateTime(t) : DateTime.MinValue;
        }
    }

    // Her islem bir satir: tarih, islem kodu, ayrinti, onceki, sonraki, sonuc, basarili (sekmeyle ayrilmis)
    static class Gecmis
    {
        static readonly string Dosya = Path.Combine(Ortak.VeriKlasoru, "gecmis.txt");
        static readonly object kilit = new object();

        static string Temizle(string metin)
        {
            return (metin ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        public static void Ekle(string islem, string ayrinti, string eski, string yeni, string sonuc, bool basarili)
        {
            string satir = string.Join("\t", DateTime.Now.ToString("o", CultureInfo.InvariantCulture), Temizle(islem), Temizle(ayrinti),
                Temizle(eski), Temizle(yeni), Temizle(sonuc), basarili ? "1" : "0");
            lock (kilit)
            {
                try
                {
                    Directory.CreateDirectory(Ortak.VeriKlasoru);
                    File.AppendAllText(Dosya, satir + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception) { }
            }
        }

        // En yeni kayit en ustte
        public static List<GecmisKaydi> Oku()
        {
            var kayitlar = new List<GecmisKaydi>();
            lock (kilit)
            {
                try
                {
                    if (!File.Exists(Dosya)) return kayitlar;
                    foreach (string satir in File.ReadAllLines(Dosya, Encoding.UTF8))
                    {
                        string[] p = satir.Split('\t');
                        DateTime tarih;
                        if (p.Length < 7 || !DateTime.TryParse(p[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out tarih)) continue;
                        kayitlar.Add(new GecmisKaydi { Tarih = tarih, Islem = p[1], Ayrinti = p[2], Eski = p[3], Yeni = p[4], Sonuc = p[5], Basarili = p[6] == "1" });
                    }
                }
                catch (Exception) { }
            }
            kayitlar.Reverse();
            return kayitlar;
        }

        public static void Sil()
        {
            lock (kilit)
            {
                try { File.Delete(Dosya); } catch (Exception) { }
            }
        }
    }
}
