// Program.cs - Baslangic noktasi: tek kopya calisma, sistem tepsisinde baslama ve kaldirma
//   /tepsi   Windows acilisinda pencere acmadan sistem tepsisinde baslar
//   /devral  Calisan kopyanin kapanmasini bekleyip yerine gecer (yonetici olarak yeniden baslatma)
//   /kaldir  Programi kaldirir (Ayarlar > Uygulamalar listesinden cagrilir); /sessiz ile soru sormadan
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SurucuAraci
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Ayarlar.Yukle();
            Dil.Ayarla(Ayarlar.DilSecimi);
            Tema.Uygula(Ayarlar.TemaSecimi);
            Guncelleyici.Toparla();

            string[] secenekler = args.Select(a => a.ToLowerInvariant()).ToArray();
            if (secenekler.Contains("/kaldir"))
            {
                Kaldirici.Calistir(secenekler.Contains("/sessiz"));
                return;
            }
            bool gizli = secenekler.Contains("/tepsi");
            // Tepsi kapatildiysa Windows acilisinda sessizce cikilir
            if (gizli && !Ayarlar.TepsideCalis) return;
            if (!TekKopya.Baslat(secenekler.Contains("/devral"), !gizli)) return;

            var form = new AnaForm();
            Application.Run(new UygulamaBaglami(form, gizli));
            TekKopya.Birak();
        }
    }

    // Pencere gizliyken de uygulamayi calisir tutar; pencere kapaninca uygulama biter
    class UygulamaBaglami : ApplicationContext
    {
        public UygulamaBaglami(AnaForm form, bool gizli)
        {
            form.FormClosed += (s, e) => ExitThread();
            // Pencere hic gosterilmese de arka plan islerinin sonucu arayuze aktarilabilsin
            GC.KeepAlive(form.Handle);
            TekKopya.GosterIstegiDinle(() => form.BeginInvoke(new Action(form.PencereyiGoster)));
            if (!gizli) form.Show();
        }
    }

    // Ikinci kez acilan program, calisan kopyanin penceresini one getirip kapanir
    static class TekKopya
    {
        const string KilitAdi = @"Local\SurucuAraci_Tekil";
        const string OlayAdi = @"Local\SurucuAraci_Goster";

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int islemNo);

        static Mutex kilit;
        static EventWaitHandle olay;
        static bool sahip;

        // Yonetici olan ve olmayan kopyalar birbirine ulasabilsin diye herkese erisim verilir
        public static bool Baslat(bool bekle, bool gosterIste)
        {
            var herkes = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            try
            {
                var kilitGuvenligi = new MutexSecurity();
                kilitGuvenligi.AddAccessRule(new MutexAccessRule(herkes, MutexRights.FullControl, AccessControlType.Allow));
                bool yeni;
                kilit = new Mutex(false, KilitAdi, out yeni, kilitGuvenligi);
                try { sahip = kilit.WaitOne(bekle ? 15000 : 0); }
                catch (AbandonedMutexException) { sahip = true; }
            }
            catch (UnauthorizedAccessException)
            {
                sahip = false;
            }

            if (!sahip)
            {
                if (gosterIste)
                {
                    try
                    {
                        AllowSetForegroundWindow(-1);
                        EventWaitHandle.OpenExisting(OlayAdi).Set();
                    }
                    catch (Exception) { }
                }
                return false;
            }

            var olayGuvenligi = new EventWaitHandleSecurity();
            olayGuvenligi.AddAccessRule(new EventWaitHandleAccessRule(herkes, EventWaitHandleRights.FullControl, AccessControlType.Allow));
            bool olusturuldu;
            olay = new EventWaitHandle(false, EventResetMode.AutoReset, OlayAdi, out olusturuldu, olayGuvenligi);
            return true;
        }

        public static void GosterIstegiDinle(Action goster)
        {
            var dinleyici = new Thread(() =>
            {
                while (true)
                {
                    olay.WaitOne();
                    goster();
                }
            }) { IsBackground = true };
            dinleyici.Start();
        }

        public static void Birak()
        {
            if (!sahip) return;
            try { kilit.ReleaseMutex(); } catch (Exception) { }
        }
    }

    static class Kaldirici
    {
        // sessiz: winget gibi araclar icin; soru sorulmaz, ayarlar ve gecmis korunur
        public static void Calistir(bool sessiz)
        {
            if (!sessiz && MessageBox.Show(Dil.M("Sürücü Aracı bilgisayarınızdan kaldırılsın mı?", "Remove Driver Tool from your PC?"), Ortak.Ad,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            // Calisan kopyalari kapat
            int buIslem = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName("SurucuAraci").Where(p => p.Id != buIslem))
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }
                catch (Exception) { }
            }

            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Ortak.BaslangicAnahtari, true))
                    if (k != null) k.DeleteValue(Ortak.BaslangicDegeri, false);
                Registry.CurrentUser.DeleteSubKeyTree(Ortak.KaldirmaAnahtari, false);
            }
            catch (Exception) { }
            foreach (string kisayol in Ortak.TumKisayollar())
            {
                try { File.Delete(kisayol); } catch (Exception) { }
            }

            if (!sessiz && MessageBox.Show(Dil.M("Ayarlar ve işlem geçmişi de silinsin mi?\n\n(Belgeler klasöründeki sürücü yedekleri silinmez.)",
                                      "Also delete the settings and action history?\n\n(Driver backups in your Documents folder are kept.)"),
                    Ortak.Ad, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try { Directory.Delete(Ortak.VeriKlasoru, true); } catch (Exception) { }
            }

            // Program klasoru bu islem kapandiktan sonra silinir; yalnizca kurulum klasorunden calisiyorsa
            string klasor = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.Equals(klasor.TrimEnd('\\'), Ortak.KurulumKlasoru.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + klasor + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetTempPath()  // silinecek klasorun icinde durmasin
                });
            }
            if (!sessiz) MessageBox.Show(Dil.M("Sürücü Aracı kaldırıldı.", "Driver Tool has been removed."), Ortak.Ad, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
