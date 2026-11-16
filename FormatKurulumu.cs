// FormatKurulumu.cs - Windows yeniden kurulduktan sonra butun suruculeri tek seferde en yeni resmi surumlerine getirir:
// Windows Update'teki tum suruculer (istege bagli olanlar dahil), Intel'in resmi araci, NVIDIA'nin en yeni surucusu
// ve bilgisayar ureticisinin surucu sayfasi.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SurucuAraci
{
    class FormatSonucu
    {
        public readonly List<string> Yapilanlar = new List<string>();
        public readonly List<string> Elle = new List<string>();  // kullanicinin tamamlamasi gerekenler
        public readonly List<string> AcilacakSayfalar = new List<string>();
        public int Kurulan, Basarisiz;
        public bool YenidenBaslat;
    }

    static class FormatKurulumu
    {
        const int EnFazlaTur = 3;
        const string IntelArac = "Intel.IntelDriverAndSupportAssistant";
        const string IntelTaramaSayfasi = "https://www.intel.com/content/www/us/en/support/detect.html";

        static string M(string tr, string en) { return Dil.M(tr, en); }

        // ilerleme: her adimda durum cubugunda gosterilecek metin. Yonetici yetkisiyle cagrilmalidir.
        public static FormatSonucu Calistir(Action<string> ilerleme, Func<int, string> sonucMetni)
        {
            var r = new FormatSonucu();

            ilerleme(M("Geri yükleme noktası oluşturuluyor...", "Creating a restore point..."));
            r.Yapilanlar.Add(Sistem.GeriYuklemeNoktasi(M("Sürücü Aracı - format sonrası kurulum öncesi", "Driver Tool - before post-install setup"))
                ? M("✓ Geri yükleme noktası oluşturuldu", "✓ Restore point created")
                : M("• Geri yükleme noktası oluşturulamadı (Sistem Koruması kapalı olabilir)", "• Couldn't create a restore point (System Protection may be off)"));

            WindowsUpdate(r, ilerleme, sonucMetni);

            List<Kaynak> kaynaklar;
            try { kaynaklar = Sistem.UreticiKaynaklari(); }
            catch (Exception) { kaynaklar = new List<Kaynak>(); }
            Intel(r, kaynaklar, ilerleme);
            Nvidia(r, ilerleme);

            Kaynak bilgisayar = kaynaklar.FirstOrDefault(k => k.Bilgisayar);
            if (bilgisayar != null)
            {
                if (bilgisayar.Adres != "") r.AcilacakSayfalar.Add(bilgisayar.Adres);
                r.Elle.Add(string.Format(M("{0}: dizüstüne özel yazılımlar (Control Center, klavye ışığı, fan ayarları) için üreticinin sayfası açılacak. {1}",
                                           "{0}: the maker's page will open for laptop-specific software (Control Center, keyboard light, fan settings). {1}"),
                                         bilgisayar.Donanim, bilgisayar.Durum));
            }
            return r;
        }

        // Bir surucu kurulunca baska aygitlar icin yeni suruculer gorunebilir (once yonga seti, sonra digerleri);
        // bu yuzden yeni bir sey cikmayana kadar birkac tur aranir.
        static void WindowsUpdate(FormatSonucu r, Action<string> ilerleme, Func<int, string> sonucMetni)
        {
            var denenen = new HashSet<string>();
            for (int tur = 1; tur <= EnFazlaTur; tur++)
            {
                List<Guncelleme> yeni;
                try
                {
                    ilerleme(string.Format(M("Windows Update'te sürücüler aranıyor ({0}. tur)...", "Searching Windows Update for drivers (round {0})..."), tur));
                    yeni = Sistem.GuncellemeAra().Where(g => !denenen.Contains(g.Id)).ToList();
                }
                catch (Exception h)
                {
                    r.Yapilanlar.Add(M("✗ Windows Update'e ulaşılamadı: ", "✗ Couldn't reach Windows Update: ") + h.Message);
                    return;
                }
                if (yeni.Count == 0) break;
                foreach (Guncelleme g in yeni) denenen.Add(g.Id);

                ilerleme(string.Format(M("{0} sürücü indirilip kuruluyor ({1}. tur, birkaç dakika sürebilir)...",
                                         "Downloading and installing {0} drivers (round {1}, may take a few minutes)..."), yeni.Count, tur));
                try
                {
                    bool yenidenBaslat;
                    foreach (GuncellemeSonucu s in Sistem.GuncellemeKur(yeni.Select(g => g.Id).ToList(), out yenidenBaslat))
                    {
                        Gecmis.Ekle("guncelleme", s.Baslik, "", s.Baslik, sonucMetni(s.Kod), s.Basarili);
                        if (s.Basarili) r.Kurulan++; else r.Basarisiz++;
                        r.Yapilanlar.Add((s.Basarili ? "✓ " : "✗ ") + s.Baslik);
                    }
                    r.YenidenBaslat |= yenidenBaslat;
                }
                catch (Exception h)
                {
                    r.Basarisiz += yeni.Count;
                    r.Yapilanlar.Add(M("✗ Kurulum yarıda kaldı: ", "✗ Installation stopped: ") + h.Message);
                }
            }
            if (denenen.Count == 0) r.Yapilanlar.Add(M("✓ Windows Update'te kurulacak yeni sürücü yok", "✓ No new drivers on Windows Update"));
        }

        static void Intel(FormatSonucu r, List<Kaynak> kaynaklar, Action<string> ilerleme)
        {
            Kaynak intel = kaynaklar.FirstOrDefault(k => k.WingetId == IntelArac);
            if (intel == null) return;  // Intel parcasi yok
            if (!intel.Kurulu)
            {
                ilerleme(M("Intel Driver & Support Assistant kuruluyor...", "Installing Intel Driver & Support Assistant..."));
                IslemSonucu s = Sistem.WingetKur(IntelArac);
                Gecmis.Ekle("arac", intel.Ad, "", IntelArac, s.Basarili ? M("Kuruldu", "Installed") : M("Başarısız", "Failed"), s.Basarili);
                if (!s.Basarili)
                {
                    r.Yapilanlar.Add(M("✗ Intel aracı kurulamadı", "✗ Couldn't install the Intel tool"));
                    r.Elle.Add(M("Intel: Üretici Kaynakları sekmesinden Intel'in sayfasını açıp aracı elle kurun.",
                                 "Intel: open Intel's page from the Manufacturer Sources tab and install the tool manually."));
                    return;
                }
                r.Yapilanlar.Add(M("✓ Intel Driver & Support Assistant kuruldu", "✓ Intel Driver & Support Assistant installed"));
            }
            r.AcilacakSayfalar.Add(IntelTaramaSayfasi);
            r.Elle.Add(M("Intel: açılan Intel sayfası bilgisayarınızı tarar; Wi-Fi, Bluetooth, grafik ve yonga seti için çıkan güncellemeleri oradan kurun (Intel en yenilerini yalnızca kendi aracıyla dağıtır).",
                         "Intel: the Intel page that opens scans your PC; install the Wi-Fi, Bluetooth, graphics and chipset updates it finds (Intel only distributes its newest drivers through its own tool)."));
        }

        static void Nvidia(FormatSonucu r, Action<string> ilerleme)
        {
            ProgramGuncellemesi p;
            try
            {
                ilerleme(M("NVIDIA'da en yeni sürücü denetleniyor...", "Checking NVIDIA for the newest driver..."));
                p = Programlar.NvidiaDenetle();
            }
            catch (Exception) { p = null; }
            if (p == null) return;  // NVIDIA kart yok

            if (!p.GuncellemeVar)
            {
                r.Yapilanlar.Add(p.YeniSurum == "?"
                    ? M("✗ NVIDIA'ya ulaşılamadı", "✗ Couldn't reach NVIDIA")
                    : string.Format(M("✓ NVIDIA sürücüsü güncel ({0})", "✓ NVIDIA driver is up to date ({0})"), p.Surum));
                if (p.YeniSurum == "?")
                    r.Elle.Add(M("NVIDIA: Üretici Kaynakları sekmesinden NVIDIA sayfasını açıp sürücüyü elle indirin.",
                                 "NVIDIA: open NVIDIA's page from the Manufacturer Sources tab and download the driver manually."));
                return;
            }
            if (!p.Kurulabilir)
            {
                r.Elle.Add(string.Format(M("NVIDIA: yeni sürücü var ({0} → {1}); Üretici Kaynakları sekmesinden NVIDIA sayfasını açın.",
                                           "NVIDIA: a new driver is available ({0} → {1}); open NVIDIA's page from the Manufacturer Sources tab."), p.Surum, p.YeniSurum));
                return;
            }
            ilerleme(string.Format(M("NVIDIA {0} sürücüsü indiriliyor (yaklaşık 700 MB)...", "Downloading NVIDIA driver {0} (about 700 MB)..."), p.YeniSurum));
            IslemSonucu s;
            try { s = Programlar.Guncelle(p); }
            catch (Exception h) { s = new IslemSonucu { Mesaj = h.Message }; }
            Gecmis.Ekle("uygulama", p.Ad, p.Surum, p.YeniSurum, s.Mesaj, s.Basarili);
            if (s.Basarili)
            {
                r.Yapilanlar.Add(string.Format(M("✓ NVIDIA {0} sürücüsü indirildi ve kurulumu açıldı", "✓ NVIDIA driver {0} downloaded and its installer opened"), p.YeniSurum));
                r.Elle.Add(M("NVIDIA: açılan NVIDIA kurulum penceresinde adımları tamamlayın (ekran birkaç kez kararabilir).",
                             "NVIDIA: finish the steps in NVIDIA's installer window (the screen may flicker a few times)."));
            }
            else
            {
                r.Yapilanlar.Add(M("✗ NVIDIA sürücüsü kurulamadı: ", "✗ Couldn't install the NVIDIA driver: ") + s.Mesaj);
            }
        }
    }
}
