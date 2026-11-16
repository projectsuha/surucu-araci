// Programlar.cs - Kurulu programlarin ve NVIDIA ekran karti surucusunun guncellemelerini bulur ve kurar.
// Programlar Windows'un kendi program yukleyicisi (winget) ile, NVIDIA surucusu NVIDIA'nin kendi sunucusundan guncellenir.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace SurucuAraci
{
    enum ProgramTuru { Winget, Nvidia }

    class ProgramGuncellemesi
    {
        public ProgramTuru Tur;
        public string Ad = "", Kimlik = "", Surum = "", YeniSurum = "", Kaynak = "", Not = "";
        public string IndirmeAdresi;  // yalnizca NVIDIA surucusu icin
        public bool GuncellemeVar = true;

        // winget dar ekranda uzun kimlikleri "…" ile kisaltir; kisaltilmis kimlikle kurulum yapilamaz
        public bool Kurulabilir
        {
            get { return GuncellemeVar && (Tur == ProgramTuru.Nvidia ? IndirmeAdresi != null : !Kimlik.EndsWith("…")); }
        }
    }

    static class Programlar
    {
        // Winget komutu bulunamazsa (eski Windows 10) aciklayici hata verir
        static KomutSonucu Winget(string argumanlar)
        {
            try
            {
                return Sistem.Komut("winget.exe", argumanlar, Encoding.UTF8);
            }
            catch (Win32Exception)
            {
                throw new InvalidOperationException(Dil.M(
                    "Bu bilgisayarda Windows'un program yükleyicisi (winget) bulunamadı.\nMicrosoft Store'dan \"Uygulama Yükleyicisi\" (App Installer) uygulamasını kurup tekrar deneyin.",
                    "Windows Package Manager (winget) was not found on this PC.\nInstall \"App Installer\" from the Microsoft Store and try again."));
            }
        }

        // NVIDIA surucusu (varsa) en ustte, ardindan winget'in guncelleme buldugu programlar
        public static List<ProgramGuncellemesi> GuncellemeleriBul()
        {
            var sonuc = new List<ProgramGuncellemesi>();
            try
            {
                ProgramGuncellemesi nvidia = NvidiaDenetle();
                if (nvidia != null) sonuc.Add(nvidia);
            }
            catch (Exception) { }  // NVIDIA denetimi programlarin listelenmesini engellemesin

            KomutSonucu k = Winget("upgrade --source winget --accept-source-agreements --disable-interactivity");
            sonuc.AddRange(TabloyuOku(k.Cikti));
            return sonuc;
        }

        // winget'in metin tablosunu okur. Sutun basliklari Windows diline gore degisir (Name/Ad, Id/Kimlik...),
        // bu yuzden sutunlar adlarindan degil, baslik satirindaki konumlarindan bulunur.
        internal static List<ProgramGuncellemesi> TabloyuOku(string cikti)
        {
            var sonuc = new List<ProgramGuncellemesi>();
            // Ilerleme gostergesi satir basina donup (\r) yazildigi icin her satirin son \r'den sonraki kismi alinir
            string[] satirlar = cikti.Replace("\r\n", "\n").Split('\n').Select(s =>
            {
                int i = s.LastIndexOf('\r');
                return i >= 0 ? s.Substring(i + 1) : s;
            }).ToArray();

            for (int i = 1; i < satirlar.Length; i++)
            {
                if (!Regex.IsMatch(satirlar[i], @"^-{10,}\s*$")) continue;
                string baslik = Regex.Replace(satirlar[i - 1], @"[\x00-\x1F]", "").TrimStart(' ', '-', '\\', '|', '/');
                var konumlar = new List<int>();
                for (int j = 0; j < baslik.Length; j++)
                    if (baslik[j] != ' ' && (j == 0 || baslik[j - 1] == ' ')) konumlar.Add(j);
                if (konumlar.Count < 4) continue;

                for (int k = i + 1; k < satirlar.Length; k++)
                {
                    string s = satirlar[k];
                    // Tablo bos satirla veya "3 yükseltme mevcut." gibi kisa bir ozet satiriyla biter
                    if (s.Trim() == "" || s.Length <= konumlar[3]) break;
                    var h = new string[konumlar.Count];
                    for (int c = 0; c < konumlar.Count; c++)
                    {
                        int bas = konumlar[c], son = c + 1 < konumlar.Count ? Math.Min(konumlar[c + 1], s.Length) : s.Length;
                        h[c] = bas < s.Length && son > bas ? s.Substring(bas, son - bas).Trim() : "";
                    }
                    if (h[1] == "" || h[1].Contains(" ") || h[3] == "") continue;
                    var p = new ProgramGuncellemesi
                    {
                        Tur = ProgramTuru.Winget,
                        Ad = h[0],
                        Kimlik = h[1],
                        Surum = h[2],
                        YeniSurum = h[3],
                        Kaynak = konumlar.Count > 4 ? h[4] : "winget"
                    };
                    if (!p.Kurulabilir)
                        p.Not = Dil.M("Kimliği kısaltılmış; Windows'un Ayarlar > Uygulamalar bölümünden güncelleyin",
                                      "Its ID was shortened; update it from Windows Settings > Apps");
                    sonuc.Add(p);
                }
            }
            // Ayni program iki tabloda (normal ve "acikca hedeflenmesi gereken") gorunebilir
            return sonuc.GroupBy(p => p.Kimlik).Select(g => g.First()).ToList();
        }

        public static IslemSonucu Guncelle(ProgramGuncellemesi p)
        {
            return p.Tur == ProgramTuru.Nvidia ? NvidiaKur(p) : WingetGuncelle(p);
        }

        static IslemSonucu WingetGuncelle(ProgramGuncellemesi p)
        {
            KomutSonucu k = Winget("upgrade --id \"" + p.Kimlik + "\" -e --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity");
            if (k.Kod == 0) return new IslemSonucu { Basarili = true, Mesaj = Dil.M("Güncellendi", "Updated") };
            // 0x8A15002B: guncellenecek bir sey yok (bu arada baska yoldan guncellenmis)
            if (k.Kod == unchecked((int)0x8A15002B)) return new IslemSonucu { Basarili = true, Mesaj = Dil.M("Zaten güncel", "Already up to date") };
            string[] satirlar = k.Cikti.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 3 && !Regex.IsMatch(s, @"^[\-\\|/█▒ ]+$|^\d+(\.\d+)? [KMG]B")).ToArray();
            string ozet = string.Join(" ", satirlar.Skip(Math.Max(0, satirlar.Length - 2)));
            return new IslemSonucu { Mesaj = ozet != "" ? ozet : string.Format("winget: 0x{0:X8}", k.Kod) };
        }

        // ---------- NVIDIA ----------

        // Windows'taki surucu surumu (32.0.15.6094) NVIDIA'nin kullandigi numaraya (560.94) cevrilir
        internal static string NvidiaSurumu(string windowsSurumu)
        {
            string[] p = (windowsSurumu ?? "").Split('.');
            int ucuncu, dorduncu;
            if (p.Length != 4 || !int.TryParse(p[2], out ucuncu) || !int.TryParse(p[3], out dorduncu)) return "";
            int sayi = (ucuncu % 10) * 10000 + dorduncu;
            return (sayi / 100) + "." + (sayi % 100).ToString("00");
        }

        static decimal SurumSayisi(string surum)
        {
            decimal d;
            return decimal.TryParse(surum, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : 0;
        }

        // NVIDIA ekran karti yoksa null doner. En yeni surucu, NVIDIA uygulamasinin da sordugu
        // NVIDIA sunucusuna kartin donanim kimligiyle sorulur.
        static ProgramGuncellemesi NvidiaDenetle()
        {
            string kart = null, aygitNo = null, surum = null;
            using (var arayici = new ManagementObjectSearcher("SELECT Name, PNPDeviceID, DriverVersion FROM Win32_VideoController"))
            {
                foreach (ManagementObject m in arayici.Get())
                {
                    Match e = Regex.Match((m["PNPDeviceID"] ?? "").ToString(), @"VEN_10DE&DEV_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                    if (!e.Success) continue;
                    kart = (m["Name"] ?? "NVIDIA").ToString();
                    aygitNo = e.Groups[1].Value.ToUpperInvariant();
                    surum = NvidiaSurumu((m["DriverVersion"] ?? "").ToString());
                    break;
                }
            }
            if (kart == null) return null;

            var p = new ProgramGuncellemesi
            {
                Tur = ProgramTuru.Nvidia,
                Ad = Dil.M("NVIDIA sürücüsü · ", "NVIDIA driver · ") + kart,
                Kimlik = "NVIDIA",
                Surum = surum,
                Kaynak = "nvidia.com",
                GuncellemeVar = false
            };

            bool dizustu;
            using (var arayici = new ManagementObjectSearcher("SELECT Name FROM Win32_Battery"))
                dizustu = arayici.Get().Count > 0;
            string sorgu = "{\"dIDa\":[\"" + aygitNo + "_10DE\"],\"osC\":\"10.0\",\"osB\":\"64\",\"is6\":\"1\",\"lg\":\"1033\",\"iLp\":\"" + (dizustu ? "1" : "0") +
                           "\",\"prvMd\":\"0\",\"gcV\":\"3.28.0.417\",\"gIsB\":\"1\",\"dch\":\"1\",\"upCRD\":\"0\",\"isCRD\":\"0\"}";
            string adres = "https://gfwsl.geforce.com/nvidia_web_services/controller.gfeclientcontent.NG.php/com.nvidia.services.GFEClientContent_NG.getDispDrvrByDevid/"
                           + Uri.EscapeDataString(sorgu);
            try
            {
                string json;
                using (WebClient istemci = Guncelleyici.Istemci()) json = istemci.DownloadString(adres);
                object veri = new JavaScriptSerializer().DeserializeObject(json);
                string enYeni = Bul(veri, "Version");
                string indirme = Bul(veri, "DownloadURLAdmin") ?? Bul(veri, "DownloadURL");
                if (string.IsNullOrEmpty(enYeni) || SurumSayisi(enYeni) == 0) throw new FormatException();
                p.YeniSurum = enYeni;
                p.GuncellemeVar = SurumSayisi(enYeni) > SurumSayisi(surum);
                if (p.GuncellemeVar && GuvenliAdres(indirme)) p.IndirmeAdresi = indirme;
                p.Not = p.GuncellemeVar
                    ? (p.IndirmeAdresi != null ? Dil.M("NVIDIA'nın resmi kurulum programı indirilip açılır", "NVIDIA's official installer is downloaded and opened")
                                               : Dil.M("Üretici Kaynakları sekmesinden NVIDIA sayfasını açın", "Open NVIDIA's page from the Manufacturer Sources tab"))
                    : Dil.M("Güncel", "Up to date");
            }
            catch (Exception)
            {
                p.YeniSurum = "?";
                p.Not = Dil.M("NVIDIA'ya ulaşılamadı; Üretici Kaynakları sekmesinden NVIDIA sayfasını açın",
                              "Couldn't reach NVIDIA; open NVIDIA's page from the Manufacturer Sources tab");
            }
            return p;
        }

        // JSON icinde verilen adli ilk metin degerini arar (yanitin yapisi surumden surume degisebilir)
        static string Bul(object dugum, string anahtar)
        {
            var sozluk = dugum as IDictionary<string, object>;
            if (sozluk != null)
            {
                object deger;
                if (sozluk.TryGetValue(anahtar, out deger) && deger is string && (string)deger != "") return (string)deger;
                foreach (object alt in sozluk.Values)
                {
                    string s = Bul(alt, anahtar);
                    if (s != null) return s;
                }
                return null;
            }
            var dizi = dugum as IEnumerable;
            if (dizi != null && !(dugum is string))
                foreach (object alt in dizi)
                {
                    string s = Bul(alt, anahtar);
                    if (s != null) return s;
                }
            return null;
        }

        static bool GuvenliAdres(string adres)
        {
            Uri u;
            return adres != null && Uri.TryCreate(adres, UriKind.Absolute, out u) && u.Scheme == Uri.UriSchemeHttps
                && (u.Host.Equals("nvidia.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".nvidia.com", StringComparison.OrdinalIgnoreCase))
                && u.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        // Kurulum programini indirir, NVIDIA imzasini dogrular ve acar; kurulumu kullanici NVIDIA'nin penceresinde tamamlar
        static IslemSonucu NvidiaKur(ProgramGuncellemesi p)
        {
            string klasor = Path.Combine(Path.GetTempPath(), "SurucuAraci");
            Directory.CreateDirectory(klasor);
            string dosya = Path.Combine(klasor, Path.GetFileName(new Uri(p.IndirmeAdresi).AbsolutePath));
            using (WebClient istemci = Guncelleyici.Istemci()) istemci.DownloadFile(p.IndirmeAdresi, dosya);

            if (!NvidiaImzaliMi(dosya))
            {
                try { File.Delete(dosya); } catch (Exception) { }
                return new IslemSonucu { Mesaj = Dil.M("İndirilen dosyanın NVIDIA imzası doğrulanamadı; kurulmadı.", "The download's NVIDIA signature couldn't be verified; it wasn't installed.") };
            }
            try
            {
                Process.Start(new ProcessStartInfo(dosya) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                return new IslemSonucu { Mesaj = Dil.M("Kurulum başlatılmadı.", "The installer wasn't started.") };
            }
            return new IslemSonucu
            {
                Basarili = true,
                Mesaj = Dil.M("NVIDIA kurulumu açıldı; NVIDIA penceresindeki adımlarla tamamlayın", "NVIDIA's installer is open; finish in NVIDIA's window")
            };
        }

        // ---------- Imza dogrulama (WinVerifyTrust) ----------

        static bool NvidiaImzaliMi(string dosya)
        {
            if (!ImzaGecerli(dosya)) return false;
            try
            {
                var sertifika = new X509Certificate2(X509Certificate.CreateFromSignedFile(dosya));
                return sertifika.GetNameInfo(X509NameType.SimpleName, false).IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WintrustFileInfo
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile, pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WintrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData, pSIPClientData;
            public uint dwUIChoice, fdwRevocationChecks, dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData, pwszURLReference;
            public uint dwProvFlags, dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid eylem, ref WintrustData veri);

        static readonly Guid GenelDogrulama = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");  // WINTRUST_ACTION_GENERIC_VERIFY_V2

        static bool ImzaGecerli(string dosya)
        {
            var bilgi = new WintrustFileInfo { cbStruct = (uint)Marshal.SizeOf(typeof(WintrustFileInfo)), pcwszFilePath = dosya };
            IntPtr bilgiPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WintrustFileInfo)));
            try
            {
                Marshal.StructureToPtr(bilgi, bilgiPtr, false);
                var veri = new WintrustData
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WintrustData)),
                    dwUIChoice = 2,            // WTD_UI_NONE
                    fdwRevocationChecks = 0,   // WTD_REVOKE_NONE
                    dwUnionChoice = 1,         // WTD_CHOICE_FILE
                    pFile = bilgiPtr,
                    dwStateAction = 0,         // WTD_STATEACTION_IGNORE
                    dwProvFlags = 0x80         // WTD_REVOCATION_CHECK_NONE
                };
                return WinVerifyTrust(new IntPtr(-1), GenelDogrulama, ref veri) == 0;
            }
            finally
            {
                Marshal.DestroyStructure(bilgiPtr, typeof(WintrustFileInfo));
                Marshal.FreeHGlobal(bilgiPtr);
            }
        }
    }
}
