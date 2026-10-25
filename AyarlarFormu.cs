// AyarlarFormu.cs - Tema, dil ve arka plan calisma ayarlari
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SurucuAraci
{
    class AyarlarFormu : Form
    {
        static readonly string[] TemaDegerleri = { "sistem", "acik", "koyu" };
        static readonly string[] DilDegerleri = { "sistem", "tr", "en" };
        static readonly int[] KontrolDegerleri = { 0, 1, 7 };

        static string M(string tr, string en) { return Dil.M(tr, en); }

        readonly float olcek;
        readonly ComboBox temaSecimi, dilSecimi, kontrolSecimi;
        readonly CheckBox tepsi, baslangic, bildirim, programGuncellemesi;

        public bool TemaDegisti { get; private set; }
        public bool DilDegisti { get; private set; }
        public bool DenetimIstendi { get; private set; }  // "Simdi denetle" ile kapatildi

        public AyarlarFormu()
        {
            Text = M("Ayarlar", "Settings");
            Font = new Font("Segoe UI", 9.75f);
            using (Graphics g = CreateGraphics()) olcek = g.DpiX / 96f;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Tema.Zemin;
            ForeColor = Tema.Yazi;

            var duzen = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(O(24), O(18), O(24), O(8)), Margin = Padding.Empty };
            duzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            duzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            temaSecimi = Secim(new[] { M("Windows ayarına uy", "Follow Windows"), M("Açık", "Light"), M("Koyu", "Dark") },
                Array.IndexOf(TemaDegerleri, Ayarlar.TemaSecimi));
            dilSecimi = Secim(new[] { M("Windows diline uy", "Follow Windows"), "Türkçe", "English" },
                Array.IndexOf(DilDegerleri, Ayarlar.DilSecimi));
            kontrolSecimi = Secim(new[] { M("Kapalı", "Off"), M("Her gün", "Every day"), M("Her hafta", "Every week") },
                Array.IndexOf(KontrolDegerleri, Ayarlar.KontrolGunu));
            tepsi = Onay(M("Pencere kapatılınca sistem tepsisinde çalışmaya devam et", "Keep running in the system tray when the window is closed"), Ayarlar.TepsideCalis);
            baslangic = Onay(M("Windows açılınca sistem tepsisinde başlat", "Start in the system tray when Windows starts"), Sistem.BaslangictaCalisir);
            bildirim = Onay(M("Güncelleme bulununca bildirim göster", "Show a notification when updates are found"), Ayarlar.Bildirim);
            tepsi.CheckedChanged += (s, e) => baslangic.Enabled = tepsi.Checked;
            baslangic.Enabled = tepsi.Checked;

            Bolum(duzen, M("Görünüm", "Appearance"));
            Satir(duzen, M("Tema", "Theme"), temaSecimi);
            Satir(duzen, M("Dil", "Language"), dilSecimi);
            Bolum(duzen, M("Arka planda çalışma", "Background"));
            Tam(duzen, tepsi);
            Tam(duzen, baslangic);
            Satir(duzen, M("Otomatik kontrol", "Automatic check"), kontrolSecimi);
            Tam(duzen, bildirim);
            var not = new Label
            {
                Text = M("Otomatik kontrol, program açıkken veya sistem tepsisindeyken yapılır.", "Automatic checks run while the program is open or in the system tray."),
                AutoSize = true,
                MaximumSize = new Size(O(460), 0),
                ForeColor = Tema.Soluk,
                Margin = new Padding(0, O(2), 0, O(6))
            };
            Tam(duzen, not);

            Bolum(duzen, string.Format(M("Program (sürüm {0})", "Program (version {0})"), Ortak.Surum));
            programGuncellemesi = Onay(M("Yeni sürümleri otomatik denetle (GitHub)", "Check for new versions automatically (GitHub)"), Ayarlar.ProgramGuncellemesi);
            if (Ortak.KendiniGuncelle) Tam(duzen, programGuncellemesi);
            var simdiDenetle = new Dugme(M("Şimdi denetle", "Check now"), "\uE72C", DugmeTuru.Arac, Font) { Margin = new Padding(0, O(4), 0, O(4)) };
            simdiDenetle.Click += (s, e) => { DenetimIstendi = true; Kaydet(); };
            if (Ortak.KendiniGuncelle) Tam(duzen, simdiDenetle);

            var butonlar = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Anchor = AnchorStyles.Right, Margin = new Padding(0, O(14), 0, O(8)) };
            var kaydet = new Dugme(M("Kaydet", "Save"), "\uE74E", DugmeTuru.Birincil, new Font("Segoe UI Semibold", 10f)) { Margin = new Padding(O(8), 0, 0, 0) };
            var iptal = new Dugme(M("İptal", "Cancel"), "\uE711", DugmeTuru.Arac, Font);
            kaydet.Click += (s, e) => Kaydet();
            iptal.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            butonlar.Controls.Add(kaydet);
            butonlar.Controls.Add(iptal);
            Tam(duzen, butonlar);

            Controls.Add(duzen);
            HandleCreated += (s, e) => Tema.BaslikCubugu(this);
        }

        int O(int piksel) { return (int)(piksel * olcek); }

        ComboBox Secim(string[] secenekler, int secili)
        {
            var kutu = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = O(220), Margin = new Padding(0, O(4), 0, O(4)) };
            kutu.Items.AddRange(secenekler);
            kutu.SelectedIndex = Math.Max(0, secili);
            Tema.Secim(kutu);
            return kutu;
        }

        CheckBox Onay(string metin, bool isaretli)
        {
            var kutu = new CheckBox { Text = metin, Checked = isaretli, AutoSize = true, Margin = new Padding(0, O(4), 0, O(4)) };
            Tema.OnayKutusu(kutu);
            kutu.ForeColor = Tema.Yazi;
            return kutu;
        }

        void Bolum(TableLayoutPanel duzen, string baslik)
        {
            var etiket = new Label
            {
                Text = baslik,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = Tema.Yazi,
                Margin = new Padding(0, duzen.Controls.Count == 0 ? 0 : O(16), 0, O(6))
            };
            Tam(duzen, etiket);
        }

        void Satir(TableLayoutPanel duzen, string etiketMetni, Control kontrol)
        {
            var etiket = new Label { Text = etiketMetni, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Tema.Yazi, Margin = new Padding(0, 0, O(24), 0) };
            duzen.Controls.Add(etiket);
            duzen.Controls.Add(kontrol);
        }

        void Tam(TableLayoutPanel duzen, Control kontrol)
        {
            duzen.Controls.Add(kontrol);
            duzen.SetColumnSpan(kontrol, 2);
        }

        void Kaydet()
        {
            string tema = TemaDegerleri[temaSecimi.SelectedIndex], dil = DilDegerleri[dilSecimi.SelectedIndex];
            TemaDegisti = tema != Ayarlar.TemaSecimi;
            DilDegisti = dil != Ayarlar.DilSecimi;
            Ayarlar.TemaSecimi = tema;
            Ayarlar.DilSecimi = dil;
            Ayarlar.TepsideCalis = tepsi.Checked;
            Ayarlar.Bildirim = bildirim.Checked;
            Ayarlar.KontrolGunu = KontrolDegerleri[kontrolSecimi.SelectedIndex];
            Ayarlar.ProgramGuncellemesi = programGuncellemesi.Checked;
            try
            {
                // Windows ile baslatma tepsi olmadan anlamsiz: pencere hic gorunmezdi
                Sistem.BaslangictaCalisir = tepsi.Checked && baslangic.Checked;
            }
            catch (Exception) { }
            DialogResult = DialogResult.OK;
        }
    }
}
