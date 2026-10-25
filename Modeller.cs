// Modeller.cs - Programin tasidigi veriler
using System;

namespace SurucuAraci
{
    enum Durum { Sorunlu, GuncellemeVar, Guncel }

    class Guncelleme
    {
        public string Id, Baslik, DonanimKimligi, Model, Sinif;
        public DateTime Tarih = DateTime.MinValue;
        public decimal Boyut;
    }

    class Aygit
    {
        public string Ad = "", Sinif = "", Uretici = "", Surum = "", InfAdi = "", Kimlik = "";
        public DateTime Tarih = DateTime.MinValue;
        public string[] DonanimKimlikleri = new string[0];
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
        public bool Basarili;
        public string Mesaj = "";
        public string Klasor;
    }

    class KomutSonucu
    {
        public int Kod;
        public string Cikti;
    }
}
