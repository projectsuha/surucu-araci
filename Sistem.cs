// Sistem.cs - Sistemden bilgi toplayan, guncelleme kuran, yedek alan ve temizlik yapan kisim
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SurucuAraci
{
    static class Sistem
    {
        const string Sorgu = "IsInstalled=0 and Type='Driver' and IsHidden=0";
        public static readonly string YedekKlasoru =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sürücü Yedekleri");
        static readonly string PnpUtil = Path.Combine(Environment.SystemDirectory, "pnputil.exe");

        public static bool YoneticiMi()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        static string Metin(ManagementBaseObject m, string ad)
        {
            object deger = m[ad];
            return deger == null ? "" : deger.ToString();
        }

        static T Oku<T>(Func<T> okuyucu, T varsayilan)
        {
            try
            {
                T deger = okuyucu();
                return deger == null ? varsayilan : deger;
            }
            catch (Exception)
            {
                return varsayilan;
            }
        }

        public static string BoyutMetni(long bayt)
        {
            return bayt >= 1073741824 ? (bayt / 1073741824.0).ToString("0.0") + " GB" : (bayt / 1048576.0).ToString("0.0") + " MB";
        }

        // ---------- Tarama ----------

        public static List<Aygit> AygitlariTara()
        {
            var suruculer = new Dictionary<string, ManagementBaseObject>(StringComparer.OrdinalIgnoreCase);
            using (var arayici = new ManagementObjectSearcher(
                "SELECT DeviceID, DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, InfName FROM Win32_PnPSignedDriver"))
            {
                foreach (ManagementObject m in arayici.Get())
                {
                    string kimlik = Metin(m, "DeviceID");
                    if (kimlik != "" && !suruculer.ContainsKey(kimlik)) suruculer[kimlik] = m;
                }
            }

            var aygitlar = new List<Aygit>();
            // 45 = aygit su an bagli degil
            using (var arayici = new ManagementObjectSearcher(
                "SELECT DeviceID, Name, PNPClass, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 45"))
            {
                foreach (ManagementObject m in arayici.Get())
                {
                    string kimlik = Metin(m, "DeviceID");
                    uint kod = Convert.ToUInt32(m["ConfigManagerErrorCode"]);
                    ManagementBaseObject s;
                    suruculer.TryGetValue(kimlik, out s);
                    // Surucusu olmayan sorunsuz aygitlar (sanal aygitlar vb.) listelenmez
                    if (kod == 0 && (s == null || Metin(s, "DriverVersion") == "")) continue;

                    var a = new Aygit
                    {
                        Kimlik = kimlik,
                        HataKodu = kod,
                        DonanimKimlikleri = (m["HardwareID"] as string[]) ?? new string[0]
                    };
                    a.Ad = s != null && Metin(s, "DeviceName") != "" ? Metin(s, "DeviceName") : Metin(m, "Name");
                    if (a.Ad == "") a.Ad = kimlik;
                    a.Sinif = Metin(m, "PNPClass") != "" ? Metin(m, "PNPClass") : s != null ? Metin(s, "DeviceClass") : "";
                    if (s != null)
                    {
                        a.Uretici = Metin(s, "Manufacturer");
                        a.Surum = Metin(s, "DriverVersion");
                        a.InfAdi = Metin(s, "InfName");
                        string ham = Metin(s, "DriverDate");
                        if (ham != "") a.Tarih = Oku(() => ManagementDateTimeConverter.ToDateTime(ham), DateTime.MinValue);
                    }
                    aygitlar.Add(a);
                }
            }
            return aygitlar;
        }

        public static string HataAciklamasi(uint kod)
        {
            switch (kod)
            {
                case 1: return Dil.M("Kod 1: Doğru yapılandırılmamış", "Code 1: Not configured correctly");
                case 3: return Dil.M("Kod 3: Sürücü bozuk olabilir", "Code 3: The driver may be corrupted");
                case 10: return Dil.M("Kod 10: Aygıt başlatılamıyor", "Code 10: The device cannot start");
                case 18: return Dil.M("Kod 18: Sürücüler yeniden yüklenmeli", "Code 18: Drivers need to be reinstalled");
                case 22: return Dil.M("Kod 22: Devre dışı bırakılmış", "Code 22: Disabled");
                case 24: return Dil.M("Kod 24: Aygıt yok veya düzgün çalışmıyor", "Code 24: Device not present or not working");
                case 28: return Dil.M("Kod 28: Sürücü yüklü değil", "Code 28: Driver not installed");
                case 31: return Dil.M("Kod 31: Sürücü yüklenemedi", "Code 31: The driver could not be loaded");
                case 39: return Dil.M("Kod 39: Sürücü bozuk veya eksik", "Code 39: Driver corrupted or missing");
                case 43: return Dil.M("Kod 43: Windows aygıtı durdurdu", "Code 43: Windows stopped the device");
                case 52: return Dil.M("Kod 52: Sürücü imzası doğrulanamadı", "Code 52: Driver signature could not be verified");
                default: return Dil.M("Kod ", "Code ") + kod;
            }
        }

        // ---------- Windows Update ----------

        static dynamic YeniOturum()
        {
            return Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
        }

        static dynamic Ara(dynamic oturum, string sorgu)
        {
            try
            {
                // Once Microsoft Update (daha fazla surucu iceriyor)
                dynamic arayici = oturum.CreateUpdateSearcher();
                arayici.ServiceID = "7971f918-a847-4430-9279-4a52d1efe18d";
                arayici.ServerSelection = 3;
                return arayici.Search(sorgu).Updates;
            }
            catch (COMException)
            {
                // Microsoft Update kayitli degilse varsayilan Windows Update
                dynamic arayici = oturum.CreateUpdateSearcher();
                return arayici.Search(sorgu).Updates;
            }
        }

        static List<Guncelleme> GuncellemeleriOku(dynamic bulunanlar)
        {
            var liste = new List<Guncelleme>();
            for (int i = 0; i < bulunanlar.Count; i++)
            {
                dynamic g = bulunanlar[i];
                liste.Add(new Guncelleme
                {
                    Id = (string)g.Identity.UpdateID,
                    Baslik = (string)g.Title,
                    DonanimKimligi = Oku(() => (string)g.DriverHardwareID, ""),
                    Model = Oku(() => (string)g.DriverModel, ""),
                    Sinif = Oku(() => (string)g.DriverClass, ""),
                    Tarih = Oku(() => (DateTime)g.DriverVerDate, DateTime.MinValue),
                    Boyut = Oku(() => Convert.ToDecimal((object)g.MaxDownloadSize), 0m)
                });
            }
            return liste;
        }

        public static List<Guncelleme> GuncellemeAra()
        {
            return GuncellemeleriOku(Ara(YeniOturum(), Sorgu));
        }

        public static List<Guncelleme> GizlenenleriGetir()
        {
            return GuncellemeleriOku(Ara(YeniOturum(), "IsInstalled=0 and Type='Driver' and IsHidden=1"));
        }

        // Gizlenen guncellemeyi Windows Update de otomatik kurmaz (yonetici gerekir)
        public static void GizlemeDurumu(string id, bool gizli)
        {
            dynamic bulunanlar = Ara(YeniOturum(), "IsInstalled=0 and Type='Driver'");
            for (int i = 0; i < bulunanlar.Count; i++)
            {
                dynamic g = bulunanlar[i];
                if ((string)g.Identity.UpdateID != id) continue;
                g.IsHidden = gizli;
                return;
            }
            throw new InvalidOperationException(Dil.M("Güncelleme artık bulunamadı.", "The update could not be found anymore."));
        }

        // Her guncellemeyi donanim kimligi tutan aygita baglar; eslesmeyenler ayri satir olur
        public static void Eslestir(List<Aygit> aygitlar, List<Guncelleme> guncellemeler)
        {
            var ekler = new List<Aygit>();
            foreach (Guncelleme g in guncellemeler)
            {
                Aygit hedef = g.DonanimKimligi == "" ? null : aygitlar.FirstOrDefault(a =>
                    a.DonanimKimlikleri.Any(k => string.Equals(k, g.DonanimKimligi, StringComparison.OrdinalIgnoreCase)));
                if (hedef == null)
                {
                    ekler.Add(new Aygit
                    {
                        Ad = g.Model != "" ? g.Model : g.Baslik,
                        Sinif = g.Sinif,
                        Guncelleme = g,
                        DonanimKimlikleri = g.DonanimKimligi == "" ? new string[0] : new[] { g.DonanimKimligi }
                    });
                }
                else if (hedef.Guncelleme == null || g.Tarih > hedef.Guncelleme.Tarih)
                {
                    hedef.Guncelleme = g;
                }
            }
            aygitlar.AddRange(ekler);
        }

        public static List<GuncellemeSonucu> GuncellemeKur(List<string> kimlikler, out bool yenidenBaslat)
        {
            // COM nesneleri is parcaciklari arasinda tasinmasin diye guncellemeler burada yeniden aranir
            dynamic oturum = YeniOturum();
            dynamic bulunanlar = Ara(oturum, Sorgu);
            dynamic koleksiyon = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
            for (int i = 0; i < bulunanlar.Count; i++)
            {
                dynamic g = bulunanlar[i];
                if (!kimlikler.Contains((string)g.Identity.UpdateID)) continue;
                if (!g.EulaAccepted) g.AcceptEula();
                koleksiyon.Add(g);
            }

            var sonuclar = new List<GuncellemeSonucu>();
            yenidenBaslat = false;
            if (koleksiyon.Count == 0) return sonuclar;

            dynamic indirici = oturum.CreateUpdateDownloader();
            indirici.Updates = koleksiyon;
            indirici.Download();

            dynamic kurucu = oturum.CreateUpdateInstaller();
            kurucu.Updates = koleksiyon;
            dynamic kurulum = kurucu.Install();

            for (int i = 0; i < koleksiyon.Count; i++)
            {
                sonuclar.Add(new GuncellemeSonucu
                {
                    Id = (string)koleksiyon[i].Identity.UpdateID,
                    Baslik = (string)koleksiyon[i].Title,
                    Kod = (int)kurulum.GetUpdateResult(i).ResultCode
                });
            }
            yenidenBaslat = (bool)kurulum.RebootRequired;
            return sonuclar;
        }

        // ---------- Geri yukleme noktalari (yonetici gerekir) ----------

        public static bool GeriYuklemeNoktasi(string aciklama)
        {
            try
            {
                using (var sinif = new ManagementClass(@"\\.\root\default", "SystemRestore", null))
                {
                    // 12 = MODIFY_SETTINGS, 100 = BEGIN_SYSTEM_CHANGE
                    object sonuc = sinif.InvokeMethod("CreateRestorePoint", new object[] { aciklama, 12u, 100u });
                    return Convert.ToUInt32(sonuc) == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool GeriYuklemeNoktasi()
        {
            return GeriYuklemeNoktasi(Dil.M("Sürücü Aracı - değişiklik öncesi", "Driver Tool - before changes"));
        }

        public static List<GeriNokta> GeriYuklemeNoktalari()
        {
            var noktalar = new List<GeriNokta>();
            using (var arayici = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\default"), new ObjectQuery("SELECT * FROM SystemRestore")))
            {
                foreach (ManagementObject m in arayici.Get())
                {
                    string ham = Metin(m, "CreationTime");
                    noktalar.Add(new GeriNokta
                    {
                        Sira = Convert.ToUInt32(m["SequenceNumber"]),
                        Tur = Convert.ToUInt32(m["RestorePointType"]),
                        Aciklama = Metin(m, "Description"),
                        Tarih = Oku(() => ManagementDateTimeConverter.ToDateTime(ham), DateTime.MinValue)
                    });
                }
            }
            return noktalar.OrderByDescending(n => n.Sira).ToList();
        }

        // Geri yuklemeyi hazirlar; tamamlanmasi icin bilgisayar yeniden baslatilmalidir
        public static bool GeriDon(uint sira)
        {
            using (var sinif = new ManagementClass(@"\\.\root\default", "SystemRestore", null))
                return Convert.ToUInt32(sinif.InvokeMethod("Restore", new object[] { sira })) == 0;
        }

        // ---------- Komutlar ve yedekleme ----------

        static KomutSonucu Komut(string dosya, string argumanlar, Encoding kodlama)
        {
            var bilgi = new ProcessStartInfo(dosya, argumanlar)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = kodlama,
                StandardErrorEncoding = kodlama
            };
            using (Process p = Process.Start(bilgi))
            {
                Task<string> hata = p.StandardError.ReadToEndAsync();
                string cikti = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return new KomutSonucu { Kod = p.ExitCode, Cikti = (cikti + "\n" + hata.Result).Trim() };
            }
        }

        static KomutSonucu PnpUtilCalistir(string argumanlar)
        {
            return Komut(PnpUtil, argumanlar, Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage));
        }

        static string SonSatirlar(string metin, int adet)
        {
            string[] satirlar = metin.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("\n", satirlar.Skip(Math.Max(0, satirlar.Length - adet)));
        }

        static string YeniYedekKlasoru(string ek)
        {
            string yol = Path.Combine(YedekKlasoru, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ek);
            Directory.CreateDirectory(yol);
            return yol;
        }

        static long KlasorBoyutu(string klasor)
        {
            return new DirectoryInfo(klasor).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }

        public static IslemSonucu TumunuYedekle()
        {
            string klasor = YeniYedekKlasoru(Dil.M(" tam yedek", " full backup"));
            KomutSonucu k = PnpUtilCalistir("/export-driver * \"" + klasor + "\"");
            int adet = Directory.GetFiles(klasor, "*.inf", SearchOption.AllDirectories).Length;
            if (adet == 0)
                return new IslemSonucu { Mesaj = Dil.M("Yedekleme başarısız oldu:\n", "Backup failed:\n") + SonSatirlar(k.Cikti, 6) };
            return new IslemSonucu
            {
                Basarili = true,
                Klasor = klasor,
                Mesaj = string.Format(Dil.M("{0} sürücü yedeklendi ({1}).\n\nKlasör:\n{2}", "{0} drivers backed up ({1}).\n\nFolder:\n{2}"),
                    adet, BoyutMetni(KlasorBoyutu(klasor)), klasor)
            };
        }

        // Verilen (uretici) surucu paketlerini yedekler; yedek alinmadiysa null doner
        public static string SecilenleriYedekle(IEnumerable<string> infAdlari, string ek)
        {
            List<string> infler = infAdlari.Where(OemInfMi).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (infler.Count == 0) return null;
            string klasor = YeniYedekKlasoru(ek);
            foreach (string inf in infler) PnpUtilCalistir("/export-driver " + inf + " \"" + klasor + "\"");
            if (Directory.GetFiles(klasor, "*.inf", SearchOption.AllDirectories).Length > 0) return klasor;
            Directory.Delete(klasor, true);
            return null;
        }

        static bool OemInfMi(string inf)
        {
            return inf != null && Regex.IsMatch(inf, @"^oem\d+\.inf$", RegexOptions.IgnoreCase);
        }

        // Klasordeki .cab dosyalarini acar (Microsoft Update Katalogu'ndan inenler), sonra tum .inf'leri kurar
        public static IslemSonucu KlasordenYukle(string klasor)
        {
            foreach (string cab in Directory.GetFiles(klasor, "*.cab"))
            {
                string hedef = Path.Combine(klasor, Path.GetFileNameWithoutExtension(cab));
                if (Directory.Exists(hedef)) continue;
                Directory.CreateDirectory(hedef);
                Komut(Path.Combine(Environment.SystemDirectory, "expand.exe"), "\"" + cab + "\" -F:* \"" + hedef + "\"", Encoding.Default);
            }
            if (Directory.GetFiles(klasor, "*.inf", SearchOption.AllDirectories).Length == 0)
                return new IslemSonucu { Mesaj = Dil.M("Bu klasörde yüklenecek sürücü (.inf veya .cab dosyası) bulunamadı.", "No drivers (.inf or .cab files) were found in this folder.") };

            KomutSonucu k = PnpUtilCalistir("/add-driver \"" + Path.Combine(klasor.TrimEnd('\\'), "*.inf") + "\" /subdirs /install");
            bool basarili = k.Kod == 0 || k.Kod == 3010;
            return new IslemSonucu
            {
                Basarili = basarili,
                Mesaj = (basarili ? Dil.M("Sürücüler yüklendi.", "Drivers installed.")
                        : Dil.M("Bazı sürücüler yüklenemedi veya bilgisayardakiler zaten daha yeni.", "Some drivers couldn't be installed, or the ones on this PC are already newer."))
                    + (k.Kod == 3010 ? Dil.M("\nDeğişikliklerin etkili olması için bilgisayarı yeniden başlatın.", "\nRestart the PC for the changes to take effect.") : "")
                    + "\n\n" + SonSatirlar(k.Cikti, 6)
            };
        }

        // ---------- Eski surucu temizligi ----------

        static void SurumAyir(string ham, out DateTime tarih, out Version surum)
        {
            string[] parcalar = ham.Trim().Split(' ');
            if (!DateTime.TryParseExact(parcalar[0], "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out tarih))
                tarih = DateTime.MinValue;
            if (parcalar.Length < 2 || !Version.TryParse(parcalar[parcalar.Length - 1], out surum)) surum = new Version(0, 0);
        }

        // Ayni paketin (ozgun ad + saglayici + sinif) birden fazla surumu varsa en yenisi disindakiler eskidir.
        // Bir aygitin su an kullandigi paket asla listelenmez.
        public static List<EskiSurucu> EskiSurucular()
        {
            KomutSonucu k = Komut(PnpUtil, "/enum-drivers /format xml", Encoding.UTF8);
            int bas = k.Cikti.IndexOf("<PnpUtil", StringComparison.Ordinal);
            int son = k.Cikti.LastIndexOf("</PnpUtil>", StringComparison.Ordinal);
            if (bas < 0 || son < 0)
                throw new InvalidOperationException(Dil.M("Bu Windows sürümü sürücü paketlerini listelemeyi desteklemiyor.", "This version of Windows can't list driver packages."));
            XElement kok = XElement.Parse(k.Cikti.Substring(bas, son - bas + "</PnpUtil>".Length));

            var kullanimda = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var arayici = new ManagementObjectSearcher("SELECT InfName FROM Win32_PnPSignedDriver"))
                foreach (ManagementObject m in arayici.Get()) kullanimda.Add(Metin(m, "InfName"));

            var paketler = new List<EskiSurucu>();
            foreach (XElement d in kok.Elements("Driver"))
            {
                DateTime tarih;
                Version surum;
                SurumAyir((string)d.Element("DriverVersion") ?? "", out tarih, out surum);
                paketler.Add(new EskiSurucu
                {
                    Inf = (string)d.Attribute("DriverName") ?? "",
                    OrijinalAd = (string)d.Element("OriginalName") ?? "",
                    Saglayici = (string)d.Element("ProviderName") ?? "",
                    Sinif = (string)d.Element("ClassName") ?? "",
                    SurumNo = surum,
                    Surum = surum.ToString(),
                    Tarih = tarih
                });
            }

            var eskiler = new List<EskiSurucu>();
            foreach (var grup in paketler.Where(p => OemInfMi(p.Inf))
                .GroupBy(p => (p.OrijinalAd + "|" + p.Saglayici + "|" + p.Sinif).ToLowerInvariant()).Where(g => g.Count() > 1))
            {
                EskiSurucu enYeni = grup.OrderByDescending(p => p.SurumNo).ThenByDescending(p => p.Tarih).First();
                foreach (EskiSurucu p in grup)
                {
                    if (p == enYeni || kullanimda.Contains(p.Inf)) continue;
                    p.YeniSurum = enYeni.Surum;
                    p.Boyut = PaketBoyutu(p.OrijinalAd, p.SurumNo);
                    eskiler.Add(p);
                }
            }
            return eskiler.OrderBy(p => p.Saglayici).ThenBy(p => p.OrijinalAd).ThenBy(p => p.SurumNo).ToList();
        }

        // Paketin surucu deposundaki klasorunu surum satirindan bulup boyutunu hesaplar.
        // INF'teki surum "10.29.00.9677" gibi basta sifirli olabilir; bu yuzden sayi olarak karsilastirilir.
        static long PaketBoyutu(string orijinalAd, Version surum)
        {
            try
            {
                string depo = Path.Combine(Environment.SystemDirectory, "DriverStore", "FileRepository");
                foreach (string klasor in Directory.GetDirectories(depo, orijinalAd + "_*"))
                {
                    string inf = Path.Combine(klasor, orijinalAd);
                    if (!File.Exists(inf)) continue;
                    string surumSatiri = File.ReadLines(inf).FirstOrDefault(s => s.TrimStart().StartsWith("DriverVer", StringComparison.OrdinalIgnoreCase));
                    if (surumSatiri == null) continue;
                    Version bulunan;
                    string metin = surumSatiri.Substring(surumSatiri.LastIndexOf(',') + 1).Split(';')[0].Trim();
                    if (Version.TryParse(metin, out bulunan) && bulunan == surum) return KlasorBoyutu(klasor);
                }
            }
            catch (Exception) { }
            return 0;
        }

        // /force kullanilmaz: kullanimdaki bir paket silinmez
        public static Dictionary<string, bool> SurucuPaketiSil(IEnumerable<string> infler)
        {
            var sonuc = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (string inf in infler.Where(OemInfMi)) sonuc[inf] = PnpUtilCalistir("/delete-driver " + inf).Kod == 0;
            return sonuc;
        }

        // ---------- Uretici kaynaklari ----------

        // Bilgisayar ureticilerinin resmi surucu sayfalari
        static readonly string[][] UreticiSayfalari =
        {
            new[] { "MONSTER", "https://support.monsternotebook.com/tr/drivers/" },
            new[] { "LENOVO", "https://pcsupport.lenovo.com/" },
            new[] { "HEWLETT", "https://support.hp.com/drivers" },
            new[] { "HP", "https://support.hp.com/drivers" },
            new[] { "DELL", "https://www.dell.com/support/home" },
            new[] { "ASUS", "https://www.asus.com/support/download-center/" },
            new[] { "ACER", "https://www.acer.com/support" },
            new[] { "MICRO-STAR", "https://www.msi.com/support" },
            new[] { "MSI", "https://www.msi.com/support" }
        };

        // Denetim Masasi > Programlar listesindeki adlar
        static List<string> KuruluProgramlar()
        {
            var adlar = new List<string>();
            string yol = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var anahtarlar = new[]
            {
                Registry.LocalMachine.OpenSubKey(yol),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.CurrentUser.OpenSubKey(yol)
            };
            foreach (RegistryKey anahtar in anahtarlar)
            {
                if (anahtar == null) continue;
                using (anahtar)
                {
                    foreach (string alt in anahtar.GetSubKeyNames())
                    {
                        using (RegistryKey k = anahtar.OpenSubKey(alt))
                        {
                            string ad = k == null ? null : k.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(ad)) adlar.Add(ad);
                        }
                    }
                }
            }
            return adlar;
        }

        static Kaynak AracKaynagi(string donanim, string ad, bool kurulu, string adres, string wingetId)
        {
            return new Kaynak
            {
                Donanim = donanim,
                Ad = ad,
                Kurulu = kurulu,
                Durum = kurulu ? Dil.M("Kurulu: güncellemeleri bu programdan yapın", "Installed: update from this program")
                    : wingetId != null ? Dil.M("Kurulu değil: \"Resmi Aracı Kur\" ile kurabilirsiniz", "Not installed: use \"Install Official Tool\"")
                    : Dil.M("Kurulu değil: sayfayı açıp indirin", "Not installed: open the page to download"),
                Adres = adres,
                WingetId = wingetId,
                SimgeAnahtari = kurulu ? "guncel" : "guncelleme"
            };
        }

        public static List<Kaynak> UreticiKaynaklari()
        {
            List<string> programlar = KuruluProgramlar();
            Func<string[], bool> kurulu = parcalar =>
                programlar.Any(p => parcalar.Any(x => p.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0));
            var kaynaklar = new List<Kaynak>();

            var ekranKartlari = new List<string>();
            using (var arayici = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                foreach (ManagementObject m in arayici.Get()) ekranKartlari.Add(Metin(m, "Name"));

            foreach (string kart in ekranKartlari.Where(k => k.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0))
                kaynaklar.Add(AracKaynagi(Dil.M("Ekran kartı: ", "Graphics card: ") + kart, "NVIDIA App",
                    kurulu(new[] { "NVIDIA app", "GeForce Experience" }), "https://www.nvidia.com/en-us/software/nvidia-app/", null));

            foreach (string kart in ekranKartlari.Where(k => k.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0))
                kaynaklar.Add(AracKaynagi(Dil.M("Ekran kartı: ", "Graphics card: ") + kart, "AMD Software: Adrenalin Edition",
                    kurulu(new[] { "AMD Software" }), "https://www.amd.com/en/support/download/drivers.html", null));

            bool intelVar;
            using (var arayici = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_PnPSignedDriver WHERE Manufacturer LIKE '%Intel%'"))
                intelVar = arayici.Get().Count > 0;
            if (intelVar)
                kaynaklar.Add(AracKaynagi(Dil.M("Intel parçaları (Wi-Fi, Bluetooth, grafik, yonga seti)", "Intel components (Wi-Fi, Bluetooth, graphics, chipset)"),
                    "Intel Driver & Support Assistant", kurulu(new[] { "Driver & Support Assistant" }),
                    "https://www.intel.com/content/www/us/en/support/detect.html", "Intel.IntelDriverAndSupportAssistant"));

            // Dizustune ozel suruculer (fan, tus isiklari, guc yonetimi) icin bilgisayar ureticisi
            string uretici = "", model = "";
            using (var arayici = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                foreach (ManagementObject m in arayici.Get()) { uretici = Metin(m, "Manufacturer"); model = Metin(m, "Model"); }
            string[] eslesen = UreticiSayfalari.FirstOrDefault(u => uretici.ToUpperInvariant().Contains(u[0]));
            if (uretici != "")
            {
                kaynaklar.Add(new Kaynak
                {
                    Donanim = Dil.M("Bilgisayar: ", "Computer: ") + uretici + " " + model,
                    Ad = eslesen != null ? Dil.M("Üreticinin sürücü sayfası", "Manufacturer's driver page") : Dil.M("Üreticinin destek sitesi", "Manufacturer's support site"),
                    Durum = string.Format(eslesen != null ? Dil.M("Sayfada \"{0}\" modelini arayın", "Search the page for \"{0}\"")
                        : Dil.M("Bu üretici tanınmadı: sitesinde \"{0}\" modelini arayın", "Unknown manufacturer: search their site for \"{0}\""), model),
                    Adres = eslesen != null ? eslesen[1] : ""
                });
            }
            return kaynaklar;
        }

        public static IslemSonucu WingetKur(string id)
        {
            KomutSonucu k;
            try
            {
                k = Komut("winget.exe", "install --id " + id + " -e --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity", Encoding.UTF8);
            }
            catch (Win32Exception)
            {
                return new IslemSonucu
                {
                    Mesaj = Dil.M("Bu bilgisayarda Windows'un program yükleyicisi (winget) bulunamadı.\n\"Sayfayı Aç\" ile resmi sayfadan indirebilirsiniz.",
                        "Windows Package Manager (winget) was not found on this PC.\nUse \"Open Page\" to download from the official site.")
                };
            }
            if (k.Kod == 0) return new IslemSonucu { Basarili = true, Mesaj = Dil.M("Kurulum tamamlandı.", "Installation complete.") };
            return new IslemSonucu { Mesaj = Dil.M("Kurulum tamamlanamadı:\n\n", "Installation failed:\n\n") + SonSatirlar(k.Cikti, 6) };
        }

        // Microsoft Update Katalogu'nda aygitin donanim kimligiyle arama adresi
        public static string KatalogAdresi(Aygit a)
        {
            if (a.DonanimKimlikleri.Length == 0) return "";
            string kimlik = a.DonanimKimlikleri[0];
            Match m = Regex.Match(kimlik, @"(VEN_[0-9A-F]{4}&DEV_[0-9A-F]{4}|VID_[0-9A-F]{4}&PID_[0-9A-F]{4})", RegexOptions.IgnoreCase);
            return "https://www.catalog.update.microsoft.com/Search.aspx?q=" + Uri.EscapeDataString(m.Success ? m.Value : kimlik);
        }

        // ---------- Windows ile baslatma ----------

        public static bool BaslangictaCalisir
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Ortak.BaslangicAnahtari))
                    return k != null && k.GetValue(Ortak.BaslangicDegeri) != null;
            }
            set
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Ortak.BaslangicAnahtari))
                {
                    if (value) k.SetValue(Ortak.BaslangicDegeri, "\"" + Application.ExecutablePath + "\" /tepsi");
                    else k.DeleteValue(Ortak.BaslangicDegeri, false);
                }
            }
        }
    }
}
