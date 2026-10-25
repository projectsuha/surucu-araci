// Kurulum.cs - SurucuAraciKurulum.exe: programi kullanici klasorune kurar (yonetici gerekmez)
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SurucuAraci
{
    class KurulumFormu : Form
    {
        static string M(string tr, string en) { return Dil.M(tr, en); }

        readonly float olcek;
        readonly CheckBox masaustu, baslangic, calistir;
        readonly Dugme btnKur, btnIptal;

        public KurulumFormu()
        {
            Text = M("Sürücü Aracı Kurulumu", "Driver Tool Setup");
            Font = new Font("Segoe UI", 9.75f);
            using (Graphics g = CreateGraphics()) olcek = g.DpiX / 96f;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(O(540), O(400));
            BackColor = Tema.Zemin;

            Bitmap logo = null;
            using (Stream akis = typeof(KurulumFormu).Assembly.GetManifestResourceStream("SurucuAraci.Simge.ico"))
            {
                if (akis != null)
                {
                    Icon = new Icon(akis);
                    using (var buyuk = new Icon(Icon, 64, 64)) logo = buyuk.ToBitmap();
                }
            }

            var bant = new GradyanPanel { Dock = DockStyle.Top, Height = O(112), Padding = new Padding(O(24), O(18), O(24), O(18)) };
            var bantDuzen = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            bantDuzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bantDuzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var logoKutusu = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(O(60), O(60)), BackColor = Color.Transparent, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, O(16), 0) };
            var metinler = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, BackColor = Color.Transparent, Anchor = AnchorStyles.Left };
            metinler.Controls.Add(BantYazisi(Ortak.Ad + " " + Ortak.Surum, new Font("Segoe UI Semibold", 16f), Color.White));
            metinler.Controls.Add(BantYazisi(M("Resmi kaynaklarla sürücü tarama ve güncelleme", "Driver scanning and updating with official sources"),
                new Font("Segoe UI", 9.25f), Color.FromArgb(190, 207, 226)));
            bantDuzen.Controls.Add(logoKutusu, 0, 0);
            bantDuzen.Controls.Add(metinler, 1, 0);
            bant.Controls.Add(bantDuzen);

            var govde = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(O(26), O(20), O(26), 0), BackColor = Tema.Zemin };
            govde.Controls.Add(new Label
            {
                Text = M("Program şu klasöre kurulacak:", "The program will be installed to:"),
                AutoSize = true,
                ForeColor = Tema.Yazi,
                Margin = Padding.Empty
            });
            govde.Controls.Add(new Label
            {
                Text = Ortak.KurulumKlasoru,
                AutoSize = true,
                MaximumSize = new Size(O(480), 0),
                ForeColor = Tema.Soluk,
                Margin = new Padding(0, O(2), 0, O(16))
            });
            masaustu = Onay(govde, M("Masaüstüne kısayol oluştur", "Create a desktop shortcut"), true);
            baslangic = Onay(govde, M("Windows açılınca sistem tepsisinde başlat", "Start in the system tray when Windows starts"), false);
            calistir = Onay(govde, M("Kurulum bitince programı aç", "Open the program when setup finishes"), true);

            var butonlar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(O(20), O(10), O(20), O(18)), BackColor = Tema.Zemin };
            btnKur = new Dugme(M("Kur", "Install"), "\uE896", DugmeTuru.Birincil, new Font("Segoe UI Semibold", 10.5f)) { Margin = new Padding(O(8), 0, 0, 0) };
            btnIptal = new Dugme(M("İptal", "Cancel"), "\uE711", DugmeTuru.Arac, Font);
            btnKur.Click += (s, e) => Kur();
            btnIptal.Click += (s, e) => Close();
            butonlar.Controls.Add(btnKur);
            butonlar.Controls.Add(btnIptal);

            Controls.Add(govde);
            Controls.Add(butonlar);
            Controls.Add(bant);
            HandleCreated += (s, e) => Tema.BaslikCubugu(this);
        }

        int O(int piksel) { return (int)(piksel * olcek); }

        static Label BantYazisi(string metin, Font yazi, Color renk)
        {
            return new Label { Text = metin, Font = yazi, ForeColor = renk, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 2) };
        }

        CheckBox Onay(FlowLayoutPanel govde, string metin, bool isaretli)
        {
            var kutu = new CheckBox { Text = metin, Checked = isaretli, AutoSize = true, Margin = new Padding(0, O(4), 0, O(4)) };
            Tema.OnayKutusu(kutu);
            kutu.ForeColor = Tema.Yazi;
            govde.Controls.Add(kutu);
            return kutu;
        }

        // Kisayol, Windows'un kendi WScript.Shell nesnesiyle olusturulur
        static void Kisayol(string yol, string hedef)
        {
            dynamic kabuk = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic kisayol = kabuk.CreateShortcut(yol);
            kisayol.TargetPath = hedef;
            kisayol.WorkingDirectory = Path.GetDirectoryName(hedef);
            kisayol.IconLocation = hedef + ",0";
            kisayol.Description = M("Resmi kaynaklarla sürücü tarama ve güncelleme", "Driver scanning and updating with official sources");
            kisayol.Save();
        }

        void Kur()
        {
            if (Process.GetProcessesByName("SurucuAraci").Length > 0 &&
                MessageBox.Show(this, M("Sürücü Aracı şu an açık. Kuruluma devam etmek için kapatılsın mı?", "Driver Tool is currently running. Close it to continue setup?"),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            btnKur.Enabled = btnIptal.Enabled = false;
            UseWaitCursor = true;
            try
            {
                string hedef = KurulumYap(masaustu.Checked, baslangic.Checked);
                UseWaitCursor = false;
                MessageBox.Show(this, M("Kurulum tamamlandı. Programı Başlat menüsünden açabilirsiniz.", "Setup complete. You can open the program from the Start menu."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (calistir.Checked) Process.Start(hedef);
                Close();
            }
            catch (Exception hata)
            {
                UseWaitCursor = false;
                btnKur.Enabled = btnIptal.Enabled = true;
                MessageBox.Show(this, M("Kurulum tamamlanamadı:\n\n", "Setup failed:\n\n") + hata.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Calisan programi kapatir, dosyayi kopyalar, kisayollari ve Uygulamalar kaydini olusturur; kurulan exe'nin yolunu doner
        public static string KurulumYap(bool masaustuKisayolu, bool windowsIleBaslat)
        {
            foreach (Process p in Process.GetProcessesByName("SurucuAraci"))
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }
                catch (Exception) { }
            }

            Directory.CreateDirectory(Ortak.KurulumKlasoru);
            string hedef = Path.Combine(Ortak.KurulumKlasoru, "SurucuAraci.exe");
            using (Stream kaynak = typeof(KurulumFormu).Assembly.GetManifestResourceStream("SurucuAraci.Uygulama.exe"))
            using (FileStream dosya = File.Create(hedef))
                kaynak.CopyTo(dosya);

            // Onceki kurulumdan kalan, diger dildeki kisayollar temizlenir; masaustunde kisayol varsa yenisi olusturulur
            string masaustu = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (string eski in Ortak.TumKisayollar())
            {
                if (!File.Exists(eski)) continue;
                if (eski.StartsWith(masaustu, StringComparison.OrdinalIgnoreCase)) masaustuKisayolu = true;
                try { File.Delete(eski); } catch (Exception) { }
            }
            Kisayol(Ortak.BaslatKisayolu, hedef);
            if (masaustuKisayolu) Kisayol(Ortak.MasaustuKisayolu, hedef);

            // Ayarlar > Uygulamalar listesinde gorunmesi icin; QuietUninstallString winget'in sessiz kaldirmasi icindir
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Ortak.KaldirmaAnahtari))
            {
                k.SetValue("DisplayName", Ortak.Ad);
                k.SetValue("DisplayVersion", Ortak.Surum);
                k.SetValue("DisplayIcon", hedef);
                k.SetValue("Publisher", "projectsuha");
                k.SetValue("InstallLocation", Ortak.KurulumKlasoru);
                k.SetValue("UninstallString", "\"" + hedef + "\" /kaldir");
                k.SetValue("QuietUninstallString", "\"" + hedef + "\" /kaldir /sessiz");
                k.SetValue("URLInfoAbout", "https://github.com/" + Ortak.Depo);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)(new FileInfo(hedef).Length / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Ortak.BaslangicAnahtari))
            {
                if (windowsIleBaslat) k.SetValue(Ortak.BaslangicDegeri, "\"" + hedef + "\" /tepsi");
                else k.DeleteValue(Ortak.BaslangicDegeri, false);
            }
            return hedef;
        }
    }

    // /sessiz (veya /S, /silent): pencere acmadan kurar, cikis kodu 0 basari, 1 hata (winget icin)
    static class KurulumProgrami
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            Dil.Ayarla("sistem");
            if (args.Any(a => new[] { "/sessiz", "/s", "/silent" }.Contains(a.ToLowerInvariant())))
            {
                try
                {
                    KurulumFormu.KurulumYap(false, false);
                    return 0;
                }
                catch (Exception)
                {
                    return 1;
                }
            }
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Tema.Uygula("sistem");
            Application.Run(new KurulumFormu());
            return 0;
        }
    }
}
