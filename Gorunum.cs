// Gorunum.cs - Tema renkleri ve ozel cizilen kontroller (program ve kurulum dosyasi birlikte kullanir)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SurucuAraci
{
    // Renkler simgedeki lacivert ve maviden alindi
    static class Tema
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int ozellik, ref int deger, int boyut);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string uygulama, string kimlik);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int mesaj, IntPtr w, IntPtr l);

        public static bool Koyu { get; private set; }
        public static Color Zemin, Yuzey, YuzeyUzerinde, YuzeyBasili, Kenarlik, KenarlikVurgu, Yazi, Soluk, PasifYazi, AracSimge;
        public static Color KirmiziYazi, TuruncuYazi, YesilYazi;

        public static readonly Color Vurgu = Color.FromArgb(47, 143, 240);
        public static readonly Color Lacivert1 = Color.FromArgb(58, 91, 128);
        public static readonly Color Lacivert2 = Color.FromArgb(27, 43, 66);
        public static readonly Color Kirmizi = Color.FromArgb(229, 72, 77);
        public static readonly Color Turuncu = Color.FromArgb(245, 165, 36);
        public static readonly Color Yesil = Color.FromArgb(48, 164, 108);
        public static readonly Color Mavi = Color.FromArgb(62, 142, 208);
        public static readonly Color Gri = Color.FromArgb(150, 160, 172);

        // Windows 11'de Segoe Fluent Icons, Windows 10'da Segoe MDL2 Assets
        public static readonly string SimgeFontu =
            FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

        // secim: sistem, acik, koyu
        public static void Uygula(string secim)
        {
            Koyu = secim == "koyu" || (secim != "acik" && SistemKoyuMu());
            if (Koyu)
            {
                Zemin = Color.FromArgb(22, 26, 32);
                Yuzey = Color.FromArgb(32, 37, 45);
                YuzeyUzerinde = Color.FromArgb(42, 50, 62);
                YuzeyBasili = Color.FromArgb(50, 62, 80);
                Kenarlik = Color.FromArgb(52, 61, 74);
                KenarlikVurgu = Color.FromArgb(70, 120, 180);
                Yazi = Color.FromArgb(226, 232, 240);
                Soluk = Color.FromArgb(146, 160, 176);
                PasifYazi = Color.FromArgb(95, 106, 120);
                AracSimge = Color.FromArgb(110, 180, 255);
                KirmiziYazi = Color.FromArgb(255, 122, 122);
                TuruncuYazi = Color.FromArgb(255, 190, 90);
                YesilYazi = Color.FromArgb(98, 206, 148);
            }
            else
            {
                Zemin = Color.FromArgb(243, 246, 250);
                Yuzey = Color.White;
                YuzeyUzerinde = Color.FromArgb(234, 243, 253);
                YuzeyBasili = Color.FromArgb(220, 235, 251);
                Kenarlik = Color.FromArgb(214, 222, 232);
                KenarlikVurgu = Color.FromArgb(143, 192, 242);
                Yazi = Color.FromArgb(31, 45, 61);
                Soluk = Color.FromArgb(95, 111, 128);
                PasifYazi = Color.FromArgb(163, 173, 184);
                AracSimge = Color.FromArgb(47, 127, 214);
                KirmiziYazi = Color.FromArgb(198, 47, 53);
                TuruncuYazi = Color.FromArgb(176, 111, 0);
                YesilYazi = Color.FromArgb(43, 138, 87);
            }
        }

        static bool SistemKoyuMu()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return k != null && Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 20H1 ve sonrasi)
        public static void BaslikCubugu(Form form)
        {
            int deger = Koyu ? 1 : 0;
            try { DwmSetWindowAttribute(form.Handle, 20, ref deger, 4); } catch (Exception) { }
        }

        public static void ListeTemasi(ListView liste)
        {
            liste.BackColor = Yuzey;
            liste.ForeColor = Yazi;
            if (!liste.IsHandleCreated) return;
            SetWindowTheme(liste.Handle, Koyu ? "DarkMode_Explorer" : "Explorer", null);
            // 0x101F = LVM_GETHEADER
            IntPtr baslik = SendMessage(liste.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
            if (baslik != IntPtr.Zero) SetWindowTheme(baslik, Koyu ? "DarkMode_ItemsView" : null, null);
        }

        public static void OnayKutusu(CheckBox kutu)
        {
            kutu.ForeColor = Soluk;
            kutu.FlatStyle = Koyu ? FlatStyle.Flat : FlatStyle.Standard;
            kutu.FlatAppearance.BorderColor = Kenarlik;
            kutu.FlatAppearance.CheckedBackColor = Yuzey;
        }

        public static void Secim(ComboBox kutu)
        {
            kutu.FlatStyle = Koyu ? FlatStyle.Flat : FlatStyle.Standard;
            kutu.BackColor = Yuzey;
            kutu.ForeColor = Yazi;
        }

        public static GraphicsPath Yuvarlak(Rectangle r, int yaricap)
        {
            int d = Math.Max(2, yaricap * 2);
            var yol = new GraphicsPath();
            yol.AddArc(r.X, r.Y, d, d, 180, 90);
            yol.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            yol.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            yol.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            yol.CloseFigure();
            return yol;
        }
    }

    // Simgedeki gibi koyu lacivert gecisli ust bant
    class GradyanPanel : Panel
    {
        public GradyanPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientRectangle.Width == 0 || ClientRectangle.Height == 0) return;
            using (var firca = new LinearGradientBrush(ClientRectangle, Tema.Lacivert1, Tema.Lacivert2, LinearGradientMode.ForwardDiagonal))
                e.Graphics.FillRectangle(firca, ClientRectangle);
        }
    }

    enum DugmeTuru { Birincil, Seffaf, Arac, Sekme }

    // Yuvarlak koseli, simgeli duz buton; metni bos ise yalnizca simge gosterir
    class Dugme : Control
    {
        readonly DugmeTuru tur;
        string simge;
        Font simgeFontu;
        bool uzerinde, basili, secili;

        public Dugme(string metin, string simge, DugmeTuru tur, Font font)
        {
            this.tur = tur;
            this.simge = simge;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AccessibleRole = tur == DugmeTuru.Sekme ? AccessibleRole.PageTab : AccessibleRole.PushButton;
            AutoSize = true;
            Font = font;
            Text = metin;
            AccessibleName = metin;
        }

        public string Simge
        {
            get { return simge; }
            set { simge = value; BoyutuAyarla(); }
        }

        // Sekmelerde secili olani belirtir
        public bool Secili
        {
            get { return secili; }
            set { if (secili != value) { secili = value; Invalidate(); } }
        }

        Font SimgeFontu
        {
            get
            {
                if (simgeFontu == null) simgeFontu = new Font(Tema.SimgeFontu, Font.SizeInPoints, GraphicsUnit.Point);
                return simgeFontu;
            }
        }

        int Bosluk { get { return Text == "" ? 0 : (int)(Font.Height * 0.45); } }

        protected override void OnFontChanged(EventArgs e)
        {
            if (simgeFontu != null) simgeFontu.Dispose();
            simgeFontu = null;
            base.OnFontChanged(e);
            BoyutuAyarla();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            BoyutuAyarla();
        }

        void BoyutuAyarla()
        {
            Size = GetPreferredSize(Size.Empty);
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size yazi = Text == "" ? Size.Empty : TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            Size ikon = TextRenderer.MeasureText(simge, SimgeFontu, Size.Empty, TextFormatFlags.NoPadding);
            bool kucuk = tur == DugmeTuru.Arac || tur == DugmeTuru.Sekme;
            int dikey = (int)(Font.Height * (kucuk ? 0.45 : 0.55));
            int yukseklik = Math.Max(yazi.Height, ikon.Height) + dikey * 2;
            if (Text == "") return new Size(Math.Max(yukseklik, ikon.Width + dikey * 2), yukseklik);
            int yatay = kucuk ? (int)(Font.Height * 0.75) : Font.Height;
            return new Size(yatay * 2 + ikon.Width + Bosluk + yazi.Width, yukseklik);
        }

        protected override void OnMouseEnter(EventArgs e) { uzerinde = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { uzerinde = basili = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { basili = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            basili = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        // Tiklama bir mesaj kutusu acarsa basili gorunumde kalmasin
        protected override void OnClick(EventArgs e)
        {
            basili = false;
            Invalidate();
            base.OnClick(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled) uzerinde = basili = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
            base.OnKeyUp(e);
        }

        void Renkler(out Color zemin, out Color kenar, out Color yazi, out Color ikon)
        {
            Color soluk = Color.FromArgb(140, 160, 184);
            switch (tur)
            {
                case DugmeTuru.Birincil:
                    zemin = !Enabled ? Color.FromArgb(45, 255, 255, 255)
                        : basili ? Color.FromArgb(28, 118, 206) : uzerinde ? Color.FromArgb(76, 166, 250) : Tema.Vurgu;
                    kenar = Color.Empty;
                    yazi = ikon = Enabled ? Color.White : soluk;
                    break;
                case DugmeTuru.Seffaf:
                    zemin = Color.FromArgb(!Enabled ? 10 : basili ? 60 : uzerinde ? 40 : 22, 255, 255, 255);
                    kenar = Color.FromArgb(Enabled ? 110 : 40, 255, 255, 255);
                    yazi = ikon = Enabled ? Color.White : soluk;
                    break;
                case DugmeTuru.Sekme:
                    zemin = Enabled && !secili && (uzerinde || basili) ? Tema.YuzeyUzerinde : Color.Empty;
                    kenar = Color.Empty;
                    yazi = !Enabled ? Tema.PasifYazi : secili ? Tema.Yazi : Tema.Soluk;
                    ikon = !Enabled ? Tema.PasifYazi : secili ? Tema.Vurgu : Tema.Soluk;
                    break;
                default:
                    zemin = !Enabled ? Tema.Zemin : basili ? Tema.YuzeyBasili : uzerinde ? Tema.YuzeyUzerinde : Tema.Yuzey;
                    kenar = Enabled && uzerinde ? Tema.KenarlikVurgu : Tema.Kenarlik;
                    yazi = Enabled ? Tema.Yazi : Tema.PasifYazi;
                    ikon = Enabled ? Tema.AracSimge : Tema.PasifYazi;
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color zemin, kenar, yazi, ikon;
            Renkler(out zemin, out kenar, out yazi, out ikon);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath yol = Tema.Yuvarlak(r, Math.Max(3, Height / 6)))
            {
                if (zemin.A > 0) using (var firca = new SolidBrush(zemin)) g.FillPath(firca, yol);
                if (kenar.A > 0) using (var kalem = new Pen(kenar)) g.DrawPath(kalem, yol);
            }
            if (tur == DugmeTuru.Sekme && secili)
            {
                int kalinlik = Math.Max(2, Font.Height / 7);
                using (var firca = new SolidBrush(Tema.Vurgu))
                using (GraphicsPath cizgi = Tema.Yuvarlak(new Rectangle(Height / 3, Height - kalinlik, Width - Height * 2 / 3, kalinlik - 1), 1))
                    g.FillPath(firca, cizgi);
            }

            const TextFormatFlags bicim = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine;
            Size ikonBoyutu = TextRenderer.MeasureText(g, simge, SimgeFontu, Size.Empty, bicim);
            Size yaziBoyutu = Text == "" ? Size.Empty : TextRenderer.MeasureText(g, Text, Font, Size.Empty, bicim);
            int x = (Width - (ikonBoyutu.Width + Bosluk + yaziBoyutu.Width)) / 2;
            TextRenderer.DrawText(g, simge, SimgeFontu, new Rectangle(x, 0, ikonBoyutu.Width + 1, Height), ikon, bicim);
            if (Text != "")
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x + ikonBoyutu.Width + Bosluk, 0, yaziBoyutu.Width + 2, Height), yazi, bicim);

            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -3, -3));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && simgeFontu != null) simgeFontu.Dispose();
            base.Dispose(disposing);
        }
    }

    // Ozet karti: renkli simge dairesi, buyuk sayi ve aciklama
    class Kart : Control
    {
        readonly string baslik, simge;
        readonly Color renk;
        string deger = "-";
        Font sayiFontu, simgeFontu;

        public Kart(string baslik, string simge, Color renk)
        {
            this.baslik = baslik;
            this.simge = simge;
            this.renk = renk;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public string Deger
        {
            set { deger = value; Invalidate(); }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            FontlariBirak();
            base.OnFontChanged(e);
        }

        void FontlariBirak()
        {
            if (sayiFontu != null) sayiFontu.Dispose();
            if (simgeFontu != null) simgeFontu.Dispose();
            sayiFontu = simgeFontu = null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (sayiFontu == null) sayiFontu = new Font("Segoe UI Semibold", Font.SizeInPoints * 1.9f, GraphicsUnit.Point);
            if (simgeFontu == null) simgeFontu = new Font(Tema.SimgeFontu, Font.SizeInPoints * 1.35f, GraphicsUnit.Point);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath yol = Tema.Yuvarlak(r, Math.Max(4, Height / 9)))
            using (var firca = new SolidBrush(Tema.Yuzey))
            using (var kalem = new Pen(Tema.Kenarlik))
            {
                g.FillPath(firca, yol);
                g.DrawPath(kalem, yol);
            }

            int cap = (int)(Height * 0.52), bosluk = (int)(Height * 0.24);
            var daire = new Rectangle(bosluk, (Height - cap) / 2, cap, cap);
            using (var firca = new SolidBrush(Color.FromArgb(Tema.Koyu ? 48 : 32, renk))) g.FillEllipse(firca, daire);
            TextRenderer.DrawText(g, simge, simgeFontu, daire, renk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            const TextFormatFlags bicim = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            Size sayi = TextRenderer.MeasureText(g, deger, sayiFontu, Size.Empty, bicim);
            Size alt = TextRenderer.MeasureText(g, baslik, Font, Size.Empty, bicim);
            int x = daire.Right + bosluk * 2 / 3;
            int y = (Height - sayi.Height - alt.Height) / 2;
            TextRenderer.DrawText(g, deger, sayiFontu, new Point(x, y), Tema.Yazi, bicim);
            TextRenderer.DrawText(g, baslik, Font, new Point(x + 1, y + sayi.Height), Tema.Soluk, bicim);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) FontlariBirak();
            base.Dispose(disposing);
        }
    }

    // Secili aygitin ayrintilari: baslik ve alt alta etiket/deger ciftleri
    class AyrintiPaneli : Control
    {
        string baslik = "", bosMesaj = "";
        List<KeyValuePair<string, string>> satirlar = new List<KeyValuePair<string, string>>();
        Font baslikFontu, etiketFontu;

        public AyrintiPaneli()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            var menu = new ContextMenuStrip { Renderer = new MenuCizici() };
            menu.Items.Add(Dil.M("Ayrıntıları kopyala", "Copy details"), null, (s, e) => { if (Metin != "") Clipboard.SetText(Metin); });
            ContextMenuStrip = menu;
        }

        public string Metin
        {
            get
            {
                if (satirlar.Count == 0) return "";
                return baslik + Environment.NewLine + string.Join(Environment.NewLine, satirlar.Select(s => s.Key + ": " + s.Value.Replace("\n", ", ")));
            }
        }

        public void Goster(string baslik, List<KeyValuePair<string, string>> satirlar)
        {
            this.baslik = baslik;
            this.satirlar = satirlar;
            bosMesaj = "";
            Invalidate();
        }

        public void Bosalt(string mesaj)
        {
            baslik = "";
            satirlar = new List<KeyValuePair<string, string>>();
            bosMesaj = mesaj;
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            if (baslikFontu != null) baslikFontu.Dispose();
            if (etiketFontu != null) etiketFontu.Dispose();
            baslikFontu = etiketFontu = null;
            base.OnFontChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (baslikFontu == null) baslikFontu = new Font("Segoe UI Semibold", Font.SizeInPoints * 1.15f, GraphicsUnit.Point);
            if (etiketFontu == null) etiketFontu = new Font(Font.FontFamily, Font.SizeInPoints * 0.85f, GraphicsUnit.Point);
            Graphics g = e.Graphics;
            using (var firca = new SolidBrush(Tema.Yuzey)) g.FillRectangle(firca, ClientRectangle);
            using (var kalem = new Pen(Tema.Kenarlik)) g.DrawLine(kalem, 0, 0, 0, Height);

            int bosluk = Font.Height, genislik = Width - bosluk * 2;
            if (genislik <= 0) return;
            if (bosMesaj != "")
            {
                List<string> parcalar = Satirla(g, bosMesaj, Font, genislik);
                int yukseklik = parcalar.Count * Font.Height;
                Ciz(g, parcalar, Font, Tema.Soluk, bosluk, (Height - yukseklik) / 2, genislik, true);
                return;
            }
            int y = Ciz(g, Satirla(g, baslik, baslikFontu, genislik), baslikFontu, Tema.Yazi, bosluk, bosluk, genislik, false) + bosluk * 3 / 4;
            foreach (var s in satirlar)
            {
                if (y > Height) break;
                y = Ciz(g, Satirla(g, s.Key, etiketFontu, genislik), etiketFontu, Tema.Soluk, bosluk, y, genislik, false) + Font.Height / 8;
                y = Ciz(g, Satirla(g, s.Value, Font, genislik), Font, Tema.Yazi, bosluk, y, genislik, false) + Font.Height * 2 / 3;
            }
        }

        static int Ciz(Graphics g, List<string> parcalar, Font font, Color renk, int x, int y, int genislik, bool ortala)
        {
            TextFormatFlags bicim = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                (ortala ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
            foreach (string parca in parcalar)
            {
                TextRenderer.DrawText(g, parca, font, new Rectangle(x, y, genislik, font.Height), renk, bicim);
                y += font.Height;
            }
            return y;
        }

        // Kelimelere gore satirlara boler; satira sigmayan uzun kelimeler (aygit kimlikleri) harf harf bolunur
        static List<string> Satirla(Graphics g, string metin, Font font, int genislik)
        {
            const TextFormatFlags olcu = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            Func<string, bool> sigar = s => TextRenderer.MeasureText(g, s, font, Size.Empty, olcu).Width <= genislik;
            var sonuc = new List<string>();
            foreach (string paragraf in metin.Split('\n'))
            {
                string satir = "";
                foreach (string kelime in paragraf.Split(' '))
                {
                    string aday = satir == "" ? kelime : satir + " " + kelime;
                    if (sigar(aday)) { satir = aday; continue; }
                    if (satir != "") sonuc.Add(satir);
                    satir = kelime;
                    while (satir.Length > 1 && !sigar(satir))
                    {
                        int n = satir.Length - 1;
                        while (n > 1 && !sigar(satir.Substring(0, n))) n--;
                        sonuc.Add(satir.Substring(0, n));
                        satir = satir.Substring(n);
                    }
                }
                sonuc.Add(satir);
            }
            return sonuc;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (baslikFontu != null) baslikFontu.Dispose();
                if (etiketFontu != null) etiketFontu.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Pil sagligi gecmisi: zamana gore tam dolu kapasitenin ilk gunkune orani (%)
    class PilGrafigi : Control
    {
        List<PilKaydi> kayitlar = new List<PilKaydi>();
        Font baslikFontu, kucukFont;

        public PilGrafigi()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public List<PilKaydi> Kayitlar
        {
            get { return kayitlar; }
            set { kayitlar = value ?? new List<PilKaydi>(); Invalidate(); }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            FontlariBirak();
            base.OnFontChanged(e);
        }

        void FontlariBirak()
        {
            if (baslikFontu != null) baslikFontu.Dispose();
            if (kucukFont != null) kucukFont.Dispose();
            baslikFontu = kucukFont = null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (baslikFontu == null) baslikFontu = new Font("Segoe UI Semibold", Font.SizeInPoints, GraphicsUnit.Point);
            if (kucukFont == null) kucukFont = new Font(Font.FontFamily, Font.SizeInPoints * 0.85f, GraphicsUnit.Point);
            Graphics g = e.Graphics;
            using (var firca = new SolidBrush(Tema.Yuzey)) g.FillRectangle(firca, ClientRectangle);
            using (var kalem = new Pen(Tema.Kenarlik)) g.DrawLine(kalem, 0, 0, Width, 0);

            int bosluk = Font.Height;
            TextRenderer.DrawText(g, Dil.M("Pil sağlığı geçmişi", "Battery health history"), baslikFontu, new Point(bosluk, bosluk / 2), Tema.Yazi, TextFormatFlags.NoPadding);
            if (kayitlar.Count < 2) return;

            // Dikey eksen: en dusuk degerin biraz altindan %100'e
            double alt = Math.Max(0, Math.Floor((kayitlar.Min(k => k.Yuzde) - 5) / 10) * 10);
            string ornekEtiket = "%100";
            int solBosluk = bosluk + TextRenderer.MeasureText(ornekEtiket, kucukFont).Width + bosluk / 2;
            var alan = Rectangle.FromLTRB(solBosluk, bosluk * 2, Width - bosluk * 2, Height - bosluk * 2);
            if (alan.Width < 50 || alan.Height < 30) return;

            DateTime ilk = kayitlar[0].Tarih, son = kayitlar[kayitlar.Count - 1].Tarih;
            double gunler = Math.Max(1, (son - ilk).TotalDays);
            Func<PilKaydi, PointF> nokta = k => new PointF(
                alan.Left + (float)((k.Tarih - ilk).TotalDays / gunler * alan.Width),
                alan.Bottom - (float)((k.Yuzde - alt) / (100 - alt) * alan.Height));

            using (var cizgi = new Pen(Tema.Kenarlik) { DashStyle = DashStyle.Dot })
            {
                for (double d = alt; d <= 100; d += (100 - alt) / 2)
                {
                    float y = alan.Bottom - (float)((d - alt) / (100 - alt) * alan.Height);
                    g.DrawLine(cizgi, alan.Left, y, alan.Right, y);
                    TextRenderer.DrawText(g, "%" + d.ToString("0"), kucukFont, new Rectangle(bosluk, (int)y - kucukFont.Height / 2, solBosluk - bosluk - bosluk / 2, kucukFont.Height),
                        Tema.Soluk, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                }
            }
            foreach (DateTime t in new[] { ilk, ilk.AddDays(gunler / 2), son })
            {
                string metin = t.ToString("yyyy-MM");
                int x = alan.Left + (int)((t - ilk).TotalDays / gunler * alan.Width);
                int genislik = TextRenderer.MeasureText(metin, kucukFont).Width;
                TextRenderer.DrawText(g, metin, kucukFont, new Point(Math.Min(alan.Right - genislik, Math.Max(alan.Left, x - genislik / 2)), alan.Bottom + bosluk / 3),
                    Tema.Soluk, TextFormatFlags.NoPadding);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            PointF[] noktalar = kayitlar.Select(nokta).ToArray();
            using (var yol = new GraphicsPath())
            {
                yol.AddLines(noktalar);
                yol.AddLine(noktalar[noktalar.Length - 1], new PointF(noktalar[noktalar.Length - 1].X, alan.Bottom));
                yol.AddLine(new PointF(noktalar[0].X, alan.Bottom), noktalar[0]);
                using (var dolgu = new SolidBrush(Color.FromArgb(Tema.Koyu ? 50 : 35, Tema.Vurgu))) g.FillPath(dolgu, yol);
            }
            using (var kalem = new Pen(Tema.Vurgu, Math.Max(2, Font.Height / 8f))) g.DrawLines(kalem, noktalar);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) FontlariBirak();
            base.Dispose(disposing);
        }
    }

    // Durum cubugunu duz zeminle cizer
    class DuzCizici : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var firca = new SolidBrush(e.ToolStrip.BackColor)) e.Graphics.FillRectangle(firca, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    }

    // Sag tik ve tepsi menulerini temaya uygun cizer
    class MenuCizici : ToolStripProfessionalRenderer
    {
        public MenuCizici() : base(new MenuRenkleri())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Tema.Yazi : Tema.PasifYazi;
            base.OnRenderItemText(e);
        }
    }

    class MenuRenkleri : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Tema.Yuzey; } }
        public override Color ImageMarginGradientBegin { get { return Tema.Yuzey; } }
        public override Color ImageMarginGradientMiddle { get { return Tema.Yuzey; } }
        public override Color ImageMarginGradientEnd { get { return Tema.Yuzey; } }
        public override Color MenuBorder { get { return Tema.Kenarlik; } }
        public override Color MenuItemBorder { get { return Tema.YuzeyUzerinde; } }
        public override Color MenuItemSelected { get { return Tema.YuzeyUzerinde; } }
        public override Color MenuItemSelectedGradientBegin { get { return Tema.YuzeyUzerinde; } }
        public override Color MenuItemSelectedGradientEnd { get { return Tema.YuzeyUzerinde; } }
        public override Color SeparatorDark { get { return Tema.Kenarlik; } }
        public override Color SeparatorLight { get { return Tema.Yuzey; } }
    }
}
