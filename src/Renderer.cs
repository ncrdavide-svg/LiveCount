using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LiveCount
{
    // Disegna barra, timer e messaggio. Usato sia dal display sia dalle anteprime della regia,
    // cosi' l'anteprima mostra esattamente cio' che va in onda.
    public class Renderer
    {
        readonly TimerEngine _engine;
        Settings _s;
        Font _timerFont, _messageFont;

        public Color Back, Normal, Warn, Danger, MsgColor, BoxColor;
        public bool MessageVisible;
        public string MessageText = "";

        // Con il video attivo il timer sta in un riquadro sopra; altrimenti occupa tutto lo schermo.
        public bool Overlay;
        public VideoSource Video;

        public Renderer(TimerEngine engine, Settings s)
        {
            _engine = engine;
            ApplySettings(s);
        }

        public void ApplySettings(Settings s)
        {
            _s = s;
            if (_timerFont != null) _timerFont.Dispose();
            if (_messageFont != null) _messageFont.Dispose();
            _timerFont = MakeFont(s.TimerFont, s.TimerSize, s.TimerBold);
            _messageFont = MakeFont(s.MessageFont, s.MessageSize, s.MessageBold);
            Back = Settings.ParseColor(s.BackColor, Color.Black);
            Normal = Settings.ParseColor(s.NormalColor, Color.LimeGreen);
            Warn = Settings.ParseColor(s.WarnColor, Color.Orange);
            Danger = Settings.ParseColor(s.DangerColor, Color.Red);
            MsgColor = Settings.ParseColor(s.MessageColor, Color.White);
            BoxColor = Settings.ParseColor(s.BoxColor, Color.Black);
        }

        static Font MakeFont(string family, int px, bool bold)
        {
            try { return new Font(family, Math.Max(8, px), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { return new Font("Segoe UI", Math.Max(8, px), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        }

        public Color CurrentColor
        {
            get
            {
                switch (_engine.Phase(_s.WarnPercent, _s.DangerPercent))
                {
                    case TimerPhase.Warning: return Warn;
                    case TimerPhase.Danger:
                    case TimerPhase.Overtime: return Danger;
                    default: return Normal;
                }
            }
        }

        public RectangleF BoxRect(RectangleF screen)
        {
            return new RectangleF(
                screen.X + (float)(_s.BoxX * screen.Width), screen.Y + (float)(_s.BoxY * screen.Height),
                (float)(_s.BoxW * screen.Width), (float)(_s.BoxH * screen.Height));
        }

        // screen: rettangolo che rappresenta l'intero schermo di uscita.
        // pxScale: rapporto tra quel rettangolo e la risoluzione reale (1 sul display, <1 in anteprima).
        public void Draw(Graphics g, RectangleF screen, float pxScale)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            RectangleF box = Overlay ? BoxRect(screen) : screen;
            float s = pxScale;
            if (Overlay) s *= Math.Min(box.Width / screen.Width, box.Height / screen.Height);

            var state = g.Save();
            GraphicsPath path = null;
            if (Overlay)
            {
                path = RoundRect(box, Math.Min(box.Width, box.Height) * 0.06f);
                int alpha = (int)Math.Round(Math.Max(0, Math.Min(100, _s.BoxOpacity)) * 2.55);
                using (var b = new SolidBrush(Color.FromArgb(alpha, BoxColor))) g.FillPath(b, path);
                g.SetClip(path, CombineMode.Intersect);
            }

            Color col = CurrentColor;
            float top = box.Y;
            if (_s.ShowBar)
            {
                float bh = Math.Max(1f, _s.BarHeight * s);
                using (var b = new SolidBrush(Color.FromArgb(40, col))) g.FillRectangle(b, box.X, box.Y, box.Width, bh);
                double frac = _engine.RemainingMs <= 0 ? 1.0 : _engine.RemainingFraction;
                using (var b = new SolidBrush(col)) g.FillRectangle(b, box.X, box.Y, (float)(box.Width * frac), bh);
                top += bh;
            }

            var content = new RectangleF(box.X, top, box.Width, box.Bottom - top);
            if (Overlay) content.Inflate(-box.Height * 0.04f, -box.Height * 0.04f);
            DrawText(g, content, s, col);

            g.Restore(state);
            if (path != null) path.Dispose();
        }

        void DrawText(Graphics g, RectangleF area, float s, Color col)
        {
            if (area.Width <= 1 || area.Height <= 1 || s <= 0) return;
            string text = _engine.DisplayText;
            // Misure in pixel "reali" (scala 1), poi tutto viene ridotto con una trasformazione.
            float vW = area.Width / s, vH = area.Height / s;

            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            using (var msf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                msf.Alignment = StringAlignment.Center;

                SizeF tsz = g.MeasureString(text, _timerFont, PointF.Empty, sf);
                // Centratura sulle cifre, non sulla riga: molti font hanno tanto spazio vuoto sopra.
                var fam = _timerFont.FontFamily;
                var style = _timerFont.Style;
                float ascent = _timerFont.Size * fam.GetCellAscent(style) / fam.GetEmHeight(style);
                float capH = _timerFont.Size * 0.72f;
                float digitTop = Math.Max(0, ascent - capH);
                float descent = _timerFont.Size * fam.GetCellDescent(style) / fam.GetEmHeight(style);

                bool showMsg = MessageVisible && !string.IsNullOrEmpty(MessageText);
                float msgW = vW * 0.94f;
                SizeF msz = SizeF.Empty;
                float gap = 0;
                if (showMsg)
                {
                    msz = g.MeasureString(MessageText, _messageFont, (int)Math.Max(1, msgW), msf);
                    gap = _messageFont.GetHeight(g) * 0.3f;
                }

                // Blocco visivo: dalla cima delle cifre alla fine del messaggio (o alla linea di base).
                float msgTop = ascent + (showMsg ? descent * 0.5f + gap : 0);
                float blockH = (showMsg ? msgTop + msz.Height : ascent) - digitTop;
                // Se il contenuto non entra viene ridotto in scala invece di essere tagliato.
                float fit = 1f;
                if (tsz.Width > vW * 0.98f) fit = Math.Min(fit, vW * 0.98f / tsz.Width);
                if (blockH > vH) fit = Math.Min(fit, vH / blockH);
                float total = s * fit;

                var st = g.Save();
                g.TranslateTransform(area.X + area.Width / 2f, area.Y + (area.Height - blockH * total) / 2f);
                g.ScaleTransform(total, total);
                g.TranslateTransform(0, -digitTop);
                using (var b = new SolidBrush(col))
                    g.DrawString(text, _timerFont, b, 0, 0, sf);
                if (showMsg)
                {
                    using (var b = new SolidBrush(MsgColor))
                        g.DrawString(MessageText, _messageFont, b, new RectangleF(-msgW / 2f, msgTop, msgW, msz.Height + 2), msf);
                }
                g.Restore(st);
            }
        }

        static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Max(0.5f, radius * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
