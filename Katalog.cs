// Katalog.cs - Windows Update'in bu bilgisayara sunmadigi yeni suruculeri bulup kurar:
//   1) Microsoft Update Katalogu: her aygit donanim kimligiyle aranir; paketin destekledigi kimlikler
//      indirmeden once denetlenir, yalnizca bu aygita uyanlar onerilir.
//   2) Surucu yedekleri: USB bellek, diger diskler ve programin "Surucu Yedekleri" klasorundeki daha yeni paketler.
//
// Guvenlik kurali: kurulu surucu aygitla bilgisayara ozel bir kimlikten (SUBSYS) eslesiyorsa, yerine yalnizca
// en az o kadar ozel bir paket gecer. Tek istisna grafik, ag ve Bluetooth: bunlarda cip ureticisinin (Intel,
// NVIDIA, Realtek...) kendi yayimladigi genel surucu de kurulur (Intel'in ve NVIDIA'nin kendi araclari da boyle yapar).
// Baska bir bilgisayar ureticisinin (Compal, Dell...) paketi asla baska bilgisayara kurulmaz.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SurucuAraci
{
    static class Katalog
    {
        const string Site = "https://www.catalog.update.microsoft.com/";

        // Ureticinin genel surucusunun bilgisayara ozel olanin yerine gecebilecegi siniflar
        static readonly string[] GenelSiniflar = { "Display", "Net", "Bluetooth" };
        static readonly string[] CipUreticileri =
            { "Intel", "NVIDIA", "Advanced Micro Devices", "AMD", "Realtek", "MediaTek", "Qualcomm", "Broadcom", "Marvell", "Killer" };
        // Katalogda aranan aygit turleri; yazilim ve sanal aygitlar aranmaz
        static readonly string[] Veriyollari = { "PCI\\", "USB\\", "HDAUDIO\\", "ACPI\\", "INTELAUDIO\\" };

        class Satir
        {
            public string Id, Baslik, Urunler, Siniflandirma;
            public DateTime Tarih = DateTime.MinValue;
            public Version Surum;
            public long Boyut;
        }

        class Ayrinti
        {
            public string Yayinci = "", Saglayici = "", Sinif = "";
            public DateTime SurucuTarihi = DateTime.MinValue;
            public List<string> Kimlikler = new List<string>();
        }

        static string M(string tr, string en) { return Dil.M(tr, en); }

        // ---------- Arama ----------

        // Bulunan guncellemeleri doner; katalog hic acilamadiysa hata metni de doner
        public static List<Guncelleme> Ara(List<Aygit> aygitlar, Action<string> ilerleme, out string hata)
        {
            hata = null;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            if (ServicePointManager.DefaultConnectionLimit < 8) ServicePointManager.DefaultConnectionLimit = 8;

            List<Aygit> adaylar = aygitlar.Where(a => a.Kimlik != "" && Veriyollari.Any(v => a.Kimlik.StartsWith(v, StringComparison.OrdinalIgnoreCase))
                                                      && !string.Equals(a.Sinif, "Firmware", StringComparison.OrdinalIgnoreCase)
                                                      && AramaAnahtarlari(a).Count > 0).ToList();
            var aramalar = new ConcurrentDictionary<string, List<Satir>>(StringComparer.OrdinalIgnoreCase);
            var ayrintilar = new ConcurrentDictionary<string, Ayrinti>(StringComparer.OrdinalIgnoreCase);
            var bulunanlar = new ConcurrentBag<Guncelleme>();
            int biten = 0, basarisizArama = 0, toplamArama = 0;

            Parallel.ForEach(adaylar, new ParallelOptions { MaxDegreeOfParallelism = 6 }, a =>
            {
                // Modele ozel sonuclar (SUBSYS aramasi) ve cip sonuclari ayri degerlendirilir; yoksa populer bir cipin
                // baska markalara ait yuzlerce paketi, bu modelin paketine sira gelmeden ayrinti sinirini doldurur
                var model = new Dictionary<string, Satir>();
                var cip = new Dictionary<string, Satir>();
                foreach (string anahtar in AramaAnahtarlari(a))
                {
                    List<Satir> bulunan = aramalar.GetOrAdd(anahtar, k =>
                    {
                        Interlocked.Increment(ref toplamArama);
                        try { return KatalogdaAra(k); }
                        catch (Exception) { Interlocked.Increment(ref basarisizArama); return new List<Satir>(); }
                    });
                    var hedef = anahtar.StartsWith("SUBSYS_", StringComparison.OrdinalIgnoreCase) ? model : cip;
                    foreach (Satir s in bulunan) hedef[s.Id] = s;
                }
                try
                {
                    Guncelleme gm = EnIyiAday(a, model.Values.ToList(), ayrintilar, 60);
                    Guncelleme gc = EnIyiAday(a, cip.Values.Where(s => !model.ContainsKey(s.Id)).ToList(), ayrintilar, 25);
                    Guncelleme g = gm == null ? gc : gc == null ? gm : (SurumOku(gc.Surum) > SurumOku(gm.Surum) ? gc : gm);
                    if (g != null) bulunanlar.Add(g);
                }
                catch (Exception) { }
                int n = Interlocked.Increment(ref biten);
                if (ilerleme != null && n % 5 == 0)
                    ilerleme(string.Format(M("Microsoft Kataloğu'nda aranıyor ({0}/{1} aygıt)...", "Searching the Microsoft Catalog ({0}/{1} devices)..."), n, adaylar.Count));
            });

            if (toplamArama > 0 && basarisizArama == toplamArama)
                hata = M("Microsoft Kataloğu'na ulaşılamadı", "Couldn't reach the Microsoft Catalog");
            return bulunanlar.ToList();
        }

        // 1) SUBSYS_xxxxxxxx: bilgisayar ureticisinin tam bu model icin yukledigi suruculer. Populer bir cip (VEN&DEV)
        //    katalogda binlerce sonuc verir ve ilk sayfalar baska markalarin paketleriyle dolar; model kimligiyle aramak sart.
        // 2) PCI ve ses aygitlari VEN&DEV, USB aygitlari VID&PID, ACPI aygitlari tam kimlikle: modele ozel olmayan aygitlar icin.
        static List<string> AramaAnahtarlari(Aygit a)
        {
            var anahtarlar = new List<string>();
            foreach (string k in a.DonanimKimlikleri)
            {
                Match ss = Regex.Match(k, @"SUBSYS_[0-9A-F]{8}", RegexOptions.IgnoreCase);
                if (ss.Success && !ss.Value.EndsWith("00000000") && !anahtarlar.Contains(ss.Value.ToUpperInvariant())) anahtarlar.Add(ss.Value.ToUpperInvariant());
            }
            string ilk = a.DonanimKimlikleri.FirstOrDefault() ?? "";
            Match m = Regex.Match(ilk, @"(VEN_[0-9A-F]{4}&DEV_[0-9A-F]{4}|VID_[0-9A-F]{4}&PID_[0-9A-F]{4})", RegexOptions.IgnoreCase);
            if (m.Success) anahtarlar.Add(m.Value.ToUpperInvariant());
            else
            {
                // ACPI\PNP0C0A gibi Windows'un kendi genel aygitlari aranmaz; ACPI\INT34C5 gibi ureticiye ait olanlar aranir
                m = Regex.Match(ilk, @"^ACPI\\(?!PNP)[A-Z]{3,4}[0-9A-F]{4}$", RegexOptions.IgnoreCase);
                if (m.Success) anahtarlar.Add(m.Value.ToUpperInvariant());
            }
            return anahtarlar;
        }

        static Guncelleme EnIyiAday(Aygit a, List<Satir> satirlar, ConcurrentDictionary<string, Ayrinti> ayrintilar, int enFazla)
        {
            Version mevcut = SurumOku(a.Surum) ?? new Version(0, 0);
            var kimlikler = a.DonanimKimlikleri.Concat(a.UyumluKimlikler).ToList();
            var kucuk = kimlikler.Select(k => k.ToLowerInvariant()).ToList();
            int mevcutSira = a.EslesenKimlik == "" ? -1 : kucuk.IndexOf(a.EslesenKimlik.ToLowerInvariant());
            // Surucusu olmayan ya da eslesmesi bilinmeyen aygitta her uyan paket kabul edilir
            int mevcutDuzey = mevcutSira < 0 ? 0 : Duzey(a, mevcutSira);
            if (mevcutSira < 0) mevcutSira = int.MaxValue;
            bool genelOlabilir = GenelSiniflar.Any(s => string.Equals(s, a.Sinif, StringComparison.OrdinalIgnoreCase));

            int bakilan = 0;
            foreach (Satir s in satirlar.Where(s => s.Surum != null && s.Surum > mevcut)
                                        .OrderByDescending(s => s.Surum).ThenByDescending(s => s.Tarih))
            {
                if (++bakilan > enFazla) break;
                Ayrinti d = ayrintilar.GetOrAdd(s.Id, id =>
                {
                    try { return AyrintiAl(id); }
                    catch (Exception) { return null; }
                });
                if (d == null) continue;

                // Paketin destekledigi kimliklerden aygita en ozel uyani
                int sira = int.MaxValue;
                foreach (string k in d.Kimlikler)
                {
                    int i = kucuk.IndexOf(k.ToLowerInvariant());
                    if (i >= 0 && i < sira) sira = i;
                }
                if (sira == int.MaxValue) continue;  // bu aygita uymuyor

                // Bazi ureticiler iki ayri numaralandirma kullanir (Realtek 10.x ve 1168.x); surum buyuk ama surucu eski olabilir
                if (a.Tarih != DateTime.MinValue && d.SurucuTarihi != DateTime.MinValue && d.SurucuTarihi < a.Tarih.Date) continue;

                // En az kurulu surucu kadar ozel mi? (model > cip > uyumlu kimlik; ayni duzeyde revizyon farki onemsiz)
                int duzey = Duzey(a, sira);
                bool ozel = duzey >= mevcutDuzey;
                // Cip duzeyinde eslesen paketi ancak cip ureticisinin kendisi yayimladiysa kabul et; baska bir bilgisayar
                // markasinin (ASUS, Dell...) yukledigi paket yalnizca o markanin bilgisayarlari icindir
                if (duzey < 3 && !CipUreticisi(d.Yayinci)) continue;
                bool genel = !ozel && genelOlabilir && CipUreticisi(d.Saglayici);
                if (!ozel && !genel) continue;

                return new Guncelleme
                {
                    Kaynak = GuncellemeKaynagi.Katalog,
                    Id = s.Id,
                    Baslik = s.Baslik,
                    Surum = s.Surum.ToString(),
                    Tarih = d.SurucuTarihi != DateTime.MinValue ? d.SurucuTarihi : s.Tarih,
                    Boyut = s.Boyut,
                    Yayinci = d.Yayinci,
                    Sinif = a.Sinif,
                    Model = a.Ad,
                    AygitKimligi = a.Kimlik,
                    DonanimKimligi = kimlikler[sira],
                    KurulumKimligi = kimlikler[sira],
                    Genel = genel,
                    // Windows yalnizca kimlik sirasina bakip daha az ozel eslesen yeni paketi kendiliginden secmez
                    Zorla = genel || sira > mevcutSira
                };
            }
            return null;
        }

        // 3: bilgisayar modeline ozel donanim kimligi (SUBSYS), 2: cipe ozel donanim kimligi, 1: uyumlu kimlik
        static int Duzey(Aygit a, int sira)
        {
            if (sira >= a.DonanimKimlikleri.Length) return 1;
            return a.DonanimKimlikleri[sira].IndexOf("SUBSYS_", StringComparison.OrdinalIgnoreCase) >= 0 ? 3 : 2;
        }

        static bool CipUreticisi(string sirket)
        {
            return !string.IsNullOrEmpty(sirket) && CipUreticileri.Any(u => sirket.IndexOf(u, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static Version SurumOku(string metin)
        {
            Match m = Regex.Match(metin ?? "", @"\d+(\.\d+){1,3}");
            Version v;
            return m.Success && Version.TryParse(m.Value, out v) ? v : null;
        }

        static string Indir(string adres)
        {
            using (WebClient istemci = Guncelleyici.Istemci()) return istemci.DownloadString(adres);
        }

        static string Duz(string html)
        {
            return WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ")).Trim();
        }

        // Sonuclar hem surume hem tarihe gore siralanip alinir (katalog surumu yazi olarak siralayabildigi icin ikisi birlikte).
        // Model kimligiyle aramada 4 sayfa (100 sonuc), cip kimligiyle aramada ilk sayfa (25 sonuc) yeterli.
        static List<Satir> KatalogdaAra(string anahtar)
        {
            var satirlar = new Dictionary<string, Satir>();
            int sayfaSayisi = anahtar.StartsWith("SUBSYS_", StringComparison.OrdinalIgnoreCase) ? 4 : 1;
            foreach (string sira in new[] { "DriverVerVersion", "DateComputed" })
            for (int sayfa = 0; sayfa < sayfaSayisi; sayfa++)
            {
                string html = Indir(Site + "Search.aspx?q=" + Uri.EscapeDataString(anahtar) + "&scol=" + sira + "&sdir=desc" + (sayfa > 0 ? "&p=" + sayfa : ""));
                MatchCollection bulunan = Regex.Matches(html, "<tr id=\"([0-9a-f\\-]{36})_R\\d+\"[^>]*>(.*?)</tr>", RegexOptions.Singleline);
                foreach (Match r in bulunan)
                {
                    string id = r.Groups[1].Value;
                    if (satirlar.ContainsKey(id)) continue;
                    List<string> h = Regex.Matches(r.Groups[2].Value, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline).Cast<Match>().Select(x => Duz(x.Groups[1].Value)).ToList();
                    if (h.Count < 7) continue;
                    var s = new Satir { Id = id, Baslik = h[1], Urunler = h[2], Siniflandirma = h[3] };
                    DateTime t;
                    if (DateTime.TryParse(h[4], CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out t)) s.Tarih = t;
                    // Surum sutunu bazen "n/a"; o zaman basliktaki surum kullanilir
                    s.Surum = SurumOku(h[5]) ?? SurumOku(Regex.Match(h[1], @"\d+\.\d+\.\d+\.\d+").Value);
                    Match boyut = Regex.Match(r.Groups[2].Value, "_originalSize\">(\\d+)<");
                    if (boyut.Success) s.Boyut = long.Parse(boyut.Groups[1].Value);
                    if (!Uygun(s)) continue;
                    satirlar[id] = s;
                }
                if (bulunan.Count < 25) break;  // son sayfa
            }
            return satirlar.Values.ToList();
        }

        // Yalnizca 64 bit istemci Windows icin surucu paketleri; BIOS/UEFI gibi yazilim guncellemeleri (Firmware) asla
        static bool Uygun(Satir s)
        {
            if (s.Siniflandirma.IndexOf("Firmware", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (s.Siniflandirma.IndexOf("Driver", StringComparison.OrdinalIgnoreCase) < 0) return false;
            string metin = s.Baslik + " " + s.Urunler;
            if (metin.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return Regex.IsMatch(s.Urunler, @"Windows (11|10|- Client)", RegexOptions.IgnoreCase);
        }

        static Ayrinti AyrintiAl(string id)
        {
            string html = Indir(Site + "ScopedViewInline.aspx?updateid=" + id);
            Func<string, string> alan = ad =>
            {
                Match m = Regex.Match(html, "<span id=\"ScopedViewHandler_" + ad + "\"[^>]*>(.*?)</span>", RegexOptions.Singleline);
                return m.Success ? Duz(m.Groups[1].Value) : "";
            };
            var d = new Ayrinti { Yayinci = alan("company"), Saglayici = alan("driverProvider"), Sinif = alan("driverClass") };
            DateTime t;
            if (DateTime.TryParse(alan("versionDate"), CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out t)) d.SurucuTarihi = t;
            Match blok = Regex.Match(html, "id=\"driverhwIDs\"[^>]*>(.*?)</div>\\s*</div>", RegexOptions.Singleline);
            if (blok.Success)
                foreach (Match k in Regex.Matches(blok.Groups[1].Value, "<div[^>]*>(.*?)</div>", RegexOptions.Singleline))
                {
                    string kimlik = Duz(k.Groups[1].Value);
                    if (kimlik != "") d.Kimlikler.Add(kimlik);
                }
            return d;
        }

        // ---------- Kurulum (yonetici gerekir) ----------

        [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool UpdateDriverForPlugAndPlayDevices(IntPtr pencere, string donanimKimligi, string infYolu, uint secenekler, out bool yenidenBaslat);
        const uint INSTALLFLAG_FORCE = 1;

        public static IslemSonucu Kur(Guncelleme g, Action<string> ilerleme)
        {
            string klasor = Path.Combine(Path.GetTempPath(), "SurucuAraci", "katalog", g.Id);
            try
            {
                if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
                Directory.CreateDirectory(klasor);

                if (ilerleme != null) ilerleme(string.Format(M("{0} indiriliyor{1}...", "Downloading {0}{1}..."), g.Baslik,
                    g.Boyut > 0 ? " (" + Sistem.BoyutMetni((long)g.Boyut) + ")" : ""));
                List<string> dosyalar = PaketiIndir(g.Id, klasor);
                if (dosyalar.Count == 0)
                    return new IslemSonucu { Mesaj = M("Katalog indirme bağlantısı vermedi.", "The catalog gave no download link.") };

                if (ilerleme != null) ilerleme(string.Format(M("{0} kuruluyor...", "Installing {0}..."), g.Baslik));
                string eskiSurum = AygitSurumu(g.AygitKimligi);
                var sonuc = new IslemSonucu();
                if (!g.Zorla)
                {
                    // Windows en uygun surucuyu kendisi secer; bu paket aygita daha iyi uyuyorsa kurulur
                    sonuc.YenidenBaslat = Sistem.KlasordenYukle(klasor).YenidenBaslat;
                }
                else
                {
                    // Bilgisayara ozel surucunun yerine ureticinin genel surucusu: Windows bunu kendiliginden secmez
                    Sistem.KlasordenYukle(klasor);  // .cab'lari acar ve paketi surucu deposuna ekler
                    string inf = Directory.GetFiles(klasor, "*.inf", SearchOption.AllDirectories)
                        .FirstOrDefault(f => File.ReadAllText(f).IndexOf(g.KurulumKimligi, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (inf == null)
                        return new IslemSonucu { Mesaj = M("Paketin içinde bu aygıta uyan sürücü dosyası bulunamadı.", "No driver file in the package matches this device.") };
                    bool yb;
                    if (!UpdateDriverForPlugAndPlayDevices(IntPtr.Zero, g.KurulumKimligi, inf, INSTALLFLAG_FORCE, out yb))
                        return new IslemSonucu { Mesaj = M("Windows sürücüyü kurmadı (hata ", "Windows didn't install the driver (error ") + Marshal.GetLastWin32Error() + ")." };
                    sonuc.YenidenBaslat = yb;
                }

                string yeniSurum = AygitSurumu(g.AygitKimligi);
                Version yeni = SurumOku(yeniSurum), hedef = SurumOku(g.Surum);
                sonuc.Basarili = yeni != null && hedef != null && yeni >= hedef;
                sonuc.Mesaj = sonuc.Basarili
                    ? string.Format(M("Kuruldu: {0} → {1}", "Installed: {0} → {1}"), eskiSurum, yeniSurum)
                    : sonuc.YenidenBaslat
                        ? M("Kuruldu; yeniden başlattıktan sonra etkin olacak", "Installed; takes effect after a restart")
                        : string.Format(M("Windows mevcut sürücüyü ({0}) korudu; yeni sürücü depoya eklendi ama seçilmedi", "Windows kept the current driver ({0}); the new one was added to the store but not chosen"), eskiSurum);
                if (!sonuc.Basarili && sonuc.YenidenBaslat) sonuc.Basarili = true;
                return sonuc;
            }
            catch (Exception h)
            {
                return new IslemSonucu { Mesaj = h.Message };
            }
            finally
            {
                try { Directory.Delete(klasor, true); } catch (Exception) { }
            }
        }

        // Indirme penceresinin dondurdugu dosyalari indirir ve katalogdaki SHA-1 ozetiyle dogrular
        static List<string> PaketiIndir(string id, string klasor)
        {
            string govde = "updateIDs=" + Uri.EscapeDataString("[{\"size\":0,\"languages\":\"\",\"uidInfo\":\"" + id + "\",\"updateID\":\"" + id + "\"}]");
            string yanit;
            using (WebClient istemci = Guncelleyici.Istemci())
            {
                istemci.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                yanit = istemci.UploadString(Site + "DownloadDialog.aspx", govde);
            }
            var adresler = Regex.Matches(yanit, @"downloadInformation\[\d+\]\.files\[(\d+)\]\.url\s*=\s*'([^']+)'").Cast<Match>().ToList();
            var ozetler = Regex.Matches(yanit, @"downloadInformation\[\d+\]\.files\[(\d+)\]\.digest\s*=\s*'([^']+)'").Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

            var dosyalar = new List<string>();
            foreach (Match m in adresler)
            {
                var adres = new Uri(m.Groups[2].Value.Replace("http://", "https://"));
                // Yalnizca Microsoft'un indirme sunucularindan
                if (!adres.Host.EndsWith(".windowsupdate.com", StringComparison.OrdinalIgnoreCase) &&
                    !adres.Host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(M("Beklenmeyen indirme adresi: ", "Unexpected download address: ") + adres.Host);
                string dosya = Path.Combine(klasor, Path.GetFileName(adres.LocalPath));
                using (WebClient istemci = Guncelleyici.Istemci()) istemci.DownloadFile(adres, dosya);

                string beklenen;
                if (ozetler.TryGetValue(m.Groups[1].Value, out beklenen))
                {
                    string gercek;
                    using (var sha = SHA1.Create())
                    using (var akis = File.OpenRead(dosya)) gercek = Convert.ToBase64String(sha.ComputeHash(akis));
                    if (gercek != beklenen)
                        throw new InvalidOperationException(M("İndirilen dosya bozuk (özet tutmuyor).", "The downloaded file is corrupt (hash mismatch)."));
                }
                dosyalar.Add(dosya);
            }
            return dosyalar;
        }

        internal static string AygitSurumu(string aygitKimligi)
        {
            if (string.IsNullOrEmpty(aygitKimligi)) return "";
            string wql = "SELECT DriverVersion FROM Win32_PnPSignedDriver WHERE DeviceID = '" + aygitKimligi.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
            using (var arayici = new ManagementObjectSearcher(wql))
                foreach (ManagementObject m in arayici.Get())
                    return (m["DriverVersion"] ?? "").ToString();
            return "";
        }

        // ---------- Bulunanlari aygitlara baglama ----------

        // Katalog ve yedek guncellemelerini aygitlara baglar. Windows Update'te ayni aygit icin guncelleme varsa
        // daha yeni olani kalir; hicbir aygita baglanamayanlar (ses eklentileri gibi) ayri satir olur.
        public static void Birlestir(List<Aygit> aygitlar, IEnumerable<Guncelleme> guncellemeler)
        {
            foreach (Guncelleme g in guncellemeler)
            {
                Aygit hedef = g.AygitKimligi == "" ? null : aygitlar.FirstOrDefault(a => string.Equals(a.Kimlik, g.AygitKimligi, StringComparison.OrdinalIgnoreCase));
                if (hedef != null && hedef.Guncelleme != null)
                {
                    if (DahaYeni(g, hedef.Guncelleme)) { hedef.Guncelleme = g; continue; }
                    if (g.Kaynak != GuncellemeKaynagi.Yedek) continue;
                    hedef = null;  // ayni aygitin baska bir paketi (ses eklentisi gibi) ayri satirda gosterilir
                }
                if (hedef != null) { hedef.Guncelleme = g; continue; }
                aygitlar.Add(new Aygit { Ad = g.Model ?? g.Baslik, Sinif = g.Sinif ?? "", Surum = g.MevcutSurum, Guncelleme = g });
            }
        }

        static bool DahaYeni(Guncelleme yeni, Guncelleme eski)
        {
            if (eski.Kaynak == GuncellemeKaynagi.WindowsUpdate) return yeni.Tarih > eski.Tarih;
            return (SurumOku(yeni.Surum) ?? new Version(0, 0)) > (SurumOku(eski.Surum) ?? new Version(0, 0));
        }
    }

    // Surucu yedeklerinden (Surucu Araci'nin "Suruculeri Yedekle"si veya pnputil /export-driver) daha yeni ya da eksik paketleri bulur
    static class YedekKaynagi
    {
        static readonly string PnpUtil = Path.Combine(Environment.SystemDirectory, "pnputil.exe");
        static readonly string[] Atlanacak = { "Windows", "Program Files", "Program Files (x86)", "ProgramData", "$RECYCLE.BIN", "System Volume Information", "Users", "Recovery" };

        class Paket
        {
            public string Inf, OrijinalAd, Sinif = "", Saglayici = "";
            public Version Surum;
            public DateTime Tarih = DateTime.MinValue;
            public HashSet<string> Kimlikler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        static string M(string tr, string en) { return Dil.M(tr, en); }

        // Yedek olabilecek klasorler: Belgeler\Surucu Yedekleri ve sistem diski disindaki tum disklerin ilk uc duzeyi
        static List<string> InfDosyalari()
        {
            var infler = new List<string>();
            var kokler = new List<string>();
            foreach (string k in new[] { Sistem.YedekKlasoru, Sistem.EskiYedekKlasoru }.Distinct(StringComparer.OrdinalIgnoreCase))
                if (Directory.Exists(k)) kokler.Add(k);
            string sistemKoku = Path.GetPathRoot(Environment.SystemDirectory);
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || (d.DriveType != DriveType.Removable && d.DriveType != DriveType.Fixed)) continue;
                    if (string.Equals(d.RootDirectory.FullName, sistemKoku, StringComparison.OrdinalIgnoreCase)) continue;
                    kokler.Add(d.RootDirectory.FullName);
                }
                catch (Exception) { }
            }
            foreach (string kok in kokler) Topla(kok, 0, infler);
            return infler;
        }

        // Yedeklerde her paket kendi klasorundedir (Yedek\Suruculer\paket.inf_amd64_xxx\paket.inf); 4 duzeye kadar inilir
        static void Topla(string klasor, int duzey, List<string> infler)
        {
            if (infler.Count > 5000) return;
            try
            {
                if (duzey > 0) infler.AddRange(Directory.GetFiles(klasor, "*.inf"));
                if (duzey >= 4) return;
                foreach (string alt in Directory.GetDirectories(klasor))
                {
                    if (duzey == 0 && Atlanacak.Contains(Path.GetFileName(alt), StringComparer.OrdinalIgnoreCase)) continue;
                    Topla(alt, duzey + 1, infler);
                }
            }
            catch (Exception) { }
        }

        static Paket Oku(string inf)
        {
            string metin = File.ReadAllText(inf);  // INF'ler UTF-16 olabilir; BOM'dan anlasilir
            var p = new Paket { Inf = inf, OrijinalAd = Path.GetFileName(inf).ToLowerInvariant() };
            Match surum = Regex.Match(metin, @"^\s*DriverVer\s*=\s*([0-9/]+)\s*,\s*([0-9\.]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!surum.Success) return null;
            Version v;
            if (!Version.TryParse(surum.Groups[2].Value, out v)) return null;
            p.Surum = v;
            DateTime t;
            if (DateTime.TryParseExact(surum.Groups[1].Value, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) p.Tarih = t;
            Match sinif = Regex.Match(metin, @"^\s*Class\s*=\s*""?([A-Za-z0-9_]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (sinif.Success) p.Sinif = sinif.Groups[1].Value;
            foreach (Match k in Regex.Matches(metin, @"\b(?:PCI|USB|HDAUDIO|ACPI|INTELAUDIO|HID|SWC|BTHENUM|DISPLAY|SWD|ROOT|SD|UEFI|MONITOR)\\[^\s,;""]+", RegexOptions.IgnoreCase))
                p.Kimlikler.Add(k.Value);
            return p;
        }

        // Surucu deposundaki her paketin (ozgun adina gore) en yeni surumu
        static Dictionary<string, Version> Depo()
        {
            var depo = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
            KomutSonucu k = Sistem.Komut(PnpUtil, "/enum-drivers /format xml", Encoding.UTF8);
            int bas = k.Cikti.IndexOf("<PnpUtil", StringComparison.Ordinal);
            int son = k.Cikti.LastIndexOf("</PnpUtil>", StringComparison.Ordinal);
            if (bas < 0 || son < 0) return depo;
            foreach (XElement d in XElement.Parse(k.Cikti.Substring(bas, son - bas + "</PnpUtil>".Length)).Elements("Driver"))
            {
                string ad = (string)d.Element("OriginalName") ?? "";
                string ham = ((string)d.Element("DriverVersion") ?? "").Trim();
                Version v;
                if (ad == "" || !Version.TryParse(ham.Substring(ham.LastIndexOf(' ') + 1), out v)) continue;
                Version var;
                if (!depo.TryGetValue(ad, out var) || v > var) depo[ad] = v;
            }
            return depo;
        }

        public static List<Guncelleme> Ara(List<Aygit> aygitlar)
        {
            List<Paket> paketler = InfDosyalari().Select(f => { try { return Oku(f); } catch (Exception) { return null; } })
                .Where(p => p != null)
                .GroupBy(p => p.OrijinalAd).Select(g => g.OrderByDescending(p => p.Surum).First())  // ayni paketin birden fazla yedegi varsa en yenisi
                .ToList();
            if (paketler.Count == 0) return new List<Guncelleme>();
            Dictionary<string, Version> depo = Depo();

            var bulunanlar = new List<Guncelleme>();
            foreach (Paket p in paketler)
            {
                Aygit hedef = aygitlar.FirstOrDefault(a => a.Kimlik != "" &&
                    a.DonanimKimlikleri.Concat(a.UyumluKimlikler).Any(k => p.Kimlikler.Contains(k)));
                Version depoda;
                bool depodaVar = depo.TryGetValue(p.OrijinalAd, out depoda);
                if (depodaVar && p.Surum <= depoda) continue;    // bilgisayardaki ayni ya da daha yeni
                // Depoda hic yoksa ancak bu bilgisayardaki bir aygita uyuyorsa onerilir (formattan sonra eksik kalan ozel suruculer)
                if (!depodaVar && hedef == null) continue;
                Version aygitta = hedef != null ? Katalog.SurumOku(hedef.Surum) : null;
                if (aygitta != null && aygitta >= p.Surum && depodaVar) continue;  // aygit zaten daha yeni bir surucu kullaniyor

                bulunanlar.Add(new Guncelleme
                {
                    Kaynak = GuncellemeKaynagi.Yedek,
                    Id = "yedek:" + p.Inf,
                    Baslik = string.Format(M("{0} {1} (yedekten: {2})", "{0} {1} (from backup: {2})"), p.OrijinalAd, p.Surum, Path.GetDirectoryName(Path.GetDirectoryName(p.Inf))),
                    Surum = p.Surum.ToString(),
                    Tarih = p.Tarih,
                    Sinif = p.Sinif,
                    Model = hedef != null ? hedef.Ad : p.OrijinalAd,
                    AygitKimligi = hedef != null ? hedef.Kimlik : "",
                    MevcutSurum = depodaVar ? depoda.ToString() : "",
                    InfYolu = p.Inf
                });
            }
            return bulunanlar;
        }

        public static IslemSonucu Kur(Guncelleme g)
        {
            KomutSonucu k = Sistem.PnpUtilCalistir("/add-driver \"" + g.InfYolu + "\" /install");
            // 0: kuruldu, 3010: yeniden baslatinca, 259: depoya eklendi ama su an hicbir aygit kullanmiyor (eklentiler icin normal)
            bool basarili = k.Kod == 0 || k.Kod == 3010 || k.Kod == 259;
            return new IslemSonucu
            {
                Basarili = basarili,
                YenidenBaslat = k.Kod == 3010,
                Mesaj = basarili ? M("Yedekten kuruldu", "Installed from backup") : Sistem.SonSatirlar(k.Cikti, 3)
            };
        }
    }
}
