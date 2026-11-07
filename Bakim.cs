// Bakim.cs - Otomatik koruma (zamanlanmis gorev), disk temizligi, baslangic programlari ve pil gecmisi
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SurucuAraci
{
    static class Bakim
    {
        static string M(string tr, string en) { return Dil.M(tr, en); }

        // ---------- Otomatik koruma ----------

        const string GorevAdi = @"\SurucuAraci\Otomatik Koruma";
        static readonly string SchTasks = Path.Combine(Environment.SystemDirectory, "schtasks.exe");

        public static bool KorumaGoreviVar()
        {
            try { return Sistem.Komut(SchTasks, "/Query /TN \"" + GorevAdi + "\"", Encoding.Default).Kod == 0; }
            catch (Exception) { return false; }
        }

        // Gorev yonetici yetkisiyle calisacagi icin yonetici olarak kaydedilir; yalnizca bu islem icin izin (UAC) penceresi acilir
        public static bool KorumaGoreviniAyarla(bool acik)
        {
            string xml = null;
            string argumanlar = "/Delete /TN \"" + GorevAdi + "\" /F";
            if (acik)
            {
                xml = Path.Combine(Path.GetTempPath(), "SurucuAraci_gorev_" + Guid.NewGuid().ToString("N") + ".xml");
                File.WriteAllText(xml, GorevTanimi(), Encoding.Unicode);
                argumanlar = "/Create /TN \"" + GorevAdi + "\" /XML \"" + xml + "\" /F";
            }
            try
            {
                var bilgi = new ProcessStartInfo(SchTasks, argumanlar) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
                if (!Sistem.YoneticiMi()) bilgi.Verb = "runas";
                using (Process p = Process.Start(bilgi))
                {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (Win32Exception)
            {
                return false;  // kullanici UAC penceresinde Hayir dedi
            }
            finally
            {
                if (xml != null) try { File.Delete(xml); } catch (Exception) { }
            }
        }

        // Her pazar 12:00; bilgisayar kapaliysa ilk firsatta calisir. Kararlari (6 gun / 28 gun) program kendisi verir.
        static string GorevTanimi()
        {
            Func<string, string> k = SecurityElement.Escape;
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\n" +
                "  <RegistrationInfo><Description>" + k(M("Sürücü Aracı: haftalık geri yükleme noktası ve aylık sürücü yedeği",
                                                           "Driver Tool: weekly restore point and monthly driver backup")) + "</Description></RegistrationInfo>\n" +
                "  <Triggers><CalendarTrigger><StartBoundary>" + DateTime.Today.AddHours(12).ToString("s") + "</StartBoundary>" +
                "<ScheduleByWeek><DaysOfWeek><Sunday /></DaysOfWeek><WeeksInterval>1</WeeksInterval></ScheduleByWeek></CalendarTrigger></Triggers>\n" +
                "  <Principals><Principal id=\"Author\"><UserId>" + k(WindowsIdentity.GetCurrent().Name) + "</UserId>" +
                "<LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\n" +
                "  <Settings><StartWhenAvailable>true</StartWhenAvailable><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT2H</ExecutionTimeLimit>" +
                "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy></Settings>\n" +
                "  <Actions Context=\"Author\"><Exec><Command>" + k(Application.ExecutablePath) + "</Command>" +
                "<Arguments>/otomatik-koruma</Arguments></Exec></Actions>\n" +
                "</Task>";
        }

        // Zamanlanmis gorevin calistirdigi kisim (pencere acilmaz, yonetici olarak calisir)
        public static void OtomatikKorumaCalistir()
        {
            if (DateTime.Now - Ayarlar.SonOtomatikNokta >= TimeSpan.FromDays(6))
            {
                bool basarili = Sistem.GeriYuklemeNoktasi(M("Sürücü Aracı - haftalık otomatik", "Driver Tool - weekly automatic"));
                Gecmis.Ekle("nokta", M("Haftalık otomatik koruma", "Weekly automatic protection"), "", "",
                    basarili ? M("Oluşturuldu", "Created") : M("Oluşturulamadı", "Failed"), basarili);
                if (basarili) Ayarlar.SonOtomatikNokta = DateTime.Now;
            }
            if (DateTime.Now - Ayarlar.SonOtomatikYedek >= TimeSpan.FromDays(28))
            {
                IslemSonucu sonuc = Sistem.TumunuYedekle(M(" otomatik yedek", " automatic backup"));
                Gecmis.Ekle("yedek", sonuc.Klasor ?? "", "", "", sonuc.Basarili ? M("Otomatik yedek alındı", "Automatic backup created") : M("Başarısız", "Failed"), sonuc.Basarili);
                if (sonuc.Basarili)
                {
                    Ayarlar.SonOtomatikYedek = DateTime.Now;
                    EskiOtomatikYedekleriSil();
                }
            }
        }

        // Disk dolmasin diye yalnizca son iki otomatik yedek tutulur; elle alinan yedeklere dokunulmaz
        static void EskiOtomatikYedekleriSil()
        {
            try
            {
                IEnumerable<string> eskiler = Directory.GetDirectories(Sistem.YedekKlasoru)
                    .Where(k => k.EndsWith(" otomatik yedek") || k.EndsWith(" automatic backup"))
                    .OrderByDescending(k => Path.GetFileName(k)).Skip(2);
                foreach (string klasor in eskiler) Directory.Delete(klasor, true);
            }
            catch (Exception) { }
        }

        // ---------- Disk temizligi ----------

        [StructLayout(LayoutKind.Sequential)]
        struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHQueryRecycleBin(string kok, ref SHQUERYRBINFO bilgi);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr pencere, string kok, uint bayraklar);

        static IEnumerable<string> KalemKlasorleri(string anahtar)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string ortakVeri = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string yerelVeri = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            switch (anahtar)
            {
                case "kullanici-temp": return new[] { Path.GetTempPath() };
                case "windows-temp": return new[] { Path.Combine(windows, "Temp") };
                case "wu-onbellek": return new[] { Path.Combine(windows, @"SoftwareDistribution\Download") };
                case "teslim": return new[] { Path.Combine(windows, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") };
                case "hata-raporlari":
                    return new[]
                    {
                        Path.Combine(ortakVeri, @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(ortakVeri, @"Microsoft\Windows\WER\ReportQueue"),
                        Path.Combine(yerelVeri, @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(yerelVeri, @"Microsoft\Windows\WER\ReportQueue")
                    };
                default: return new string[0];
            }
        }

        // Erisilemeyen klasorler ve baglanti noktalari (junction) atlanir.
        // Gecici klasorlerde son bir gunde degisen dosyalara dokunulmaz; calisan bir programa ait olabilirler.
        static List<FileInfo> Dosyalar(string anahtar)
        {
            var dosyalar = new List<FileInfo>();
            DateTime sinir = DateTime.Now.AddDays(-1);
            bool yeniOlaniAtla = anahtar == "kullanici-temp" || anahtar == "windows-temp";
            foreach (string kok in KalemKlasorleri(anahtar))
            {
                if (!Directory.Exists(kok)) continue;
                var bekleyen = new Stack<DirectoryInfo>();
                bekleyen.Push(new DirectoryInfo(kok));
                while (bekleyen.Count > 0)
                {
                    DirectoryInfo klasor = bekleyen.Pop();
                    try
                    {
                        foreach (DirectoryInfo alt in klasor.GetDirectories())
                            if ((alt.Attributes & FileAttributes.ReparsePoint) == 0) bekleyen.Push(alt);
                        foreach (FileInfo dosya in klasor.GetFiles())
                            if (!yeniOlaniAtla || dosya.LastWriteTime < sinir) dosyalar.Add(dosya);
                    }
                    catch (Exception) { }
                }
            }
            return dosyalar;
        }

        static TemizlikKalemi Kalem(string anahtar, string ad, string aciklama, bool yonetici)
        {
            List<FileInfo> dosyalar = Dosyalar(anahtar);
            return new TemizlikKalemi
            {
                Anahtar = anahtar,
                Ad = ad,
                Aciklama = aciklama,
                Konum = KalemKlasorleri(anahtar).First(),
                Boyut = dosyalar.Sum(d => d.Length),
                DosyaSayisi = dosyalar.Count,
                YoneticiGerekir = yonetici
            };
        }

        static long GeriDonusumBoyutu(out long adet)
        {
            var bilgi = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
            adet = 0;
            if (SHQueryRecycleBin(null, ref bilgi) != 0) return 0;
            adet = bilgi.i64NumItems;
            return bilgi.i64Size;
        }

        public static List<TemizlikKalemi> TemizlikKalemleri()
        {
            var kalemler = new List<TemizlikKalemi>
            {
                Kalem("kullanici-temp", M("Geçici dosyalar", "Temporary files"),
                    M("Programların bıraktığı, bir günden eski geçici dosyalar", "Temporary files older than a day left by programs"), false),
                Kalem("windows-temp", M("Windows geçici dosyaları", "Windows temporary files"),
                    M("Windows'un bir günden eski geçici dosyaları", "Windows temporary files older than a day"), true),
                Kalem("wu-onbellek", M("Windows Update önbelleği", "Windows Update cache"),
                    M("Kurulmuş güncellemelerin indirme dosyaları; gerekirse Windows yeniden indirir", "Download files of installed updates; Windows downloads them again if needed"), true),
                Kalem("teslim", M("Teslim İyileştirme önbelleği", "Delivery Optimization cache"),
                    M("Güncellemeleri başka bilgisayarlarla paylaşmak için tutulan kopyalar", "Copies kept to share updates with other PCs"), true),
                Kalem("hata-raporlari", M("Hata raporları", "Error reports"),
                    M("Çöken programlar için Windows'un hazırladığı raporlar", "Reports Windows prepared for crashed programs"), false)
            };
            long adet;
            long boyut = GeriDonusumBoyutu(out adet);
            kalemler.Add(new TemizlikKalemi
            {
                Anahtar = "geri-donusum",
                Ad = M("Geri Dönüşüm Kutusu", "Recycle Bin"),
                Konum = M("Tüm sürücüler", "All drives"),
                Aciklama = M("İçindeki dosyalar kalıcı olarak silinir, geri alınamaz", "The files in it are permanently deleted and can't be recovered"),
                Boyut = boyut,
                DosyaSayisi = (int)Math.Min(int.MaxValue, adet),
                VarsayilanSecili = false
            });
            return kalemler;
        }

        // Silinemeyen (kullanimdaki) dosyalar atlanir; bosaltilan bayt sayisini doner
        public static long Temizle(IEnumerable<string> anahtarlar)
        {
            long bosaltilan = 0;
            foreach (string anahtar in anahtarlar)
            {
                if (anahtar == "geri-donusum")
                {
                    long adet;
                    long boyut = GeriDonusumBoyutu(out adet);
                    // 7 = onay sorma, ilerleme gosterme, ses calma
                    if (adet > 0 && SHEmptyRecycleBin(IntPtr.Zero, null, 7) == 0) bosaltilan += boyut;
                    continue;
                }
                foreach (FileInfo dosya in Dosyalar(anahtar))
                {
                    try
                    {
                        long boyut = dosya.Length;
                        if ((dosya.Attributes & FileAttributes.ReadOnly) != 0) dosya.Attributes = FileAttributes.Normal;
                        dosya.Delete();
                        bosaltilan += boyut;
                    }
                    catch (Exception) { }
                }
                foreach (string kok in KalemKlasorleri(anahtar)) BosKlasorleriSil(kok, true);
            }
            return bosaltilan;
        }

        static void BosKlasorleriSil(string klasor, bool kok)
        {
            try
            {
                if (!Directory.Exists(klasor)) return;
                foreach (string alt in Directory.GetDirectories(klasor))
                    if ((File.GetAttributes(alt) & FileAttributes.ReparsePoint) == 0) BosKlasorleriSil(alt, false);
                if (!kok && !Directory.EnumerateFileSystemEntries(klasor).Any()) Directory.Delete(klasor);
            }
            catch (Exception) { }
        }

        // ---------- Baslangic programlari ----------

        const string RunYolu = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Run32Yolu = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        const string OnayYolu = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        static RegistryKey Kok(BaslangicKonumu konum)
        {
            return konum == BaslangicKonumu.KullaniciKaydi || konum == BaslangicKonumu.KullaniciKlasoru ? Registry.CurrentUser : Registry.LocalMachine;
        }

        // Gorev Yoneticisi'nin acik/kapali bilgisini tuttugu yer
        static string OnayAnahtari(BaslangicKonumu konum)
        {
            if (konum == BaslangicKonumu.Makine32Kaydi) return OnayYolu + "Run32";
            if (konum == BaslangicKonumu.KullaniciKlasoru || konum == BaslangicKonumu.OrtakKlasor) return OnayYolu + "StartupFolder";
            return OnayYolu + "Run";
        }

        public static string KonumAdi(BaslangicKonumu konum)
        {
            switch (konum)
            {
                case BaslangicKonumu.KullaniciKaydi: return M("Bu kullanıcı", "This user");
                case BaslangicKonumu.KullaniciKlasoru: return M("Başlangıç klasörü (bu kullanıcı)", "Startup folder (this user)");
                case BaslangicKonumu.OrtakKlasor: return M("Başlangıç klasörü (tüm kullanıcılar)", "Startup folder (all users)");
                default: return M("Tüm kullanıcılar", "All users");
            }
        }

        public static List<BaslangicOgesi> BaslangicProgramlari()
        {
            var ogeler = new List<BaslangicOgesi>();
            KayittanOku(ogeler, Registry.CurrentUser, RunYolu, BaslangicKonumu.KullaniciKaydi);
            KayittanOku(ogeler, Registry.LocalMachine, RunYolu, BaslangicKonumu.MakineKaydi);
            KayittanOku(ogeler, Registry.LocalMachine, Run32Yolu, BaslangicKonumu.Makine32Kaydi);
            KlasordenOku(ogeler, Environment.GetFolderPath(Environment.SpecialFolder.Startup), BaslangicKonumu.KullaniciKlasoru);
            KlasordenOku(ogeler, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), BaslangicKonumu.OrtakKlasor);
            foreach (BaslangicOgesi o in ogeler) o.Etkin = EtkinMi(o);
            return ogeler.OrderBy(o => o.Ad, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static void KayittanOku(List<BaslangicOgesi> ogeler, RegistryKey kok, string yol, BaslangicKonumu konum)
        {
            using (RegistryKey k = kok.OpenSubKey(yol))
            {
                if (k == null) return;
                foreach (string ad in k.GetValueNames())
                {
                    string komut = k.GetValue(ad) as string;
                    if (ad != "" && !string.IsNullOrEmpty(komut))
                        ogeler.Add(new BaslangicOgesi { Ad = ad, Anahtar = ad, Komut = komut, Konum = konum });
                }
            }
        }

        static void KlasordenOku(List<BaslangicOgesi> ogeler, string klasor, BaslangicKonumu konum)
        {
            if (!Directory.Exists(klasor)) return;
            foreach (string dosya in Directory.GetFiles(klasor))
            {
                string ad = Path.GetFileName(dosya);
                if (!ad.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                    ogeler.Add(new BaslangicOgesi { Ad = Path.GetFileNameWithoutExtension(dosya), Anahtar = ad, Komut = dosya, Konum = konum });
            }
        }

        // Kayit yoksa acik sayilir; ilk baytin son biti 1 ise kapali (Gorev Yoneticisi 02 = acik, 03 = kapali yazar)
        static bool EtkinMi(BaslangicOgesi o)
        {
            try
            {
                using (RegistryKey k = Kok(o.Konum).OpenSubKey(OnayAnahtari(o.Konum)))
                {
                    byte[] deger = k == null ? null : k.GetValue(o.Anahtar) as byte[];
                    return deger == null || deger.Length == 0 || (deger[0] & 1) == 0;
                }
            }
            catch (Exception)
            {
                return true;
            }
        }

        // Gorev Yoneticisi > Baslangic sekmesiyle ayni yontem: programin kaydi silinmez, yalnizca acik/kapali isaretlenir
        public static void BaslangicDurumu(BaslangicOgesi o, bool etkin)
        {
            var deger = new byte[12];
            deger[0] = (byte)(etkin ? 2 : 3);
            if (!etkin) BitConverter.GetBytes(DateTime.Now.ToFileTime()).CopyTo(deger, 4);
            using (RegistryKey k = Kok(o.Konum).CreateSubKey(OnayAnahtari(o.Konum)))
                k.SetValue(o.Anahtar, deger, RegistryValueKind.Binary);
            o.Etkin = etkin;
        }

        // ---------- Pil gecmisi ----------

        // Windows'un pil raporundaki gecmis: her kayit icin tam dolu kapasitenin ilk gunkune orani
        public static List<PilKaydi> PilGecmisi()
        {
            var kayitlar = new List<PilKaydi>();
            string dosya = Path.Combine(Path.GetTempPath(), "SurucuAraci_pilgecmisi_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                Sistem.Komut(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), "/batteryreport /xml /output \"" + dosya + "\"", Encoding.Default);
                if (!File.Exists(dosya)) return kayitlar;
                foreach (XElement e in XElement.Load(dosya).Descendants().Where(x => x.Name.LocalName == "HistoryEntry"))
                {
                    double tasarim, tam;
                    DateTime tarih;
                    if (!double.TryParse((string)e.Attribute("DesignCapacity"), NumberStyles.Any, CultureInfo.InvariantCulture, out tasarim) ||
                        !double.TryParse((string)e.Attribute("FullChargeCapacity"), NumberStyles.Any, CultureInfo.InvariantCulture, out tam) ||
                        !DateTime.TryParse((string)e.Attribute("LocalEndDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out tarih) ||
                        tasarim <= 0 || tam <= 0) continue;
                    kayitlar.Add(new PilKaydi { Tarih = tarih, Yuzde = Math.Min(100, tam / tasarim * 100) });
                }
            }
            catch (Exception) { }
            finally
            {
                try { File.Delete(dosya); } catch (Exception) { }
            }
            return kayitlar.OrderBy(k => k.Tarih).ToList();
        }
    }
}
