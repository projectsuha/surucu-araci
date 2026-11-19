// Modeller.cs - Programin tasidigi veriler
using System;

namespace SurucuAraci
{
    enum Durum { Sorunlu, GuncellemeVar, Guncel }

    // Guncellemenin geldigi yer
    enum GuncellemeKaynagi { WindowsUpdate, Katalog, Yedek }

    class Guncelleme
    {
        public string Id, Baslik, DonanimKimligi, Model, Sinif;
        public DateTime Tarih = DateTime.MinValue;
        public decimal Boyut;
        public GuncellemeKaynagi Kaynak = GuncellemeKaynagi.WindowsUpdate;
        public string Surum = "";          // yeni surucunun surumu (Windows Update'te bilinmez)
        public string Yayinci = "";        // Katalog: paketi yayimlayan sirket
        public string AygitKimligi = "";   // Katalog/Yedek: guncellemenin bulundugu aygit
        public string KurulumKimligi = ""; // Katalog: aygitla paketin ortak donanim kimligi
        public bool Genel;                 // Katalog: bilgisayara ozel surucunun yerine cip ureticisinin genel surucusu
        public bool Zorla;                 // Katalog: Windows kendiliginden secmeyecegi icin zorla kurulur
        public string InfYolu = "";        // Yedek: kurulacak .inf dosyasi
        public string MevcutSurum = "";    // Yedek: ayni paketin bilgisayardaki surumu (aygita baglanamayan satirlar icin)
    }

    class Aygit
    {
        public string Ad = "", Sinif = "", Uretici = "", Surum = "", InfAdi = "", Kimlik = "";
        public DateTime Tarih = DateTime.MinValue;
        public string[] DonanimKimlikleri = new string[0];
        public string[] UyumluKimlikler = new string[0];
        public string EslesenKimlik = "";  // kurulu surucunun aygitla eslestigi kimlik (ne kadar ozel oldugunu gosterir)
        public uint HataKodu;
        public Guncelleme Guncelleme;
        public bool Secili = true;  // "Guncelle" butonu icin isaretli mi

        // 22 = kullanici tarafindan devre disi birakilmis; sorun sayilmaz
        public Durum Durum
        {
            get
            {
                if (HataKodu != 0 && HataKodu != 22) return Durum.Sorunlu;
                return Guncelleme != null ? Durum.GuncellemeVar : Durum.Guncel;
            }
        }
    }

    class Kaynak
    {
        public string Donanim, Ad, Durum, Adres = "", WingetId;
        public bool Kurulu;
        public bool Bilgisayar;  // bilgisayar ureticisinin (Monster, Lenovo...) satiri
        public string SimgeAnahtari = "bilgi";
    }

    class GuncellemeSonucu
    {
        public string Id, Baslik;
        public int Kod;

        // 2 = basarili, 3 = uyarilarla basarili
        public bool Basarili { get { return Kod == 2 || Kod == 3; } }
    }

    class EskiSurucu
    {
        public string Inf, OrijinalAd, Saglayici, Sinif, Surum, YeniSurum;
        public Version SurumNo;
        public DateTime Tarih;
        public long Boyut;
    }

    class GeriNokta
    {
        public uint Sira, Tur;
        public string Aciklama;
        public DateTime Tarih;
    }

    // Sistem Sagligi sekmesindeki bir satir; Grup listede baslik olarak gosterilir
    class SaglikSatiri
    {
        public string Grup, Ad, Deger, Not = "";
        public string SimgeAnahtari = "bilgi";
    }

    // Disk temizliginde bir kategori (gecici dosyalar, Windows Update onbellegi...)
    class TemizlikKalemi
    {
        public string Anahtar, Ad, Konum, Aciklama;
        public long Boyut;
        public int DosyaSayisi;
        public bool YoneticiGerekir, VarsayilanSecili = true;
    }

    enum BaslangicKonumu { KullaniciKaydi, MakineKaydi, Makine32Kaydi, KullaniciKlasoru, OrtakKlasor }

    // Windows acilisinda calisan bir program; Gorev Yoneticisi'yle ayni acik/kapali kaydini kullanir
    class BaslangicOgesi
    {
        public string Ad, Komut;
        public string Anahtar;  // kayit degeri adi veya baslangic klasorundeki dosya adi
        public BaslangicKonumu Konum;
        public bool Etkin;

        public bool YoneticiGerekir
        {
            get { return Konum == BaslangicKonumu.MakineKaydi || Konum == BaslangicKonumu.Makine32Kaydi || Konum == BaslangicKonumu.OrtakKlasor; }
        }
    }

    class PilKaydi
    {
        public DateTime Tarih;
        public double Yuzde;
    }

    class SurumBilgisi
    {
        public Version Surum;
        public string Etiket, Notlar, IndirmeAdresi, Sha256;
    }

    class GecmisKaydi
    {
        public DateTime Tarih;
        public string Islem, Ayrinti, Eski, Yeni, Sonuc;
        public bool Basarili;
    }

    class IslemSonucu
    {
        public bool Basarili, YenidenBaslat;
        public string Mesaj = "";
        public string Klasor;
    }

    class KomutSonucu
    {
        public int Kod;
        public string Cikti;
    }
}
