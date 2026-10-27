using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveCount
{
    // ------------------------------------------------------------------
    // Finestra di output (schermo secondario)
    // ------------------------------------------------------------------
    public class DisplayForm : Form
    {
        readonly Renderer _r;
        public bool AllowClose;

        public DisplayForm(Renderer r)
        {
            _r = r;
            Text = "LiveCount - Display";
            Icon = Program.AppIcon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
        }

        public void PlaceOn(Screen screen, bool fullscreen)
        {
            WindowState = FormWindowState.Normal;
            if (fullscreen)
            {
                FormBorderStyle = FormBorderStyle.None;
                Bounds = screen.Bounds;
                TopMost = !screen.Primary;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                TopMost = false;
                var wa = screen.WorkingArea;
                int w = Math.Min(1280, wa.Width * 2 / 3), h = w * 9 / 16;
                Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Il display si chiude solo dalla regia (o alla chiusura dell'app).
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_r.Back);
            if (_r.Overlay && _r.Video != null)
            {
                IntPtr hdc = g.GetHdc();
                try { _r.Video.Draw(hdc, ClientRectangle); }
                finally { g.ReleaseHdc(hdc); }
            }
            _r.Draw(g, ClientRectangle, 1f);
        }
    }

    // ------------------------------------------------------------------
    // Anteprima dal vivo dello schermo secondario: il riquadro del timer
    // si sposta trascinandolo, si ridimensiona dall'angolo o con la rotellina.
    // ------------------------------------------------------------------
    public class PreviewControl : Control
    {
        enum DragMode { None, Move, Resize }

        public Renderer R;
        public Settings S;
        public Func<Size> OutputSize;
        public event EventHandler BoxChanged;
        public event EventHandler BoxCommitted;

        DragMode _drag;
        Point _dragStart;
        RectangleF _boxStart;
        const double MinBox = 0.06;

        public PreviewControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(28, 28, 28);
        }

        Size Output
        {
            get
            {
                Size s = OutputSize != null ? OutputSize() : new Size(1920, 1080);
                return s.Width > 0 && s.Height > 0 ? s : new Size(1920, 1080);
            }
        }

        RectangleF ScreenRect()
        {
            Size o = Output;
            var area = ClientRectangle;
            area.Inflate(-4, -4);
            float ar = (float)o.Width / o.Height;
            float w = area.Width, h = w / ar;
            if (h > area.Height) { h = area.Height; w = h * ar; }
            return new RectangleF((float)Math.Round(area.X + (area.Width - w) / 2), (float)Math.Round(area.Y + (area.Height - h) / 2), (float)Math.Round(w), (float)Math.Round(h));
        }

        RectangleF HandleRect(RectangleF box)
        {
            return new RectangleF(box.Right - 12, box.Bottom - 12, 16, 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (R == null) return;
            RectangleF sr = ScreenRect();
            using (var b = new SolidBrush(R.Back)) g.FillRectangle(b, sr);

            bool signal = false;
            if (R.Overlay && R.Video != null)
            {
                IntPtr hdc = g.GetHdc();
                try { signal = R.Video.Draw(hdc, Rectangle.Round(sr)); }
                finally { g.ReleaseHdc(hdc); }
            }

            R.Draw(g, sr, sr.Width / Output.Width);

            using (var p = new Pen(Color.FromArgb(90, 90, 90))) g.DrawRectangle(p, sr.X, sr.Y, sr.Width - 1, sr.Height - 1);

            g.SmoothingMode = SmoothingMode.None;
            if (R.Overlay)
            {
                var box = R.BoxRect(sr);
                using (var p = new Pen(Color.Gold, 1) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(p, box.X, box.Y, box.Width - 1, box.Height - 1);
                var hr = HandleRect(box);
                using (var b = new SolidBrush(Color.Gold)) g.FillRectangle(b, hr.X + 3, hr.Y + 3, 9, 9);
                if (!signal)
                    using (var b = new SolidBrush(Color.FromArgb(200, 200, 200)))
                    using (var f = new Font("Segoe UI", 9F))
                        g.DrawString(R.Video == null ? "Nessuna sorgente video" : R.Video.Kind + ": nessun segnale", f, b, sr.X + 6, sr.Y + 6);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (R == null || !R.Overlay || e.Button != MouseButtons.Left) return;
            var box = R.BoxRect(ScreenRect());
            if (HandleRect(box).Contains(e.Location)) _drag = DragMode.Resize;
            else if (box.Contains(e.Location)) _drag = DragMode.Move;
            else return;
            _dragStart = e.Location;
            _boxStart = new RectangleF((float)S.BoxX, (float)S.BoxY, (float)S.BoxW, (float)S.BoxH);
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (R == null) return;
            RectangleF sr = ScreenRect();
            if (_drag == DragMode.None)
            {
                Cursor c = Cursors.Default;
                if (R.Overlay)
                {
                    var box = R.BoxRect(sr);
                    if (HandleRect(box).Contains(e.Location)) c = Cursors.SizeNWSE;
                    else if (box.Contains(e.Location)) c = Cursors.SizeAll;
                }
                if (Cursor != c) Cursor = c;
                return;
            }
            double dx = (e.X - _dragStart.X) / sr.Width, dy = (e.Y - _dragStart.Y) / sr.Height;
            if (_drag == DragMode.Move)
            {
                S.BoxX = Clamp(_boxStart.X + dx, 0, 1 - S.BoxW);
                S.BoxY = Clamp(_boxStart.Y + dy, 0, 1 - S.BoxH);
            }
            else
            {
                S.BoxW = Clamp(_boxStart.Width + dx, MinBox, 1 - S.BoxX);
                S.BoxH = Clamp(_boxStart.Height + dy, MinBox, 1 - S.BoxY);
            }
            Changed();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_drag == DragMode.None) return;
            _drag = DragMode.None;
            Capture = false;
            if (BoxCommitted != null) BoxCommitted(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (R == null || !R.Overlay) return;
            // Rotellina: ingrandisce/riduce il riquadro attorno al suo centro.
            double k = Math.Pow(1.06, e.Delta / 120.0);
            double cx = S.BoxX + S.BoxW / 2, cy = S.BoxY + S.BoxH / 2;
            double w = S.BoxW * k, h = S.BoxH * k;
            double f = Math.Min(1.0, Math.Min(1 / w, 1 / h));
            w *= f; h *= f;
            if (w < MinBox || h < MinBox) return;
            S.BoxW = w; S.BoxH = h;
            S.BoxX = Clamp(cx - w / 2, 0, 1 - w);
            S.BoxY = Clamp(cy - h / 2, 0, 1 - h);
            Changed();
            if (BoxCommitted != null) BoxCommitted(this, EventArgs.Empty);
        }

        void Changed()
        {
            Invalidate();
            if (BoxChanged != null) BoxChanged(this, EventArgs.Empty);
        }

        static double Clamp(double v, double min, double max) { return Math.Max(min, Math.Min(max, v)); }
    }
}
