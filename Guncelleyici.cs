// Guncelleyici.cs - Programin yeni surumunu GitHub Releases sayfasindan denetler ve kurar
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SurucuAraci
{
    static class Guncelleyici
    {
        const string ApiAdresi = "https://api.github.com/repos/" + Ortak.Depo + "/releases/latest";
        const string DosyaAdi = "SurucuAraci.exe";

        static WebClient Istemci()
        {
            // .NET Framework varsayilan olarak eski TLS surumlerini kullanabilir; GitHub TLS 1.2 ister
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var istemci = new WebClient { Encoding = Encoding.UTF8 };
            istemci.Headers[HttpRequestHeader.UserAgent] = "SurucuAraci/" + Ortak.Surum;
            return istemci;
        }

        // Daha yeni bir surum yoksa null doner
        public static SurumBilgisi YeniSurumuDenetle()
        {
            string json;
            using (WebClient istemci = Istemci())
            {
                istemci.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                json = istemci.DownloadString(ApiAdresi);
            }
            var veri = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string etiket = veri["tag_name"] as string ?? "";
            Version surum;
            if (!Version.TryParse(etiket.TrimStart('v', 'V'), out surum) || surum <= Version.Parse(Ortak.Surum)) return null;

            var bilgi = new SurumBilgisi { Surum = surum, Etiket = etiket, Notlar = veri["body"] as string ?? "" };
            foreach (object oge in (veri["assets"] as IEnumerable) ?? new object[0])
            {
                var dosya = oge as IDictionary<string, object>;
                if (dosya == null || dosya["name"] as string != DosyaAdi) continue;
                bilgi.IndirmeAdresi = dosya["browser_download_url"] as string;
                object ozet;
                if (dosya.TryGetValue("digest", out ozet) && ozet is string && ((string)ozet).StartsWith("sha256:"))
                    bilgi.Sha256 = ((string)ozet).Substring("sha256:".Length);
            }
            return bilgi.IndirmeAdresi == null ? null : bilgi;
        }

        // Yeni exe'yi indirir, GitHub'in verdigi SHA-256 ozetiyle dogrular ve calisan dosyanin yerine koyar.
        // Calisan bir exe silinemez ama yeniden adlandirilabilir; eskisi bir sonraki acilista silinir.
        public static void Kur(SurumBilgisi bilgi)
        {
            string hedef = Application.ExecutablePath;
            string yeni = hedef + ".yeni", eski = hedef + ".eski";
            using (WebClient istemci = Istemci()) istemci.DownloadFile(bilgi.IndirmeAdresi, yeni);

            if (bilgi.Sha256 != null)
            {
                string ozet;
                using (SHA256 sha = SHA256.Create())
                using (FileStream dosya = File.OpenRead(yeni))
                    ozet = BitConverter.ToString(sha.ComputeHash(dosya)).Replace("-", "");
                if (!string.Equals(ozet, bilgi.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(yeni);
                    throw new InvalidOperationException(Dil.M("İndirilen dosya doğrulanamadı, güncelleme yapılmadı. Daha sonra tekrar deneyin.",
                                                              "The downloaded file couldn't be verified, so nothing was changed. Please try again later."));
                }
            }
            if (File.Exists(eski)) File.Delete(eski);
            File.Move(hedef, eski);
            File.Move(yeni, hedef);
        }

        // Onceki guncellemeden kalan dosyayi siler; kurulu surumse Uygulamalar listesindeki surum numarasini gunceller
        public static void Toparla()
        {
            try { File.Delete(Application.ExecutablePath + ".eski"); } catch (Exception) { }
            try
            {
                string klasor = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.Equals(klasor.TrimEnd('\\'), Ortak.KurulumKlasoru.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Ortak.KaldirmaAnahtari, true))
                    if (k != null) k.SetValue("DisplayVersion", Ortak.Surum);
            }
            catch (Exception) { }
        }
    }
}
