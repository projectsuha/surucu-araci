// AnaForm.cs - Ana pencere: sekmeler, liste, ayrinti paneli ve sistem tepsisi
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SurucuAraci
{
    enum Gorunum { Tarama, Gizlenenler, Uretici, Saglik, Gecmis, Bakim, DiskTemizligi, Baslangic, GeriYukleme }

    class AnaForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int mesaj, IntPtr w, string l);

        static string M(string tr, string en) { return Dil.M(tr, en); }

        readonly float olcek;
        readonly Icon uygulamaSimgesi;
        readonly ListView liste = new ListView();
        readonly Label bosDurum = new Label();
        readonly AyrintiPaneli ayrinti = new AyrintiPaneli();
        readonly PilGrafigi pilGrafigi = new PilGrafigi();
        readonly Label ozet, altBilgi;
        readonly Kart kartGuncelleme, kartSorunlu, kartTaranan;
        readonly TextBox arama = new TextBox();
        readonly CheckBox tumunuGoster = new CheckBox();
        readonly StatusStrip alt;
        readonly ToolStripStatusLabel durum = new ToolStripStatusLabel();
        readonly ToolStripProgressBar ilerleme = new ToolStripProgressBar();
        readonly Control[] zeminKontrolleri;
        readonly Panel cerceve;
        readonly FlowLayoutPanel araclar;
        readonly Dugme btnYeniSurum, btnAyarlar, btnTara, btnGuncelle;
        readonly Dugme btnKatalog, btnOnar, btnGizle, btnGizlenenler, btnTaramayaDon, btnTekrarGoster, btnAc, btnAracKur, btnSaglikYenile, btnGecmisiSil;
        readonly Dugme btnBakimYenile, btnSil, btnYedekle, btnKlasordenYukle, btnYonetici, btnYeniNokta, btnGeriDon, btnSistemGeriYukleme, btnRapor;
        readonly Dugme btnDiskTemizligi, btnBaslangic, btnBakimaDon, btnTemizle, btnBaslangicDegistir;
        readonly Dictionary<Gorunum, Dugme> sekmeler = new Dictionary<Gorunum, Dugme>();
        readonly Dictionary<Gorunum, Dugme[]> gorunumAraclari = new Dictionary<Gorunum, Dugme[]>();
        readonly NotifyIcon tepsi = new NotifyIcon();
        readonly Dictionary<string, Icon> tepsiSimgeleri = new Dictionary<string, Icon>();
        readonly Timer kontrolZamanlayicisi = new Timer();

        Gorunum gorunum = Gorunum.Tarama;
        bool mesgul, cikiliyor, taramaBasladi;
        List<Aygit> aygitlar = new List<Aygit>();
        List<Guncelleme> sonGuncellemeler = new List<Guncelleme>();
        SurumBilgisi yeniSurum;
        bool guncellemelerArandi;
        string guncellemeHatasi;
        DateTime sonTarama;
        List<ListViewItem> tumOgeler = new List<ListViewItem>();
        string bosMetni = "";

        public AnaForm()
        {
            Text = Ortak.Ad + (Sistem.YoneticiMi() ? M("  [Yönetici]", "  [Administrator]") : "");
            Font = new Font("Segoe UI", 9.75f);
            using (Graphics g = CreateGraphics()) olcek = g.DpiX / 96f;
            Size = new Size(O(1260), O(800));
            MinimumSize = new Size(O(1060), O(600));
            StartPosition = FormStartPosition.CenterScreen;

            Bitmap logo = null;
            using (Stream akis = typeof(AnaForm).Assembly.GetManifestResourceStream("SurucuAraci.Simge.ico"))
            {
                if (akis != null)
                {
                    uygulamaSimgesi = new Icon(akis);
                    Icon = uygulamaSimgesi;
                    using (var buyuk = new Icon(uygulamaSimgesi, 64, 64)) logo = buyuk.ToBitmap();
                }
            }

            // ----- Ust bant: logo, ozet ve ana butonlar -----
            var ustBant = new GradyanPanel { Dock = DockStyle.Top, Height = O(118), Padding = new Padding(O(26), O(16), O(26), O(16)) };
            var ustDuzen = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = Color.Transparent };
            ustDuzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ustDuzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) ustDuzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ustDuzen.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var logoKutusu = new PictureBox
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(O(62), O(62)),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, O(16), 0)
            };

            var metinler = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
            metinler.Controls.Add(Etiket(Ortak.Ad.ToUpper(Dil.Ingilizce ? System.Globalization.CultureInfo.InvariantCulture : new System.Globalization.CultureInfo("tr-TR")),
                new Font("Segoe UI Semibold", 8.5f), Color.FromArgb(150, 182, 218)), 0, 0);
            ozet = Etiket(M("Tarama yapılmadı", "Not scanned yet"), new Font("Segoe UI Semibold", 17f), Color.White);
            altBilgi = Etiket(M("Resmi kaynaklarla sürücü taraması", "Driver scanning with official sources"), new Font("Segoe UI", 9.25f), Color.FromArgb(190, 207, 226));
            metinler.Controls.Add(ozet, 0, 1);
            metinler.Controls.Add(altBilgi, 0, 2);

            var buyukYazi = new Font("Segoe UI Semibold", 11f);
            // Programin yeni surumu varsa gorunur
            btnYeniSurum = new Dugme("", "\uE896", DugmeTuru.Seffaf, new Font("Segoe UI Semibold", 9.75f)) { Anchor = AnchorStyles.None, Margin = new Padding(O(10), 0, 0, 0), Visible = false };
            btnYeniSurum.Click += (s, e) => YeniSurumuGoster();
            btnAyarlar =new Dugme("", "\uE713", DugmeTuru.Seffaf, buyukYazi) { Anchor = AnchorStyles.None, Margin = new Padding(O(10), 0, 0, 0), AccessibleName = M("Ayarlar", "Settings") };
            btnAyarlar.Click += (s, e) => AyarlariAc();
            new ToolTip().SetToolTip(btnAyarlar, M("Ayarlar", "Settings"));
            btnTara = new Dugme(M("Tara", "Scan"), "\uE72C", DugmeTuru.Seffaf, buyukYazi) { Anchor = AnchorStyles.None, Margin = new Padding(O(10), 0, 0, 0) };
            btnTara.Click += (s, e) => Tara(false);
            btnGuncelle = new Dugme(M("Güncelle", "Update"), "\uE896", DugmeTuru.Birincil, buyukYazi) { Anchor = AnchorStyles.None, Margin = new Padding(O(10), 0, 0, 0) };
            btnGuncelle.Click += (s, e) => Guncelle();

            ustDuzen.Controls.Add(logoKutusu, 0, 0);
            ustDuzen.Controls.Add(metinler, 1, 0);
            ustDuzen.Controls.Add(btnYeniSurum, 2, 0);
            ustDuzen.Controls.Add(btnAyarlar, 3, 0);
            ustDuzen.Controls.Add(btnTara, 4, 0);
            ustDuzen.Controls.Add(btnGuncelle, 5, 0);
            ustBant.Controls.Add(ustDuzen);

            // ----- Ozet kartlari -----
            var kartlar = new TableLayoutPanel { Dock = DockStyle.Top, Height = O(104), ColumnCount = 3, RowCount = 1, Padding = new Padding(O(20), O(18), O(20), O(4)) };
            for (int i = 0; i < 3; i++) kartlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            kartlar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            kartGuncelleme = new Kart(M("Güncelleme bekliyor", "Updates available"), "\uE896", Tema.Turuncu);
            kartSorunlu = new Kart(M("Sorunlu aygıt", "Devices with problems"), "\uE7BA", Tema.Kirmizi);
            kartTaranan = new Kart(M("Taranan aygıt", "Devices scanned"), "\uE772", Tema.Mavi);
            foreach (Kart k in new[] { kartGuncelleme, kartSorunlu, kartTaranan })
            {
                k.Dock = DockStyle.Fill;
                k.Margin = new Padding(O(6), 0, O(6), 0);
                kartlar.Controls.Add(k);
            }

            // ----- Sekmeler ve arama -----
            var sekmeSatiri = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Padding = new Padding(O(22), O(14), O(26), 0) };
            sekmeSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sekmeSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var sekmeAkisi = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false };
            Sekme(sekmeAkisi, Gorunum.Tarama, M("Tarama", "Scan"), "\uE721");
            Sekme(sekmeAkisi, Gorunum.Uretici, M("Üretici Kaynakları", "Manufacturer Sources"), "\uE8F1");
            Sekme(sekmeAkisi, Gorunum.Saglik, M("Sistem Sağlığı", "System Health"), "\uE95E");
            Sekme(sekmeAkisi, Gorunum.Gecmis, M("Geçmiş", "History"),"\uE81C");
            Sekme(sekmeAkisi, Gorunum.Bakim, M("Bakım", "Maintenance"), "\uE90F");
            Sekme(sekmeAkisi, Gorunum.GeriYukleme, M("Geri Yükleme", "System Restore"), "\uE777");
            arama.Width = O(240);
            arama.Anchor = AnchorStyles.Right;
            arama.BorderStyle = BorderStyle.FixedSingle;
            arama.Margin = new Padding(0, O(4), 0, 0);
            // 0x1501 = EM_SETCUEBANNER: kutu bosken soluk ipucu yazisi
            arama.HandleCreated += (s, e) => SendMessage(arama.Handle, 0x1501, (IntPtr)1, M("Listede ara...", "Search the list..."));
            arama.TextChanged += (s, e) => Filtrele();
            sekmeSatiri.Controls.Add(sekmeAkisi, 0, 0);
            sekmeSatiri.Controls.Add(arama, 1, 0);

            // ----- Gorunume gore degisen arac butonlari -----
            var aracSatiri = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Padding = new Padding(O(26), O(10), O(26), O(10)) };
            aracSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            aracSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            araclar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            btnKatalog = Arac(M("Katalogda Ara", "Search Catalog"), "\uE774", KatalogdaAra);
            btnOnar = Arac(M("Onar", "Repair"), "\uE90F", OnarmaMenusunuAc);
            btnSaglikYenile = Arac(M("Yenile", "Refresh"), "\uE72C", SaglikGoster);
            btnGizle = Arac(M("Güncellemeyi Gizle", "Hide Update"), "\uED1A", GuncellemeyiGizle);
            btnGizlenenler = Arac(M("Gizlenenler", "Hidden Updates"), "\uE890", () => GorunumuDegistir(Gorunum.Gizlenenler));
            btnTaramayaDon = Arac(M("Tarama Sonuçlarına Dön", "Back to Scan Results"), "\uE72B", () => GorunumuDegistir(Gorunum.Tarama));
            btnTekrarGoster = Arac(M("Tekrar Göster", "Unhide"), "\uE890", GizlemeyiKaldir);
            btnAc = Arac(M("Sayfayı Aç", "Open Page"), "\uE8A7", SecilenSayfayiAc);
            btnAracKur = Arac(M("Resmi Aracı Kur", "Install Official Tool"), "\uE896", ResmiAraciKur);
            btnGecmisiSil = Arac(M("Geçmişi Temizle", "Clear History"), "\uE74D", GecmisiTemizle);
            btnBakimYenile = Arac(M("Yeniden Tara", "Rescan"), "\uE72C", EskiSuruculeriGoster);
            btnSil = Arac(M("Seçilenleri Sil", "Remove Selected"), "\uE74D", EskiSuruculeriSil);
            btnYedekle = Arac(M("Sürücüleri Yedekle", "Back Up Drivers"), "\uE74E", Yedekle);
            btnKlasordenYukle = Arac(M("Klasörden Yükle", "Install from Folder"), "\uE838", KlasordenYukle);
            btnYonetici = Arac(M("Yönetici Olarak Aç", "Run as Administrator"), "\uE7EF", () => YoneticiOl());
            btnYeniNokta = Arac(M("Yeni Nokta Oluştur", "Create Restore Point"), "\uE710", YeniNoktaOlustur);
            btnGeriDon = Arac(M("Bu Noktaya Dön", "Restore to This Point"), "\uE7A7", NoktayaDon);
            btnSistemGeriYukleme = Arac(M("Windows Sistem Geri Yükleme", "Windows System Restore"), "\uE8A7", SistemGeriYuklemeyiAc);
            btnRapor = Arac(M("Rapor Kaydet", "Save Report"), "\uE8A5", RaporKaydet);
            btnDiskTemizligi = Arac(M("Disk Temizliği", "Disk Cleanup"), "\uE74D", () => GorunumuDegistir(Gorunum.DiskTemizligi));
            btnBaslangic = Arac(M("Başlangıç Programları", "Startup Programs"), "\uE7E8", () => GorunumuDegistir(Gorunum.Baslangic));
            btnBakimaDon = Arac(M("Bakım'a Dön", "Back to Maintenance"), "\uE72B", () => GorunumuDegistir(Gorunum.Bakim));
            btnTemizle = Arac(M("Seçilenleri Temizle", "Clean Selected"), "\uE74D", DiskiTemizle);
            btnBaslangicDegistir = Arac(M("Devre Dışı Bırak", "Disable"), "\uE7E8", BaslangicDurumunuDegistir);

            gorunumAraclari[Gorunum.Tarama] = new[] { btnKatalog, btnOnar, btnGizle, btnGizlenenler, btnRapor };
            gorunumAraclari[Gorunum.Gizlenenler] = new[] { btnTaramayaDon, btnTekrarGoster, btnRapor };
            gorunumAraclari[Gorunum.Uretici] = new[] { btnAc, btnAracKur, btnRapor };
            gorunumAraclari[Gorunum.Saglik] = new[] { btnSaglikYenile, btnYonetici, btnRapor };
            gorunumAraclari[Gorunum.Gecmis] = new[] { btnGecmisiSil, btnRapor };
            gorunumAraclari[Gorunum.Bakim] = new[] { btnBakimYenile, btnSil, btnYedekle, btnKlasordenYukle, btnDiskTemizligi, btnBaslangic, btnRapor };
            gorunumAraclari[Gorunum.DiskTemizligi] = new[] { btnBakimaDon, btnTemizle, btnRapor };
            gorunumAraclari[Gorunum.Baslangic] = new[] { btnBakimaDon, btnBaslangicDegistir, btnRapor };
            gorunumAraclari[Gorunum.GeriYukleme] = new[] { btnYonetici, btnYeniNokta, btnGeriDon, btnSistemGeriYukleme, btnRapor };

            tumunuGoster.Text = M("Tüm aygıtları göster", "Show all devices");
            tumunuGoster.AutoSize = true;
            tumunuGoster.Anchor = AnchorStyles.Right;
            tumunuGoster.CheckedChanged += (s, e) => { if (gorunum == Gorunum.Tarama) TaramayiGoster(); };
            aracSatiri.Controls.Add(araclar, 0, 0);
            aracSatiri.Controls.Add(tumunuGoster, 1, 0);

            // ----- Liste ve ayrinti paneli -----
            var listeAlani = new Panel { Dock = DockStyle.Fill, Padding = new Padding(O(26), 0, O(26), O(14)) };
            cerceve = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            liste.Dock = DockStyle.Fill;
            liste.BorderStyle = BorderStyle.None;
            liste.View = View.Details;
            liste.FullRowSelect = true;
            liste.HideSelection = false;
            liste.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            liste.ShowItemToolTips = true;
            liste.SmallImageList = DurumSimgeleri();
            liste.HandleCreated += (s, e) => Tema.ListeTemasi(liste);
            liste.Resize += (s, e) => SonSutunuDoldur();
            // Taramada yalnizca guncellemesi olan satirlar isaretlenebilir
            liste.ItemCheck += (s, e) =>
            {
                if (gorunum != Gorunum.Tarama) return;
                var a = liste.Items[e.Index].Tag as Aygit;
                if (a == null || a.Guncelleme == null) e.NewValue = e.CurrentValue;
            };
            liste.ItemChecked += (s, e) =>
            {
                var a = e.Item.Tag as Aygit;
                if (a != null && gorunum == Gorunum.Tarama) a.Secili = e.Item.Checked;
                ButonlariGuncelle();
            };
            liste.SelectedIndexChanged += (s, e) => { AyrintiyiGuncelle(); ButonlariGuncelle(); };
            liste.ItemActivate += (s, e) => { if (gorunum == Gorunum.Uretici) SecilenSayfayiAc(); };

            bosDurum.Dock = DockStyle.Fill;
            bosDurum.TextAlign = ContentAlignment.MiddleCenter;
            bosDurum.Font = new Font("Segoe UI Semibold", 13f);
            bosDurum.Visible = false;
            ayrinti.Dock = DockStyle.Right;
            ayrinti.Width = O(290);
            cerceve.Controls.Add(liste);
            cerceve.Controls.Add(bosDurum);
            cerceve.Controls.Add(ayrinti);
            pilGrafigi.Dock = DockStyle.Bottom;
            pilGrafigi.Height = O(165);
            pilGrafigi.Visible = false;
            cerceve.Controls.Add(pilGrafigi);
            listeAlani.Controls.Add(cerceve);

            // ----- Durum cubugu -----
            alt = new StatusStrip { SizingGrip = false, Renderer = new DuzCizici(), Padding = new Padding(O(26), 0, O(26), O(6)) };
            durum.Spring = true;
            durum.TextAlign = ContentAlignment.MiddleLeft;
            ilerleme.Style = ProgressBarStyle.Marquee;
            ilerleme.Visible = false;
            alt.Items.Add(durum);
            alt.Items.Add(ilerleme);

            // Son eklenen once yerlesir: liste kalan alani doldurur
            Controls.Add(listeAlani);
            Controls.Add(aracSatiri);
            Controls.Add(sekmeSatiri);
            Controls.Add(kartlar);
            Controls.Add(ustBant);
            Controls.Add(alt);
            zeminKontrolleri = new Control[] { kartlar, sekmeSatiri, sekmeAkisi, aracSatiri, araclar, listeAlani };

            TepsiyiHazirla();
            kontrolZamanlayicisi.Interval = 30 * 60 * 1000;
            kontrolZamanlayicisi.Tick += (s, e) => { OtomatikKontrol(); ProgramGuncellemesiniDenetle(false); };
            kontrolZamanlayicisi.Start();
            // Windows acilisinda gizli baslarsa ilk kontrol yarim dakika sonra yapilir
            var ilkKontrol = new Timer { Interval = 30000 };
            ilkKontrol.Tick += (s, e) => { ilkKontrol.Stop(); ilkKontrol.Dispose(); if (!taramaBasladi) OtomatikKontrol(); };
            ilkKontrol.Start();
            // Programin yeni surumu acilistan biraz sonra denetlenir
            var surumKontrolu = new Timer { Interval = 15000 };
            surumKontrolu.Tick += (s, e) => { surumKontrolu.Stop(); surumKontrolu.Dispose(); ProgramGuncellemesiniDenetle(false); };
            surumKontrolu.Start();

            HandleCreated += (s, e) => Tema.BaslikCubugu(this);
            Shown += (s, e) => { if (!taramaBasladi) Tara(false); };
            FormClosing += PencereKapaniyor;

            TemayiUygula(false);
            GorunumAyarla(Gorunum.Tarama);
            BosDurumuGoster(M("Tarama başlatılıyor...", "Starting scan..."));
        }

        int O(int piksel) { return (int)(piksel * olcek); }

        static Label Etiket(string metin, Font yazi, Color renk)
        {
            return new Label { Text = metin, Font = yazi, ForeColor = renk, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 2) };
        }

        Dugme Arac(string metin, string simge, Action tiklama)
        {
            var d = new Dugme(metin, simge, DugmeTuru.Arac, Font) { Margin = new Padding(0, 0, O(8), O(4)) };
            d.Click += (s, e) => tiklama();
            return d;
        }

        void Sekme(FlowLayoutPanel akis, Gorunum hedef, string metin, string simge)
        {
            var d = new Dugme(metin, simge, DugmeTuru.Sekme, Font) { Margin = new Padding(0, 0, O(4), 0) };
            d.Click += (s, e) => GorunumuDegistir(hedef);
            sekmeler[hedef] = d;
            akis.Controls.Add(d);
        }

        // Liste satirlarinin basindaki renkli durum noktalari
        ImageList DurumSimgeleri()
        {
            var simgeler = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(O(22), O(32)) };
            Nokta(simgeler, "sorunlu", Tema.Kirmizi);
            Nokta(simgeler, "guncelleme", Tema.Turuncu);
            Nokta(simgeler, "guncel", Tema.Yesil);
            Nokta(simgeler, "bilgi", Tema.Mavi);
            Nokta(simgeler, "gizli", Tema.Gri);
            return simgeler;
        }

        void Nokta(ImageList simgeler, string ad, Color renk)
        {
            var bmp = new Bitmap(simgeler.ImageSize.Width, simgeler.ImageSize.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var firca = new SolidBrush(renk))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cap = O(10);
                g.FillEllipse(firca, (bmp.Width - cap) / 2f, (bmp.Height - cap) / 2f, cap, cap);
            }
            simgeler.Images.Add(ad, bmp);
        }

        void TemayiUygula(bool yenidenCiz)
        {
            BackColor = Tema.Zemin;
            foreach (Control c in zeminKontrolleri) c.BackColor = Tema.Zemin;
            cerceve.BackColor = Tema.Kenarlik;
            bosDurum.BackColor = Tema.Yuzey;
            bosDurum.ForeColor = Tema.Soluk;
            arama.BackColor = Tema.Yuzey;
            arama.ForeColor = Tema.Yazi;
            Tema.OnayKutusu(tumunuGoster);
            alt.BackColor = Tema.Zemin;
            durum.ForeColor = Tema.Soluk;
            Tema.ListeTemasi(liste);
            if (IsHandleCreated) Tema.BaslikCubugu(this);
            Invalidate(true);
            if (!yenidenCiz) return;
            if (gorunum == Gorunum.Tarama && taramaBasladi) TaramayiGoster();
            else if (gorunum == Gorunum.Gecmis) GecmisiGoster();
        }

        // ---------- Sistem tepsisi ----------

        void TepsiyiHazirla()
        {
            tepsiSimgeleri["bilgi"] = TepsiSimgesi(Tema.Mavi);
            tepsiSimgeleri["guncel"] = TepsiSimgesi(Tema.Yesil);
            tepsiSimgeleri["guncelleme"] = TepsiSimgesi(Tema.Turuncu);
            tepsiSimgeleri["sorunlu"] = TepsiSimgesi(Tema.Kirmizi);

            var menu = new ContextMenuStrip { Renderer = new MenuCizici() };
            ToolStripItem ac = menu.Items.Add(M("Aç", "Open"), null, (s, e) => PencereyiGoster());
            ac.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(M("Şimdi Kontrol Et", "Check Now"), null, (s, e) => Tara(true));
            menu.Items.Add(M("Ayarlar", "Settings"), null, (s, e) => { PencereyiGoster(); AyarlariAc(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(M("Çıkış", "Exit"), null, (s, e) => Cik());

            tepsi.ContextMenuStrip = menu;
            tepsi.Icon = tepsiSimgeleri["bilgi"];
            tepsi.Text = Ortak.Ad;
            tepsi.Visible = Ayarlar.TepsideCalis;
            tepsi.DoubleClick += (s, e) => PencereyiGoster();
            tepsi.BalloonTipClicked += (s, e) => PencereyiGoster();
        }

        // Uygulama simgesinin sag altina durum rengini gosteren bir nokta ekler
        Icon TepsiSimgesi(Color renk)
        {
            Size boyut = SystemInformation.SmallIconSize;
            using (var bmp = new Bitmap(boyut.Width, boyut.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    if (uygulamaSimgesi != null)
                        using (var kucuk = new Icon(uygulamaSimgesi, boyut)) g.DrawIcon(kucuk, new Rectangle(Point.Empty, boyut));
                    float cap = boyut.Width * 0.5f, x = boyut.Width - cap, y = boyut.Height - cap;
                    using (var kenar = new SolidBrush(Color.White)) g.FillEllipse(kenar, x - 1, y - 1, cap + 1, cap + 1);
                    using (var firca = new SolidBrush(renk)) g.FillEllipse(firca, x, y, cap - 1, cap - 1);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public void PencereyiGoster()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        void PencereKapaniyor(object sender, FormClosingEventArgs e)
        {
            if (cikiliyor || e.CloseReason != CloseReason.UserClosing || !Ayarlar.TepsideCalis)
            {
                tepsi.Visible = false;
                return;
            }
            e.Cancel = true;
            Hide();
            if (!Ayarlar.TepsiIpucuGosterildi)
            {
                Ayarlar.TepsiIpucuGosterildi = true;
                tepsi.ShowBalloonTip(8000, Ortak.Ad,
                    M("Program arka planda çalışmaya ve sürücüleri düzenli olarak kontrol etmeye devam ediyor. Tamamen kapatmak için bu simgeye sağ tıklayıp Çıkış'ı seçin.",
                      "The program keeps running in the background and checks your drivers regularly. To close it completely, right-click this icon and choose Exit."),
                    ToolTipIcon.Info);
            }
        }

        public void Cik()
        {
            cikiliyor = true;
            tepsi.Visible = false;
            Close();
        }

        void OtomatikKontrol()
        {
            if (mesgul || Ayarlar.KontrolGunu <= 0) return;
            if (DateTime.Now - Ayarlar.SonKontrol < TimeSpan.FromDays(Ayarlar.KontrolGunu)) return;
            Tara(true);
        }

        // Pencere gizliyken bulunan guncellemeleri Windows bildirimiyle haber verir
        void Bildir()
        {
            if (!Ayarlar.Bildirim || !tepsi.Visible || (Visible && WindowState != FormWindowState.Minimized)) return;
            int guncelleme = aygitlar.Count(a => a.Guncelleme != null), sorunlu = aygitlar.Count(a => a.Durum == Durum.Sorunlu);
            if (guncelleme == 0 && sorunlu == 0) return;
            var parcalar = new List<string>();
            if (guncelleme > 0) parcalar.Add(string.Format(M("{0} sürücü güncellemesi bulundu.", "{0} driver update(s) found."), guncelleme));
            if (sorunlu > 0) parcalar.Add(string.Format(M("{0} aygıtta sorun var.", "{0} device(s) have a problem."), sorunlu));
            parcalar.Add(M("Görmek için tıklayın.", "Click to view."));
            tepsi.ShowBalloonTip(10000, Ortak.Ad, string.Join(" ", parcalar), ToolTipIcon.Info);
        }

        // ---------- Ortak isleyis ----------

        void Mesgul(bool deger, string mesaj)
        {
            mesgul = deger;
            ilerleme.Visible = deger;
            UseWaitCursor = deger;
            if (mesaj != null) durum.Text = mesaj;
            ButonlariGuncelle();
        }

        // Uzun isleri arka planda calistirir, bitince sonucu arayuze verir
        void Calistir<T>(string mesaj, Func<T> islem, Action<T> bitti)
        {
            Mesgul(true, mesaj);
            Task.Factory.StartNew(islem).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                Mesgul(false, "");
                if (t.IsFaulted)
                {
                    Exception hata = t.Exception.GetBaseException();
                    durum.Text = M("Hata: ", "Error: ") + hata.Message;
                    MessageBox.Show(this, hata.Message, M("Hata", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else bitti(t.Result);
            })));
        }

        bool Sor(string mesaj, MessageBoxIcon simge)
        {
            return MessageBox.Show(this, mesaj, Ortak.Ad, MessageBoxButtons.YesNo, simge) == DialogResult.Yes;
        }

        void Bilgi(string mesaj, bool basarili)
        {
            MessageBox.Show(this, mesaj, Ortak.Ad, MessageBoxButtons.OK, basarili ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        void ListeyiDoldur(string[] sutunlar, int[] genislikler, List<ListViewItem> ogeler, bool onayKutulu, string bosMetin)
        {
            liste.BeginUpdate();
            liste.Items.Clear();
            liste.Columns.Clear();
            liste.CheckBoxes = onayKutulu;
            for (int i = 0; i < sutunlar.Length; i++) liste.Columns.Add(sutunlar[i], O(genislikler[i]));
            liste.EndUpdate();
            tumOgeler = ogeler;
            bosMetni = bosMetin;
            Filtrele();
        }

        // Arama kutusundaki metni iceren satirlari gosterir
        void Filtrele()
        {
            string aranan = arama.Text.Trim();
            List<ListViewItem> gorunen = aranan == "" ? tumOgeler : tumOgeler.Where(o => o.SubItems.Cast<ListViewItem.ListViewSubItem>()
                .Any(h => h.Text.IndexOf(aranan, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
            liste.BeginUpdate();
            liste.Items.Clear();
            liste.Items.AddRange(gorunen.ToArray());
            liste.EndUpdate();
            BosDurumuGoster(gorunen.Count > 0 ? null
                : tumOgeler.Count > 0 ? M("Aramayla eşleşen sonuç yok.", "No results match your search.") : bosMetni);
            // Liste gorunur olduktan sonra olculur; gizliyken genisligi dogru degildir
            SonSutunuDoldur();
            AyrintiyiGuncelle();
            ButonlariGuncelle();
        }

        // Son sutun kalan genisligi doldurur, yatay kaydirma cubugu cikmaz.
        // Dikey kaydirma cubugu henuz cizilmemis olabilir; satirlar sigmiyorsa onun genisligi de dusulur.
        void SonSutunuDoldur()
        {
            if (liste.Columns.Count == 0) return;
            int diger = 0;
            for (int i = 0; i < liste.Columns.Count - 1; i++) diger += liste.Columns[i].Width;
            int genislik = liste.ClientSize.Width;
            if (liste.Items.Count > 0 && genislik == liste.Width)
            {
                Rectangle satir = liste.GetItemRect(0);
                if (satir.Top + satir.Height * liste.Items.Count > liste.ClientSize.Height) genislik -= SystemInformation.VerticalScrollBarWidth;
            }
            liste.Columns[liste.Columns.Count - 1].Width = Math.Max(O(100), genislik - diger - 1);
        }

        // Liste bosken ortada bir mesaj gosterir; metin null ise listeyi gosterir
        void BosDurumuGoster(string metin)
        {
            bool bos = metin != null;
            if (bos) bosDurum.Text = metin;
            bosDurum.Visible = bos;
            liste.Visible = !bos;
        }

        T SeciliEtiket<T>() where T : class
        {
            return liste.SelectedItems.Count == 0 ? null : liste.SelectedItems[0].Tag as T;
        }

        void ButonlariGuncelle()
        {
            int secili = aygitlar.Count(a => a.Guncelleme != null && a.Secili);
            btnGuncelle.Text = secili > 0 ? M("Güncelle", "Update") + " (" + secili + ")" : M("Güncelle", "Update");
            btnGuncelle.Enabled = !mesgul && secili > 0;

            Aygit aygit = SeciliEtiket<Aygit>();
            Kaynak kaynak = SeciliEtiket<Kaynak>();
            btnKatalog.Enabled = !mesgul && aygit != null && aygit.DonanimKimlikleri.Length > 0;
            btnOnar.Enabled = !mesgul && gorunum == Gorunum.Tarama && aygit != null && aygit.Kimlik != "";
            btnGizle.Enabled = btnTekrarGoster.Enabled = !mesgul && aygit != null && aygit.Guncelleme != null;
            btnAc.Enabled = !mesgul && kaynak != null && kaynak.Adres != "";
            btnAracKur.Enabled = !mesgul && kaynak != null && kaynak.WingetId != null && !kaynak.Kurulu;
            btnSil.Enabled = !mesgul && gorunum == Gorunum.Bakim && liste.CheckedItems.Count > 0;
            btnTemizle.Enabled = !mesgul && gorunum == Gorunum.DiskTemizligi && liste.CheckedItems.Count > 0;
            BaslangicOgesi baslangic = SeciliEtiket<BaslangicOgesi>();
            btnBaslangicDegistir.Enabled = !mesgul && baslangic != null;
            btnBaslangicDegistir.Text = baslangic != null && !baslangic.Etkin ? M("Etkinleştir", "Enable") : M("Devre Dışı Bırak", "Disable");
            btnGeriDon.Enabled = !mesgul && SeciliEtiket<GeriNokta>() != null;
            btnGecmisiSil.Enabled = !mesgul && tumOgeler.Count > 0;
            btnRapor.Enabled = !mesgul && liste.Items.Count > 0;
            foreach (Dugme d in new[] { btnYeniSurum, btnAyarlar, btnTara, btnGizlenenler, btnTaramayaDon, btnBakimYenile, btnYedekle, btnKlasordenYukle,
                                        btnYonetici, btnYeniNokta, btnSistemGeriYukleme, btnSaglikYenile, btnDiskTemizligi, btnBaslangic, btnBakimaDon })
                d.Enabled = !mesgul;
            foreach (Dugme s in sekmeler.Values) s.Enabled = !mesgul;
            tumunuGoster.Enabled = arama.Enabled = !mesgul;
        }

        bool YoneticiOl()
        {
            if (Sistem.YoneticiMi()) return true;
            if (Sor(M("Bu işlem için yönetici yetkisi gerekiyor.\nProgram yönetici olarak yeniden başlatılsın mı?",
                      "This action requires administrator rights.\nRestart the program as administrator?"), MessageBoxIcon.Question))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "/devral") { UseShellExecute = true, Verb = "runas" });
                    Cik();
                }
                catch (Win32Exception) { }  // kullanici UAC penceresinde Hayir dedi
            }
            return false;
        }

        void GorunumuDegistir(Gorunum hedef)
        {
            if (mesgul) return;
            GorunumAyarla(hedef);
            switch (hedef)
            {
                case Gorunum.Tarama: TaramayiGoster(); break;
                case Gorunum.Gizlenenler: GizlenenleriGoster(); break;
                case Gorunum.Uretici: UreticiKaynaklariniGoster(); break;
                case Gorunum.Saglik: SaglikGoster(); break;
                case Gorunum.Gecmis: GecmisiGoster(); break;
                case Gorunum.Bakim: EskiSuruculeriGoster(); break;
                case Gorunum.DiskTemizligi: DiskTemizliginiGoster(); break;
                case Gorunum.Baslangic: BaslangicGoster(); break;
                case Gorunum.GeriYukleme: GeriYuklemeNoktalariniGoster(); break;
            }
        }

        // Sekme vurgusunu, arac butonlarini ve paneli secilen gorunume gore ayarlar
        void GorunumAyarla(Gorunum hedef)
        {
            gorunum = hedef;
            foreach (var s in sekmeler) s.Value.Secili = s.Key == hedef || (hedef == Gorunum.Gizlenenler && s.Key == Gorunum.Tarama)
                || ((hedef == Gorunum.DiskTemizligi || hedef == Gorunum.Baslangic) && s.Key == Gorunum.Bakim);
            araclar.SuspendLayout();
            araclar.Controls.Clear();
            araclar.Controls.AddRange(gorunumAraclari[hedef].Where(d => d != btnYonetici || !Sistem.YoneticiMi()).ToArray());
            araclar.ResumeLayout();
            tumunuGoster.Visible = hedef == Gorunum.Tarama;
            ayrinti.Visible = hedef == Gorunum.Tarama || hedef == Gorunum.Gizlenenler;
            pilGrafigi.Visible = false;  // Sistem Sagligi yuklenince gecmis varsa gosterilir
            arama.Text = "";
        }

        string GorunumBasligi()
        {
            switch (gorunum)
            {
                case Gorunum.Gizlenenler: return M("Gizlenen Güncellemeler", "Hidden Updates");
                case Gorunum.Uretici: return M("Üretici Kaynakları", "Manufacturer Sources");
                case Gorunum.Saglik: return M("Sistem Sağlığı", "System Health");
                case Gorunum.Gecmis: return M("İşlem Geçmişi", "Action History");
                case Gorunum.Bakim: return M("Eski Sürücü Paketleri", "Old Driver Packages");
                case Gorunum.DiskTemizligi: return M("Disk Temizliği", "Disk Cleanup");
                case Gorunum.Baslangic: return M("Başlangıç Programları", "Startup Programs");
                case Gorunum.GeriYukleme: return M("Geri Yükleme Noktaları", "Restore Points");
                default: return M("Sürücü Tarama Sonuçları", "Driver Scan Results");
            }
        }

        // ---------- Tarama ----------

        void Tara(bool bildir)
        {
            if (mesgul) return;
            taramaBasladi = true;
            GorunumAyarla(Gorunum.Tarama);
            ozet.Text = M("Taranıyor...", "Scanning...");
            altBilgi.Text = M("Aygıtlar ve sürücüler inceleniyor", "Checking devices and drivers");
            foreach (Kart k in new[] { kartGuncelleme, kartSorunlu, kartTaranan }) k.Deger = "…";
            tepsi.Icon = tepsiSimgeleri["bilgi"];
            Calistir(M("Aygıtlar ve sürücüler taranıyor...", "Scanning devices and drivers..."), Sistem.AygitlariTara, bulunanlar =>
            {
                aygitlar = bulunanlar;
                guncellemelerArandi = false;
                guncellemeHatasi = null;
                TaramayiGoster();
                Calistir(M("Windows Update'te sürücü güncellemeleri aranıyor (birkaç dakika sürebilir)...",
                           "Searching Windows Update for driver updates (this may take a few minutes)..."), GuncellemeleriAra, sonuc =>
                {
                    guncellemelerArandi = true;
                    guncellemeHatasi = sonuc.Item2;
                    sonTarama = DateTime.Now;
                    if (guncellemeHatasi == null) Ayarlar.SonKontrol = sonTarama;
                    sonGuncellemeler = sonuc.Item1;
                    Sistem.Eslestir(aygitlar, sonuc.Item1);
                    if (gorunum == Gorunum.Tarama) TaramayiGoster(); else KartlariGuncelle();
                    if (bildir) Bildir();
                });
            });
        }

        // Internet yoksa tarama yine de biter; hata ozet olarak gosterilir
        static Tuple<List<Guncelleme>, string> GuncellemeleriAra()
        {
            try { return Tuple.Create(Sistem.GuncellemeAra(), (string)null); }
            catch (Exception h) { return Tuple.Create(new List<Guncelleme>(), h.Message); }
        }

        void KartlariGuncelle()
        {
            int sorunlu = aygitlar.Count(a => a.Durum == Durum.Sorunlu);
            int guncelleme = aygitlar.Count(a => a.Guncelleme != null);
            int taranan = aygitlar.Count(a => a.Kimlik != "");

            kartTaranan.Deger = taranan.ToString();
            kartSorunlu.Deger = sorunlu.ToString();
            kartGuncelleme.Deger = !guncellemelerArandi ? "…" : guncellemeHatasi != null && guncelleme == 0 ? "?" : guncelleme.ToString();

            if (!guncellemelerArandi)
            {
                ozet.Text = M("Güncellemeler aranıyor...", "Searching for updates...");
                altBilgi.Text = string.Format(M("{0} aygıt tarandı  ·  Windows Update kontrol ediliyor", "{0} devices scanned  ·  Checking Windows Update"), taranan);
            }
            else
            {
                if (guncelleme > 0)
                    ozet.Text = string.Format(M("{0} güncelleme bulundu", "{0} update(s) found"), guncelleme)
                        + (sorunlu > 0 ? string.Format(M("  ·  {0} sorunlu aygıt", "  ·  {0} problem device(s)"), sorunlu) : "");
                else if (sorunlu > 0) ozet.Text = string.Format(M("{0} sorunlu aygıt bulundu", "{0} device(s) with problems"), sorunlu);
                else ozet.Text = guncellemeHatasi == null ? M("Sürücüleriniz güncel", "Your drivers are up to date") : M("Güncellemeler aranamadı", "Couldn't search for updates");
                altBilgi.Text = string.Format(M("Son tarama: {0}  ·  Kaynaklar: Windows Update ve resmi üretici siteleri",
                                                "Last scan: {0}  ·  Sources: Windows Update and official manufacturer sites"), sonTarama.ToString("HH:mm"));
            }

            string anahtar = !guncellemelerArandi ? "bilgi" : sorunlu > 0 ? "sorunlu" : guncelleme > 0 ? "guncelleme" : "guncel";
            tepsi.Icon = tepsiSimgeleri[anahtar];
            string ipucu = Ortak.Ad + " - " + ozet.Text.Replace("  ·  ", ", ");
            tepsi.Text = ipucu.Length > 63 ? ipucu.Substring(0, 63) : ipucu;
        }

        void TaramayiGoster()
        {
            KartlariGuncelle();
            int sorunlu = aygitlar.Count(a => a.Durum == Durum.Sorunlu);
            int guncelleme = aygitlar.Count(a => a.Guncelleme != null);
            IEnumerable<Aygit> gosterilecek = tumunuGoster.Checked ? aygitlar : aygitlar.Where(a => a.Durum != Durum.Guncel);
            List<ListViewItem> ogeler = gosterilecek.OrderBy(a => a.Durum).ThenBy(a => a.Sinif).ThenBy(a => a.Ad).Select(AygitSatiri).ToList();
            string bos = !guncellemelerArandi ? M("Güncellemeler aranıyor...", "Searching for updates...")
                : guncellemeHatasi != null ? M("Güncellemeler aranamadı\nİnternet bağlantınızı kontrol edip yeniden tarayın.",
                                               "Couldn't search for updates\nCheck your internet connection and scan again.")
                : M("✔  Her şey yolunda\nSürücüleriniz güncel, sorunlu aygıt yok.", "✔  All good\nYour drivers are up to date and no device has a problem.");
            // Onay kutulari yalnizca kurulabilecek guncelleme varken gosterilir
            ListeyiDoldur(new[] { M("Durum", "Status"), M("Aygıt", "Device"), M("Sınıf", "Class"), M("Mevcut Sürüm", "Current Version"), M("Yeni Sürüm", "New Version"), M("Açıklama", "Details") },
                new[] { 155, 215, 110, 125, 95, 180 }, ogeler, guncelleme > 0, bos);

            if (!guncellemelerArandi) return;
            if (guncellemeHatasi != null)
                durum.Text = string.Format(M("Güncellemeler aranamadı ({0}). İnternet bağlantınızı kontrol edip tekrar tarayın.",
                                             "Couldn't search for updates ({0}). Check your internet connection and scan again."), guncellemeHatasi);
            else if (ogeler.Count == 0)
                durum.Text = M("Tüm aygıtları görmek için \"Tüm aygıtları göster\"i işaretleyin.", "Tick \"Show all devices\" to see every device.");
            else if (guncelleme > 0)
                durum.Text = M("Güncellemek istediklerinizi işaretleyip \"Güncelle\"ye basın. İstemediğiniz bir güncellemeyi gizleyebilirsiniz.",
                               "Tick the updates you want and press \"Update\". You can hide an update you don't want.");
            else if (sorunlu > 0)
                durum.Text = M("Sorunlu bir aygıtı seçip \"Katalogda Ara\" ile sürücüsünü Microsoft'un arşivinde arayabilirsiniz.",
                               "Select a problem device and use \"Search Catalog\" to find its driver in Microsoft's archive.");
            else
                durum.Text = string.Format(M("{0} aygıt listeleniyor. Tüm sürücüleriniz güncel.", "Listing {0} devices. All your drivers are up to date."), ogeler.Count);
        }

        static string DurumMetni(Aygit a)
        {
            switch (a.Durum)
            {
                case Durum.Sorunlu: return M("Sorunlu", "Problem");
                case Durum.GuncellemeVar: return M("Güncelleme var", "Update available");
                default: return M("Güncel", "Up to date");
            }
        }

        static ListViewItem AygitSatiri(Aygit a)
        {
            string yeni = "", aciklama = "";
            if (a.Guncelleme != null)
            {
                yeni = a.Guncelleme.Tarih == DateTime.MinValue ? "?" : a.Guncelleme.Tarih.ToString("yyyy-MM-dd");
                aciklama = a.Guncelleme.Baslik + (a.Guncelleme.Boyut > 0 ? "  ·  " + Sistem.BoyutMetni((long)a.Guncelleme.Boyut) : "");
            }
            if (a.HataKodu != 0) aciklama = Sistem.HataAciklamasi(a.HataKodu) + (aciklama == "" ? "" : "  ·  " + aciklama);

            string simge = a.Durum == Durum.Sorunlu ? "sorunlu" : a.Durum == Durum.GuncellemeVar ? "guncelleme" : "guncel";
            Color renk = a.Durum == Durum.Sorunlu ? Tema.KirmiziYazi : a.Durum == Durum.GuncellemeVar ? Tema.TuruncuYazi : Tema.YesilYazi;
            var oge = new ListViewItem(new[] { DurumMetni(a), a.Ad, a.Sinif, a.Surum == "" ? "-" : a.Surum, yeni, aciklama })
            {
                Tag = a,
                ImageKey = simge,
                Checked = a.Guncelleme != null && a.Secili,
                ToolTipText = aciklama,
                UseItemStyleForSubItems = false
            };
            oge.ForeColor = renk;
            return oge;
        }

        void AyrintiyiGuncelle()
        {
            if (!ayrinti.Visible) return;
            Aygit a = SeciliEtiket<Aygit>();
            if (a == null)
            {
                ayrinti.Bosalt(M("Ayrıntıları görmek için listeden bir aygıt seçin.", "Select a device in the list to see its details."));
                return;
            }
            var satirlar = new List<KeyValuePair<string, string>>();
            Action<string, string> ekle = (baslik, deger) => { if (!string.IsNullOrEmpty(deger)) satirlar.Add(new KeyValuePair<string, string>(baslik, deger)); };
            ekle(M("Durum", "Status"), gorunum == Gorunum.Gizlenenler ? M("Gizlenmiş güncelleme", "Hidden update") : DurumMetni(a));
            if (a.HataKodu != 0) ekle(M("Sorun", "Problem"), Sistem.HataAciklamasi(a.HataKodu));
            ekle(M("Sınıf", "Class"), a.Sinif);
            ekle(M("Üretici", "Manufacturer"), a.Uretici);
            ekle(M("Sürücü sürümü", "Driver version"), a.Surum);
            if (a.Tarih != DateTime.MinValue) ekle(M("Sürücü tarihi", "Driver date"), a.Tarih.ToString("yyyy-MM-dd"));
            ekle(M("Sürücü dosyası", "Driver file"), a.InfAdi);
            if (a.Guncelleme != null)
            {
                ekle(M("Güncelleme", "Update"), a.Guncelleme.Baslik);
                if (a.Guncelleme.Tarih != DateTime.MinValue) ekle(M("Yeni sürüm tarihi", "New version date"), a.Guncelleme.Tarih.ToString("yyyy-MM-dd"));
                if (a.Guncelleme.Boyut > 0) ekle(M("İndirme boyutu", "Download size"), Sistem.BoyutMetni((long)a.Guncelleme.Boyut));
            }
            ekle(M("Aygıt kimliği", "Device instance ID"), a.Kimlik);
            ekle(M("Donanım kimliği", "Hardware ID"), string.Join("\n", a.DonanimKimlikleri.Take(2)));
            ayrinti.Goster(a.Ad, satirlar);
        }

        static string SonucMetni(int kod)
        {
            switch (kod)
            {
                case 2: return M("Başarılı", "Succeeded");
                case 3: return M("Uyarılarla başarılı", "Succeeded with warnings");
                case 4: return M("Başarısız", "Failed");
                case 5: return M("İptal edildi", "Canceled");
                default: return M("Kod ", "Code ") + kod;
            }
        }

        void Guncelle()
        {
            List<Aygit> secilen = aygitlar.Where(a => a.Guncelleme != null && a.Secili).ToList();
            if (secilen.Count == 0 || !YoneticiOl()) return;
            if (!Sor(string.Format(M("{0} sürücü güncellenecek.\n\nÖnce güvenlik için:\n• Sistem geri yükleme noktası oluşturulacak\n• Değişecek sürücülerin yedeği alınacak\n\nDevam edilsin mi?",
                                     "{0} driver(s) will be updated.\n\nFor safety, first:\n• A system restore point will be created\n• The drivers being replaced will be backed up\n\nContinue?"), secilen.Count),
                     MessageBoxIcon.Question)) return;

            List<string> kimlikler = secilen.Select(a => a.Guncelleme.Id).Distinct().ToList();
            List<string> infler = secilen.Select(a => a.InfAdi).ToList();
            Calistir(M("Geri yükleme noktası oluşturuluyor, sürücüler yedekleniyor, güncellemeler indirilip kuruluyor...",
                       "Creating a restore point, backing up drivers, downloading and installing updates..."), () =>
            {
                var mesajlar = new List<string>();
                if (!Sistem.GeriYuklemeNoktasi())
                    mesajlar.Add(M("Not: Geri yükleme noktası oluşturulamadı (Sistem Koruması kapalı olabilir).\n", "Note: Couldn't create a restore point (System Protection may be off).\n"));
                string yedek = Sistem.SecilenleriYedekle(infler, M(" güncelleme öncesi", " before update"));
                if (yedek != null) mesajlar.Add(M("Eski sürücülerin yedeği:\n", "Backup of the old drivers:\n") + yedek + "\n");
                bool yenidenBaslat;
                List<GuncellemeSonucu> sonuclar = Sistem.GuncellemeKur(kimlikler, out yenidenBaslat);
                foreach (GuncellemeSonucu s in sonuclar)
                {
                    Aygit a = secilen.FirstOrDefault(x => x.Guncelleme.Id == s.Id);
                    Gecmis.Ekle("guncelleme", a != null && a.Kimlik != "" ? a.Ad : s.Baslik, a != null ? a.Surum : "", s.Baslik, SonucMetni(s.Kod), s.Basarili);
                    mesajlar.Add("[" + SonucMetni(s.Kod) + "] " + s.Baslik);
                }
                if (sonuclar.Count == 0) mesajlar.Add(M("Seçilen güncellemeler artık bulunamadı.", "The selected updates could not be found anymore."));
                if (yenidenBaslat) mesajlar.Add(M("\nDeğişikliklerin etkili olması için bilgisayarı yeniden başlatın.", "\nRestart the PC for the changes to take effect."));
                return string.Join("\n", mesajlar);
            }, mesaj =>
            {
                MessageBox.Show(this, mesaj, M("Güncelleme tamamlandı", "Update finished"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Tara(false);
            });
        }

        void KatalogdaAra()
        {
            Aygit a = SeciliEtiket<Aygit>();
            if (a == null) return;
            string adres = Sistem.KatalogAdresi(a);
            if (adres == "") return;
            Process.Start(adres);
            durum.Text = M("Katalogda aygıtınıza uyan en yeni sürümü indirin, ardından Bakım sekmesindeki \"Klasörden Yükle\" ile kurun.",
                           "Download the newest matching version from the catalog, then install it with \"Install from Folder\" on the Maintenance tab.");
        }

        // ---------- Aygit onarimi ----------

        void OnarmaMenusunuAc()
        {
            Aygit a = SeciliEtiket<Aygit>();
            if (a == null || a.Kimlik == "") return;
            var menu = new ContextMenuStrip { Renderer = new MenuCizici() };
            menu.Items.Add(M("Aygıtı yeniden başlat", "Restart device"), null, (s, e) => AygitiOnar(a, "restart"));
            menu.Items.Add(M("Sürücüyü yeniden yükle", "Reinstall driver"), null, (s, e) => AygitiOnar(a, "reinstall"));
            menu.Items.Add(new ToolStripSeparator());
            if (a.HataKodu == 22) menu.Items.Add(M("Aygıtı etkinleştir", "Enable device"), null, (s, e) => AygitiOnar(a, "enable"));
            else menu.Items.Add(M("Aygıtı devre dışı bırak", "Disable device"), null, (s, e) => AygitiOnar(a, "disable"));
            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(btnOnar, new Point(0, btnOnar.Height));
        }

        static string OnarimAdi(string islem)
        {
            switch (islem)
            {
                case "restart": return M("Yeniden başlatma", "Restart");
                case "reinstall": return M("Sürücüyü yeniden yükleme", "Driver reinstall");
                case "enable": return M("Etkinleştirme", "Enable");
                default: return M("Devre dışı bırakma", "Disable");
            }
        }

        void AygitiOnar(Aygit a, string islem)
        {
            if (!YoneticiOl()) return;
            string aciklama;
            switch (islem)
            {
                case "restart":
                    aciklama = M("\"{0}\" yeniden başlatılacak. Aygıt birkaç saniye kullanılamaz.", "\"{0}\" will be restarted. The device will be unavailable for a few seconds.");
                    break;
                case "reinstall":
                    aciklama = M("\"{0}\" kaldırılıp Windows tarafından yeniden bulunacak ve sürücüsü yeniden yüklenecek. Aygıt kısa bir süre kullanılamaz; ağ aygıtıysa internet bağlantısı birkaç saniye kesilir.",
                                 "\"{0}\" will be removed, detected again by Windows and its driver reinstalled. The device will be briefly unavailable; for a network device, the internet connection drops for a few seconds.");
                    break;
                case "enable":
                    aciklama = M("\"{0}\" etkinleştirilecek.", "\"{0}\" will be enabled.");
                    break;
                default:
                    aciklama = M("\"{0}\" devre dışı bırakılacak ve siz yeniden etkinleştirene kadar çalışmayacak.\n\nDikkat: Klavye, fare, ekran veya disk gibi bir aygıtı devre dışı bırakmak bilgisayarı kullanılamaz hale getirebilir.",
                                 "\"{0}\" will be disabled and won't work until you enable it again.\n\nCaution: disabling a device such as the keyboard, mouse, display or disk can make the PC unusable.");
                    break;
            }
            if (!Sor(string.Format(aciklama, a.Ad) + M("\n\nDevam edilsin mi?", "\n\nContinue?"), islem == "disable" ? MessageBoxIcon.Warning : MessageBoxIcon.Question)) return;
            Calistir(M("Aygıt üzerinde işlem yapılıyor...", "Working on the device..."), () => Sistem.AygitIslemi(a.Kimlik, islem), sonuc =>
            {
                Gecmis.Ekle("onarim", a.Ad, "", OnarimAdi(islem), sonuc.Basarili ? M("Tamamlandı", "Done") : M("Başarısız", "Failed"), sonuc.Basarili);
                Bilgi(sonuc.Mesaj, sonuc.Basarili);
                YerelYenile();
            });
        }

        // Aygit listesini Windows Update'i yeniden aramadan tazeler
        void YerelYenile()
        {
            Calistir(M("Aygıtlar yeniden taranıyor...", "Rescanning devices..."), Sistem.AygitlariTara, bulunanlar =>
            {
                aygitlar = bulunanlar;
                Sistem.Eslestir(aygitlar, sonGuncellemeler);
                TaramayiGoster();
            });
        }

        // ---------- Gizlenen guncellemeler ----------

        void GuncellemeyiGizle()
        {
            Aygit a = SeciliEtiket<Aygit>();
            if (a == null || a.Guncelleme == null || !YoneticiOl()) return;
            Guncelleme g = a.Guncelleme;
            if (!Sor(string.Format(M("\"{0}\" gizlenecek.\n\nGizlenen güncelleme burada gösterilmez ve Windows Update tarafından da otomatik kurulmaz. İstediğiniz zaman \"Gizlenenler\" bölümünden geri alabilirsiniz.\n\nDevam edilsin mi?",
                                     "\"{0}\" will be hidden.\n\nA hidden update isn't shown here and Windows Update won't install it automatically either. You can undo this anytime from \"Hidden Updates\".\n\nContinue?"), g.Baslik),
                     MessageBoxIcon.Question)) return;
            Calistir(M("Güncelleme gizleniyor...", "Hiding the update..."), () => { Sistem.GizlemeDurumu(g.Id, true); return true; }, sonuc =>
            {
                Gecmis.Ekle("gizle", a.Ad, a.Surum, g.Baslik, M("Gizlendi", "Hidden"), true);
                foreach (Aygit x in aygitlar.Where(x => x.Guncelleme != null && x.Guncelleme.Id == g.Id).ToList())
                {
                    if (x.Kimlik == "") aygitlar.Remove(x);
                    else x.Guncelleme = null;
                }
                TaramayiGoster();
            });
        }

        void GizlenenleriGoster()
        {
            Calistir(M("Gizlenen güncellemeler aranıyor...", "Looking for hidden updates..."), Sistem.GizlenenleriGetir, gizliler =>
            {
                List<ListViewItem> ogeler = gizliler.Select(g =>
                {
                    var a = new Aygit { Ad = g.Model != "" ? g.Model : g.Baslik, Sinif = g.Sinif, Guncelleme = g };
                    return new ListViewItem(new[] { M("Gizli", "Hidden"), a.Ad, a.Sinif, g.Tarih == DateTime.MinValue ? "?" : g.Tarih.ToString("yyyy-MM-dd"), g.Baslik })
                    {
                        Tag = a,
                        ImageKey = "gizli"
                    };
                }).ToList();
                ListeyiDoldur(new[] { M("Durum", "Status"), M("Aygıt", "Device"), M("Sınıf", "Class"), M("Sürüm Tarihi", "Version Date"), M("Güncelleme", "Update") },
                    new[] { 110, 260, 120, 110, 300 }, ogeler, false, M("Gizlenmiş bir güncelleme yok.", "There are no hidden updates."));
                durum.Text = M("Tekrar görmek istediğiniz güncellemeyi seçip \"Tekrar Göster\"e basın.", "Select an update and press \"Unhide\" to see it again.");
            });
        }

        void GizlemeyiKaldir()
        {
            Aygit a = SeciliEtiket<Aygit>();
            if (a == null || a.Guncelleme == null || !YoneticiOl()) return;
            Guncelleme g = a.Guncelleme;
            Calistir(M("Gizleme kaldırılıyor...", "Unhiding the update..."), () => { Sistem.GizlemeDurumu(g.Id, false); return true; }, sonuc =>
            {
                Gecmis.Ekle("goster", a.Ad, "", g.Baslik, M("Tekrar gösterildi", "Unhidden"), true);
                GizlenenleriGoster();
                Bilgi(M("Güncelleme bir sonraki taramada tekrar görünecek.", "The update will appear again on the next scan."), true);
            });
        }

        // ---------- Uretici kaynaklari ----------

        void UreticiKaynaklariniGoster()
        {
            Calistir(M("Donanım ve kurulu üretici programları inceleniyor...", "Checking hardware and installed manufacturer tools..."), Sistem.UreticiKaynaklari, kaynaklar =>
            {
                List<ListViewItem> ogeler = kaynaklar.Select(k => new ListViewItem(new[] { k.Donanim, k.Ad, k.Durum, k.Adres })
                {
                    Tag = k,
                    ImageKey = k.SimgeAnahtari
                }).ToList();
                ListeyiDoldur(new[] { M("Donanım", "Hardware"), M("Resmi Kaynak", "Official Source"), M("Durum", "Status"), M("Adres", "Address") },
                    new[] { 320, 210, 300, 230 }, ogeler, false, M("Tanınan bir donanım üreticisi bulunamadı.", "No known hardware manufacturer was found."));
                durum.Text = M("Bir satırı seçip \"Sayfayı Aç\"a basın veya çift tıklayın. Sayfalar üreticilerin kendi resmi siteleridir.",
                               "Select a row and press \"Open Page\" or double-click it. The pages are the manufacturers' own official sites.");
            });
        }

        void SecilenSayfayiAc()
        {
            Kaynak k = SeciliEtiket<Kaynak>();
            if (k != null && k.Adres.StartsWith("https://")) Process.Start(k.Adres);
        }

        void ResmiAraciKur()
        {
            Kaynak k = SeciliEtiket<Kaynak>();
            if (k == null || k.WingetId == null) return;
            if (!Sor(string.Format(M("{0} resmi kaynağından indirilip kurulacak.\n\nKurulum Windows'un kendi program yükleyicisi (winget) ile yapılır ve programın lisans sözleşmesi kabul edilmiş sayılır.\n\nDevam edilsin mi?",
                                     "{0} will be downloaded from its official source and installed.\n\nInstallation uses Windows Package Manager (winget), and the program's license agreement is accepted.\n\nContinue?"), k.Ad),
                     MessageBoxIcon.Question)) return;
            Calistir(string.Format(M("{0} kuruluyor...", "Installing {0}..."), k.Ad), () => Sistem.WingetKur(k.WingetId), sonuc =>
            {
                Gecmis.Ekle("arac", k.Ad, "", k.WingetId, sonuc.Basarili ? M("Kuruldu", "Installed") : M("Başarısız", "Failed"), sonuc.Basarili);
                Bilgi(sonuc.Mesaj, sonuc.Basarili);
                UreticiKaynaklariniGoster();
            });
        }

        // ---------- Sistem sagligi ----------

        void SaglikGoster()
        {
            Calistir(M("Donanım bilgileri ve sağlık durumu okunuyor...", "Reading hardware information and health..."),
                () => Tuple.Create(Sistem.SaglikBilgisi(), Bakim.PilGecmisi()), sonuc =>
            {
                List<SaglikSatiri> satirlar = sonuc.Item1;
                pilGrafigi.Kayitlar = sonuc.Item2;
                pilGrafigi.Visible = sonuc.Item2.Count >= 2;
                // Grup basliklari kalin, renkli satirlar olarak eklenir (ListView'in kendi grup basligi koyu temada okunmuyor)
                var ogeler = new List<ListViewItem>();
                var baslikYazisi = new Font(liste.Font, FontStyle.Bold);
                string sonGrup = null;
                foreach (SaglikSatiri s in satirlar)
                {
                    if (s.Grup != sonGrup)
                    {
                        ogeler.Add(new ListViewItem(s.Grup) { Font = baslikYazisi, ForeColor = Tema.AracSimge });
                        sonGrup = s.Grup;
                    }
                    var oge = new ListViewItem(new[] { s.Ad, s.Deger, s.Not })
                    {
                        Tag = s,
                        ImageKey = s.SimgeAnahtari,
                        ToolTipText = s.Not,
                        UseItemStyleForSubItems = false
                    };
                    if (s.SimgeAnahtari == "sorunlu") oge.SubItems[1].ForeColor = Tema.KirmiziYazi;
                    else if (s.SimgeAnahtari == "guncelleme") oge.SubItems[1].ForeColor = Tema.TuruncuYazi;
                    else if (s.SimgeAnahtari == "guncel") oge.SubItems[1].ForeColor = Tema.YesilYazi;
                    ogeler.Add(oge);
                }
                ListeyiDoldur(new[] { M("Özellik", "Item"), M("Değer", "Value"), M("Not", "Notes") }, new[] { 240, 380, 400 }, ogeler, false,
                    M("Sistem bilgileri okunamadı.", "System information couldn't be read."));
                durum.Text = satirlar.Any(s => s.SimgeAnahtari == "sorunlu") ? M("Kırmızı satırlar dikkat gerektiriyor.", "The red rows need attention.")
                    : satirlar.Any(s => s.SimgeAnahtari == "guncelleme") ? M("Sarı satırlara göz atmanızı öneririz.", "Have a look at the yellow rows.")
                    : M("Donanımınız sağlıklı görünüyor.", "Your hardware looks healthy.");
            });
        }

        // ---------- Gecmis ----------

        static string IslemAdi(string kod)
        {
            switch (kod)
            {
                case "guncelleme": return M("Sürücü güncelleme", "Driver update");
                case "yukleme": return M("Klasörden yükleme", "Install from folder");
                case "arac": return M("Resmi araç kurulumu", "Official tool install");
                case "temizlik": return M("Eski sürücü silme", "Old driver removal");
                case "gizle": return M("Güncelleme gizleme", "Hide update");
                case "goster": return M("Gizlemeyi kaldırma", "Unhide update");
                case "nokta": return M("Geri yükleme noktası", "Restore point");
                case "geriyukleme": return M("Sistem geri yükleme", "System restore");
                case "yedek": return M("Sürücü yedekleme", "Driver backup");
                case "onarim": return M("Aygıt onarımı", "Device repair");
                case "program": return M("Program güncellemesi", "Program update");
                case "disk": return M("Disk temizliği", "Disk cleanup");
                case "baslangic": return M("Başlangıç programı", "Startup program");
                default: return kod;
            }
        }

        void GecmisiGoster()
        {
            List<ListViewItem> ogeler = Gecmis.Oku().Select(k => new ListViewItem(new[] { k.Tarih.ToString("yyyy-MM-dd HH:mm"), IslemAdi(k.Islem), k.Ayrinti, k.Eski, k.Yeni, k.Sonuc })
            {
                Tag = k,
                ImageKey = k.Basarili ? "guncel" : "sorunlu"
            }).ToList();
            ListeyiDoldur(new[] { M("Tarih", "Date"), M("İşlem", "Action"), M("Aygıt / Ayrıntı", "Device / Details"), M("Önceki", "Before"), M("Sonraki", "After"), M("Sonuç", "Result") },
                new[] { 145, 160, 250, 120, 250, 150 }, ogeler, false,
                M("Henüz kayıtlı bir işlem yok.\nGüncelleme, temizlik ve geri yükleme işlemleri burada listelenir.",
                  "No actions recorded yet.\nUpdates, cleanups and restores will be listed here."));
            durum.Text = string.Format(M("{0} kayıt. Bir sorun çıkarsa, ne zaman neyin değiştiğini buradan görebilirsiniz.",
                                         "{0} entries. If something goes wrong, you can see here what changed and when."), ogeler.Count);
        }

        void GecmisiTemizle()
        {
            if (!Sor(M("Tüm işlem geçmişi silinecek. Devam edilsin mi?", "The whole action history will be deleted. Continue?"), MessageBoxIcon.Question)) return;
            Gecmis.Sil();
            GecmisiGoster();
        }

        // ---------- Bakim: eski surucu temizligi, yedekleme ----------

        void EskiSuruculeriGoster()
        {
            Calistir(M("Eski sürücü paketleri aranıyor...", "Looking for old driver packages..."), Sistem.EskiSurucular, eskiler =>
            {
                List<ListViewItem> ogeler = eskiler.Select(p => new ListViewItem(new[]
                {
                    p.OrijinalAd, p.Saglayici, p.Sinif,
                    p.Surum + (p.Tarih != DateTime.MinValue ? " (" + p.Tarih.ToString("yyyy-MM-dd") + ")" : ""),
                    p.YeniSurum, p.Boyut > 0 ? Sistem.BoyutMetni(p.Boyut) : "?"
                })
                {
                    Tag = p,
                    ImageKey = "guncelleme",
                    Checked = true
                }).ToList();
                ListeyiDoldur(new[] { M("Sürücü Paketi", "Driver Package"), M("Sağlayıcı", "Provider"), M("Sınıf", "Class"), M("Eski Sürüm", "Old Version"), M("Güncel Sürüm", "Current Version"), M("Boyut", "Size") },
                    new[] { 230, 170, 110, 200, 130, 100 }, ogeler, true,
                    M("✔  Temizlenecek eski sürücü yok\nDiskteki tüm sürücü paketleri kullanımda veya en güncel sürüm.",
                      "✔  No old drivers to clean up\nEvery driver package on disk is in use or the newest version."));
                durum.Text = eskiler.Count == 0 ? ""
                    : string.Format(M("{0} eski sürücü paketi, toplam {1}. Silmek istediklerinizi işaretleyip \"Seçilenleri Sil\"e basın. Silmeden önce otomatik yedek alınır.",
                                      "{0} old driver packages, {1} in total. Tick the ones to remove and press \"Remove Selected\". A backup is made automatically first."),
                                    eskiler.Count, Sistem.BoyutMetni(eskiler.Sum(p => p.Boyut)));
            });
        }

        void EskiSuruculeriSil()
        {
            List<EskiSurucu> secilen = liste.CheckedItems.Cast<ListViewItem>().Select(o => o.Tag as EskiSurucu).Where(p => p != null).ToList();
            if (secilen.Count == 0 || !YoneticiOl()) return;
            if (!Sor(string.Format(M("{0} eski sürücü paketi silinecek.\n\nBunlar hiçbir aygıtın kullanmadığı eski sürümlerdir; kullanımdaki bir sürücü silinmez. Silmeden önce yedekleri Belgeler\\Sürücü Yedekleri klasörüne alınır.\n\nNot: Silinen bir sürüme Aygıt Yöneticisi'ndeki \"Sürücüyü Geri Al\" ile dönülemez; gerekirse yedekten \"Klasörden Yükle\" ile kurabilirsiniz. Yeni bir güncellemeden emin değilseniz, birkaç gün bekleyip öyle temizleyin.\n\nDevam edilsin mi?",
                                     "{0} old driver package(s) will be removed.\n\nThese are old versions no device uses; a driver in use is never removed. They are backed up to Documents\\Sürücü Yedekleri first.\n\nNote: you can't go back to a removed version with \"Roll Back Driver\" in Device Manager; if needed, install it from the backup with \"Install from Folder\". If you're unsure about a recent update, wait a few days before cleaning up.\n\nContinue?"), secilen.Count),
                     MessageBoxIcon.Question)) return;
            Calistir(M("Eski sürücüler yedekleniyor ve siliniyor...", "Backing up and removing old drivers..."), () =>
            {
                string yedek = Sistem.SecilenleriYedekle(secilen.Select(p => p.Inf), M(" temizlik öncesi", " before cleanup"));
                Dictionary<string, bool> sonuc = Sistem.SurucuPaketiSil(secilen.Select(p => p.Inf));
                foreach (EskiSurucu p in secilen)
                {
                    bool silindi;
                    sonuc.TryGetValue(p.Inf, out silindi);
                    Gecmis.Ekle("temizlik", p.OrijinalAd + " (" + p.Saglayici + ")", p.Surum, "",
                        silindi ? M("Silindi", "Removed") : M("Silinemedi (kullanımda olabilir)", "Not removed (may be in use)"), silindi);
                }
                return string.Format(M("{0} / {1} paket silindi.", "{0} of {1} packages removed."), sonuc.Count(x => x.Value), secilen.Count)
                    + (yedek != null ? "\n\n" + M("Silinenlerin yedeği:", "Backup of the removed drivers:") + "\n" + yedek : "");
            }, mesaj =>
            {
                Bilgi(mesaj, true);
                EskiSuruculeriGoster();
            });
        }

        void Yedekle()
        {
            if (!YoneticiOl()) return;
            if (!Sor(string.Format(M("Bilgisayardaki tüm üretici sürücüleri şu klasöre yedeklenecek:\n{0}\n\nBu birkaç dakika sürebilir ve birkaç GB yer kaplayabilir. Windows'u yeniden kurarsanız sürücüleri bu yedekten \"Klasörden Yükle\" ile geri yükleyebilirsiniz.\n\nDevam edilsin mi?",
                                     "All manufacturer drivers on this PC will be backed up to:\n{0}\n\nThis may take a few minutes and a few GB. If you reinstall Windows, you can restore them with \"Install from Folder\".\n\nContinue?"), Sistem.YedekKlasoru),
                     MessageBoxIcon.Question)) return;
            Calistir(M("Sürücüler yedekleniyor...", "Backing up drivers..."), Sistem.TumunuYedekle, sonuc =>
            {
                Gecmis.Ekle("yedek", sonuc.Klasor ?? "", "", "", sonuc.Basarili ? M("Tamamlandı", "Completed") : M("Başarısız", "Failed"), sonuc.Basarili);
                durum.Text = sonuc.Basarili ? M("Yedekleme tamamlandı.", "Backup complete.") : M("Yedekleme başarısız oldu.", "Backup failed.");
                if (!sonuc.Basarili)
                {
                    Bilgi(sonuc.Mesaj, false);
                    return;
                }
                if (Sor(sonuc.Mesaj + M("\n\nKlasör açılsın mı?", "\n\nOpen the folder?"), MessageBoxIcon.Information))
                    Process.Start("explorer.exe", "\"" + sonuc.Klasor + "\"");
            });
        }

        void KlasordenYukle()
        {
            if (!YoneticiOl()) return;
            using (var pencere = new FolderBrowserDialog
            {
                Description = M("Yüklenecek sürücülerin bulunduğu klasörü seçin (bir yedek klasörü veya Microsoft Update Kataloğu'ndan indirdiğiniz .cab dosyalarının olduğu klasör).",
                                "Choose the folder with the drivers to install (a backup folder, or a folder with .cab files downloaded from the Microsoft Update Catalog)."),
                SelectedPath = Directory.Exists(Sistem.YedekKlasoru) ? Sistem.YedekKlasoru : "",
                ShowNewFolderButton = false
            })
            {
                if (pencere.ShowDialog(this) != DialogResult.OK) return;
                string klasor = pencere.SelectedPath;
                if (!Sor(string.Format(M("Bu klasördeki sürücüler Windows'a yüklenecek:\n{0}\n\nNot: Yedekteki sürücü bilgisayardakinden eskiyse Windows yeni olanı kullanmaya devam edebilir. Bir güncellemeyi geri almanın en kesin yolu, Geri Yükleme sekmesindeki geri yükleme noktalarıdır.\n\nDevam edilsin mi?",
                                         "The drivers in this folder will be installed:\n{0}\n\nNote: if a backed-up driver is older than the one on this PC, Windows may keep using the newer one. The surest way to undo an update is a restore point on the System Restore tab.\n\nContinue?"), klasor),
                         MessageBoxIcon.Question)) return;
                Calistir(M("Sürücüler yükleniyor...", "Installing drivers..."), () =>
                {
                    Sistem.GeriYuklemeNoktasi();
                    return Sistem.KlasordenYukle(klasor);
                }, sonuc =>
                {
                    Gecmis.Ekle("yukleme", klasor, "", "", sonuc.Basarili ? M("Yüklendi", "Installed") : M("Kısmen veya hiç yüklenmedi", "Partly or not installed"), sonuc.Basarili);
                    Bilgi(sonuc.Mesaj, sonuc.Basarili);
                });
            }
        }

        // ---------- Disk temizligi ----------

        void DiskTemizliginiGoster()
        {
            Calistir(M("Temizlenebilecek dosyalar hesaplanıyor...", "Calculating files that can be cleaned up..."), Bakim.TemizlikKalemleri, kalemler =>
            {
                bool yonetici = Sistem.YoneticiMi();
                List<ListViewItem> ogeler = kalemler.Select(k =>
                {
                    // Bazi sistem klasorleri okunabilir ama silinemez; boyut okunabildiyse gosterilir
                    bool erisilemez = k.YoneticiGerekir && !yonetici && k.Boyut == 0;
                    var oge = new ListViewItem(new[]
                    {
                        k.Ad,
                        erisilemez ? "?" : Sistem.BoyutMetni(k.Boyut),
                        erisilemez ? "" : k.DosyaSayisi.ToString(),
                        k.Aciklama + (erisilemez ? M(" · boyut için yönetici olarak açın", " · run as administrator to see the size") : ""),
                        k.Konum
                    })
                    {
                        Tag = k,
                        ImageKey = k.Anahtar == "geri-donusum" ? "sorunlu" : k.Boyut > 0 || erisilemez ? "guncelleme" : "guncel",
                        Checked = k.VarsayilanSecili && (k.Boyut > 0 || erisilemez),
                        ToolTipText = k.Aciklama
                    };
                    return oge;
                }).ToList();
                ListeyiDoldur(new[] { M("Kategori", "Category"), M("Boyut", "Size"), M("Dosya", "Files"), M("Açıklama", "Details"), M("Konum", "Location") },
                    new[] { 230, 100, 70, 420, 300 }, ogeler, true, M("Temizlenecek bir şey bulunamadı.", "Nothing to clean up."));
                long toplam = kalemler.Where(k => k.Anahtar != "geri-donusum").Sum(k => k.Boyut);
                durum.Text = string.Format(M("Temizlenebilecek alan: {0}. Kullanımdaki dosyalar atlanır. Geri Dönüşüm Kutusu ancak siz işaretlerseniz boşaltılır.",
                                             "Space that can be freed: {0}. Files in use are skipped. The Recycle Bin is only emptied if you tick it."), Sistem.BoyutMetni(toplam))
                    + (yonetici ? "" : M(" Sistem klasörleri için programı yönetici olarak açın.", " Run as administrator for system folders."));
            });
        }

        void DiskiTemizle()
        {
            List<TemizlikKalemi> secilen = liste.CheckedItems.Cast<ListViewItem>().Select(o => o.Tag as TemizlikKalemi).Where(k => k != null).ToList();
            if (secilen.Count == 0) return;
            if (secilen.Any(k => k.YoneticiGerekir) && !YoneticiOl()) return;
            string adlar = string.Join("\n", secilen.Select(k => "• " + k.Ad));
            bool geriDonusum = secilen.Any(k => k.Anahtar == "geri-donusum");
            if (!Sor(string.Format(M("Şunlar temizlenecek:\n{0}\n\nBu dosyalar kalıcı olarak silinir.{1}\n\nDevam edilsin mi?",
                                     "The following will be cleaned up:\n{0}\n\nThese files are deleted permanently.{1}\n\nContinue?"), adlar,
                                   geriDonusum ? M("\nGeri Dönüşüm Kutusu'ndaki dosyalar da geri alınamaz.", "\nFiles in the Recycle Bin can't be recovered either.") : ""),
                     geriDonusum ? MessageBoxIcon.Warning : MessageBoxIcon.Question)) return;
            List<string> anahtarlar = secilen.Select(k => k.Anahtar).ToList();
            Calistir(M("Dosyalar temizleniyor...", "Cleaning up files..."), () => Bakim.Temizle(anahtarlar), bosaltilan =>
            {
                Gecmis.Ekle("disk", string.Join(", ", secilen.Select(k => k.Ad)), "", Sistem.BoyutMetni(bosaltilan), M("Tamamlandı", "Done"), true);
                Bilgi(string.Format(M("{0} alan boşaltıldı.", "{0} freed."), Sistem.BoyutMetni(bosaltilan)), true);
                DiskTemizliginiGoster();
            });
        }

        // ---------- Baslangic programlari ----------

        void BaslangicGoster()
        {
            Calistir(M("Başlangıç programları okunuyor...", "Reading startup programs..."), Bakim.BaslangicProgramlari, ogeler =>
            {
                List<ListViewItem> satirlar = ogeler.Select(o =>
                {
                    var oge = new ListViewItem(new[] { o.Etkin ? M("Açık", "Enabled") : M("Kapalı", "Disabled"), o.Ad, Bakim.KonumAdi(o.Konum), o.Komut })
                    {
                        Tag = o,
                        ImageKey = o.Etkin ? "guncel" : "gizli",
                        ToolTipText = o.Komut,
                        UseItemStyleForSubItems = false
                    };
                    oge.ForeColor = o.Etkin ? Tema.YesilYazi : Tema.Soluk;
                    return oge;
                }).ToList();
                ListeyiDoldur(new[] { M("Durum", "Status"), M("Program", "Program"), M("Konum", "Location"), M("Komut", "Command") },
                    new[] { 110, 260, 250, 400 }, satirlar, false, M("Windows açılışında başlayan program yok.", "No programs start with Windows."));
                durum.Text = string.Format(M("{0} program, {1} tanesi açık. Kapattığınız program silinmez; yalnızca Windows açılırken kendiliğinden başlamaz.",
                                             "{0} programs, {1} enabled. A disabled program isn't removed; it just doesn't start with Windows."),
                                           ogeler.Count, ogeler.Count(o => o.Etkin));
            });
        }

        void BaslangicDurumunuDegistir()
        {
            BaslangicOgesi o = SeciliEtiket<BaslangicOgesi>();
            if (o == null) return;
            if (o.YoneticiGerekir && !YoneticiOl()) return;
            bool yeni = !o.Etkin;
            try
            {
                Bakim.BaslangicDurumu(o, yeni);
                Gecmis.Ekle("baslangic", o.Ad, yeni ? M("Kapalı", "Disabled") : M("Açık", "Enabled"), yeni ? M("Açık", "Enabled") : M("Kapalı", "Disabled"), M("Tamamlandı", "Done"), true);
            }
            catch (Exception h)
            {
                Bilgi(M("Değiştirilemedi: ", "Couldn't change it: ") + h.Message, false);
            }
            BaslangicGoster();
        }

        // ---------- Geri yukleme noktalari ----------

        static string NoktaTuru(uint tur)
        {
            switch (tur)
            {
                case 0: return M("Program kurulumu", "Application install");
                case 1: return M("Program kaldırma", "Application uninstall");
                case 10: return M("Sürücü kurulumu", "Driver install");
                case 12: return M("Ayar değişikliği", "Settings change");
                case 13: return M("İptal edilen işlem", "Canceled operation");
                default: return tur.ToString();
            }
        }

        void GeriYuklemeNoktalariniGoster()
        {
            if (!Sistem.YoneticiMi())
            {
                ListeyiDoldur(new string[0], new int[0], new List<ListViewItem>(), false,
                    M("Geri yükleme noktalarını görmek ve yönetmek için\nprogramın yönetici olarak açılması gerekiyor.\n\"Yönetici Olarak Aç\" butonuna basın.",
                      "Viewing and managing restore points\nrequires administrator rights.\nPress \"Run as Administrator\"."));
                durum.Text = "";
                return;
            }
            Calistir(M("Geri yükleme noktaları okunuyor...", "Reading restore points..."), Sistem.GeriYuklemeNoktalari, noktalar =>
            {
                List<ListViewItem> ogeler = noktalar.Select(n => new ListViewItem(new[]
                {
                    n.Tarih == DateTime.MinValue ? "?" : n.Tarih.ToString("yyyy-MM-dd HH:mm"), n.Aciklama, NoktaTuru(n.Tur), n.Sira.ToString()
                })
                {
                    Tag = n,
                    ImageKey = "bilgi"
                }).ToList();
                ListeyiDoldur(new[] { M("Tarih", "Date"), M("Açıklama", "Description"), M("Tür", "Type"), M("Sıra No", "Number") },
                    new[] { 160, 420, 200, 90 }, ogeler, false,
                    M("Hiç geri yükleme noktası yok.\nSistem Koruması kapalı olabilir. \"Yeni Nokta Oluştur\" ile deneyebilirsiniz.",
                      "There are no restore points.\nSystem Protection may be off. Try \"Create Restore Point\"."));
                durum.Text = M("Bir noktayı seçip \"Bu Noktaya Dön\"e basarak bilgisayarı o tarihteki haline döndürebilirsiniz. Kişisel dosyalarınız etkilenmez.",
                               "Select a point and press \"Restore to This Point\" to return the PC to that state. Your personal files are not affected.");
            });
        }

        void YeniNoktaOlustur()
        {
            if (!YoneticiOl()) return;
            Calistir(M("Geri yükleme noktası oluşturuluyor...", "Creating a restore point..."), () =>
            {
                int once = Sistem.GeriYuklemeNoktalari().Count;
                bool basarili = Sistem.GeriYuklemeNoktasi(M("Sürücü Aracı - elle oluşturuldu", "Driver Tool - created manually"));
                int sonra = Sistem.GeriYuklemeNoktalari().Count;
                return !basarili ? -1 : sonra > once ? 1 : 0;
            }, sonuc =>
            {
                if (sonuc == 1) Gecmis.Ekle("nokta", M("Elle oluşturuldu", "Created manually"), "", "", M("Oluşturuldu", "Created"), true);
                Bilgi(sonuc == 1 ? M("Geri yükleme noktası oluşturuldu.", "Restore point created.")
                    : sonuc == 0 ? M("Windows son 24 saat içinde zaten bir geri yükleme noktası oluşturduğu için yenisini oluşturmadı. Listedeki en yeni nokta kullanılabilir.",
                                     "Windows didn't create a new point because one was already created in the last 24 hours. The newest point in the list can be used.")
                    : M("Geri yükleme noktası oluşturulamadı. Sistem Koruması kapalı olabilir.", "Couldn't create a restore point. System Protection may be off."), sonuc >= 0);
                GeriYuklemeNoktalariniGoster();
            });
        }

        void NoktayaDon()
        {
            GeriNokta n = SeciliEtiket<GeriNokta>();
            if (n == null || !YoneticiOl()) return;
            if (!Sor(string.Format(M("Bilgisayar {0} tarihindeki haline döndürülecek:\n\"{1}\"\n\n• Kişisel dosyalarınız etkilenmez.\n• Bu tarihten sonra kurulan programlar, sürücüler ve güncellemeler kaldırılır.\n• İşlemin tamamlanması için bilgisayar yeniden başlatılır. Açık dosyalarınızı kaydedin.\n\nDevam edilsin mi?",
                                     "The PC will be returned to its state on {0}:\n\"{1}\"\n\n• Your personal files are not affected.\n• Programs, drivers and updates installed after this date will be removed.\n• The PC will restart to finish. Save your open files.\n\nContinue?"),
                                   n.Tarih.ToString("yyyy-MM-dd HH:mm"), n.Aciklama), MessageBoxIcon.Warning)) return;
            Calistir(M("Sistem geri yükleme hazırlanıyor...", "Preparing system restore..."), () => Sistem.GeriDon(n.Sira), basarili =>
            {
                if (!basarili)
                {
                    Bilgi(M("Sistem geri yükleme başlatılamadı. \"Windows Sistem Geri Yükleme\" ile Windows'un kendi aracını deneyebilirsiniz.",
                            "System restore couldn't be started. You can try Windows' own tool with \"Windows System Restore\"."), false);
                    return;
                }
                Gecmis.Ekle("geriyukleme", n.Aciklama, "", n.Tarih.ToString("yyyy-MM-dd HH:mm"), M("Başlatıldı", "Started"), true);
                if (Sor(M("Geri yükleme hazır. Tamamlanması için bilgisayarın yeniden başlatılması gerekiyor.\n\nŞimdi yeniden başlatılsın mı?",
                          "The restore is ready. The PC must restart to finish it.\n\nRestart now?"), MessageBoxIcon.Question))
                    Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/r /t 5") { CreateNoWindow = true, UseShellExecute = false });
            });
        }

        void SistemGeriYuklemeyiAc()
        {
            try { Process.Start(Path.Combine(Environment.SystemDirectory, "rstrui.exe")); }
            catch (Win32Exception) { }  // kullanici UAC penceresinde Hayir dedi
        }

        // ---------- Programin kendi guncellemesi ----------

        // elle: kullanici istedi; sonuc her durumda bildirilir. Otomatikte gunde en fazla bir kez denetlenir.
        void ProgramGuncellemesiniDenetle(bool elle)
        {
            if (!Ortak.KendiniGuncelle) return;
            if (!elle &&(!Ayarlar.ProgramGuncellemesi || DateTime.Now - Ayarlar.SonProgramDenetimi < TimeSpan.FromDays(1))) return;
            Task.Factory.StartNew(() => Guncelleyici.YeniSurumuDenetle()).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                if (t.IsFaulted)
                {
                    if (elle) Bilgi(M("Program güncellemesi denetlenemedi: ", "Couldn't check for a program update: ") + t.Exception.GetBaseException().Message, false);
                    return;
                }
                Ayarlar.SonProgramDenetimi = DateTime.Now;
                yeniSurum = t.Result;
                btnYeniSurum.Visible = yeniSurum != null;
                if (yeniSurum == null)
                {
                    if (elle) Bilgi(string.Format(M("Sürücü Aracı güncel (sürüm {0}).", "Driver Tool is up to date (version {0})."), Ortak.Surum), true);
                    return;
                }
                btnYeniSurum.Text = string.Format(M("Sürüm {0} hazır", "Version {0} available"), yeniSurum.Surum);
                if (elle) YeniSurumuGoster();
                else if (!Visible && tepsi.Visible && Ayarlar.Bildirim)
                    tepsi.ShowBalloonTip(10000, Ortak.Ad, string.Format(M("Sürücü Aracı'nın yeni sürümü ({0}) yayınlandı. Görmek için tıklayın.",
                                                                          "A new version of Driver Tool ({0}) is available. Click to view."), yeniSurum.Surum), ToolTipIcon.Info);
            })));
        }

        void YeniSurumuGoster()
        {
            if (yeniSurum == null || mesgul) return;
            SurumBilgisi bilgi = yeniSurum;
            string notlar = bilgi.Notlar.Trim();
            if (notlar.Length > 700) notlar = notlar.Substring(0, 700) + "...";
            if (!Sor(string.Format(M("Sürücü Aracı {0} yayınlandı (şu anki sürüm: {1}).\n\n{2}\n\nŞimdi güncellensin mi? Program kendini günceller ve yeniden açılır.",
                                     "Driver Tool {0} is available (current version: {1}).\n\n{2}\n\nUpdate now? The program updates itself and reopens."),
                                   bilgi.Surum, Ortak.Surum, notlar), MessageBoxIcon.Question)) return;
            Calistir(M("Yeni sürüm indiriliyor...", "Downloading the new version..."), () => { Guncelleyici.Kur(bilgi); return true; }, sonuc =>
            {
                Gecmis.Ekle("program", Ortak.Ad, Ortak.Surum, bilgi.Surum.ToString(), M("Güncellendi", "Updated"), true);
                Process.Start(Application.ExecutablePath, "/devral");
                Cik();
            });
        }

        // ---------- Ayarlar ----------

        void AyarlariAc()
        {
            if (mesgul) return;
            using (var pencere = new AyarlarFormu())
            {
                if (pencere.ShowDialog(this) != DialogResult.OK) return;
                if (pencere.TemaDegisti)
                {
                    Tema.Uygula(Ayarlar.TemaSecimi);
                    TemayiUygula(true);
                }
                tepsi.Visible = Ayarlar.TepsideCalis;
                if (pencere.DenetimIstendi) ProgramGuncellemesiniDenetle(true);
                if (pencere.KorumaIstegi.HasValue) OtomatikKorumayiAyarla(pencere.KorumaIstegi.Value);
                if (pencere.DilDegisti && Sor(M("Dil değişikliğinin uygulanması için program yeniden başlatılmalı. Şimdi yeniden başlatılsın mı?",
                                                "The program must restart to apply the language change. Restart now?"), MessageBoxIcon.Question))
                {
                    Process.Start(Application.ExecutablePath, "/devral");
                    Cik();
                }
            }
        }

        // Zamanlanmis gorevi kurar veya kaldirir; Windows bu islem icin bir kez izin (UAC) ister
        void OtomatikKorumayiAyarla(bool acik)
        {
            Calistir(acik ? M("Otomatik koruma kuruluyor...", "Setting up automatic protection...") : M("Otomatik koruma kapatılıyor...", "Turning off automatic protection..."),
                () => Bakim.KorumaGoreviniAyarla(acik), basarili =>
            {
                if (basarili) Ayarlar.OtomatikKoruma = acik;
                Bilgi(!basarili ? M("İşlem yapılamadı (izin verilmemiş olabilir). Ayarlardan tekrar deneyebilirsiniz.", "It couldn't be done (permission may have been denied). You can try again in Settings.")
                    : acik ? M("Otomatik koruma açıldı. Her pazar (bilgisayar kapalıysa ilk açılışta) geri yükleme noktası, ayda bir sürücü yedeği alınacak.",
                               "Automatic protection is on. A restore point will be created every Sunday (or at the next start if the PC is off) and a driver backup monthly.")
                    : M("Otomatik koruma kapatıldı.", "Automatic protection is off."), basarili);
            });
        }

        // ---------- Rapor ----------

        static string RaporRengi(ListViewItem oge)
        {
            switch (oge.ImageKey)
            {
                case "sorunlu": return "#ffd7d7";
                case "guncelleme": return "#fff3b4";
                case "guncel": return "#d6f0d6";
                default: return null;
            }
        }

        void RaporKaydet()
        {
            if (liste.Items.Count == 0) return;
            using (var pencere = new SaveFileDialog
            {
                Filter = M("HTML dosyası", "HTML file") + "|*.html",
                FileName = M("SurucuRaporu_", "DriverReport_") + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".html",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            })
            {
                if (pencere.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.Append("<!doctype html><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(GorunumBasligi()) + "</title>");
                sb.Append("<style>body{font-family:Segoe UI,sans-serif;margin:24px}table{border-collapse:collapse}td,th{border:1px solid #ccc;padding:4px 8px;font-size:13px;text-align:left}th{background:#eee}</style>");
                sb.AppendFormat("<h1>{0}</h1><p>{1} &middot; {2} &middot; {3:yyyy-MM-dd HH:mm}</p><table><tr>",
                    WebUtility.HtmlEncode(GorunumBasligi()), WebUtility.HtmlEncode(Environment.MachineName), WebUtility.HtmlEncode(ozet.Text), DateTime.Now);
                foreach (ColumnHeader sutun in liste.Columns) sb.Append("<th>" + WebUtility.HtmlEncode(sutun.Text) + "</th>");
                sb.Append("</tr>");
                foreach (ListViewItem oge in liste.Items)
                {
                    string renk = RaporRengi(oge);
                    sb.Append(renk == null ? "<tr>" : "<tr style=\"background:" + renk + "\">");
                    foreach (ListViewItem.ListViewSubItem hucre in oge.SubItems) sb.Append("<td>" + WebUtility.HtmlEncode(hucre.Text) + "</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
                File.WriteAllText(pencere.FileName, sb.ToString(), Encoding.UTF8);
                durum.Text = M("Rapor kaydedildi: ", "Report saved: ") + pencere.FileName;
                Process.Start(pencere.FileName);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tepsi.Dispose();
                kontrolZamanlayicisi.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
