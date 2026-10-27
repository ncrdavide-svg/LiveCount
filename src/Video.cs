using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace LiveCount
{
    static class Gdi
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        public const int COLORONCOLOR = 3, HALFTONE = 4;

        [DllImport("gdi32.dll")]
        public static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int wDest, int hDest,
            int xSrc, int ySrc, int wSrc, int hSrc, byte[] bits, ref BITMAPINFOHEADER bmi, uint usage, uint rop);

        [DllImport("gdi32.dll")]
        public static extern int SetStretchBltMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr prev);
    }

    // Sorgente video generica (NDI o scheda USB): riceve su un thread dedicato
    // e tiene sempre pronto l'ultimo fotogramma in formato BGRX.
    public abstract class VideoSource : IDisposable
    {
        readonly object _lock = new object();
        Thread _thread;
        volatile bool _stop;
        byte[] _front, _back;
        int _width, _height;
        float _aspect;
        DateTime _lastFrameUtc = DateTime.MinValue;

        public string SourceName { get; private set; }
        public double Fps { get; private set; }
        public bool IsConnected { get { return _thread != null; } }
        public string LastError { get; protected set; }
        public abstract string Kind { get; }

        // Sollevato sul thread di ricezione a ogni nuovo fotogramma.
        public event Action FrameArrived;

        protected bool Stopping { get { return _stop; } }

        public bool HasSignal
        {
            get { lock (_lock) return _front != null && (DateTime.UtcNow - _lastFrameUtc).TotalSeconds < 2; }
        }

        public Size FrameSize { get { lock (_lock) return new Size(_width, _height); } }

        public void Connect(string name)
        {
            Disconnect();
            SourceName = name;
            LastError = null;
            if (string.IsNullOrEmpty(name)) return;
            _stop = false;
            _thread = new Thread(delegate() { Run(name); }) { IsBackground = true, Name = Kind + " receive" };
            // Media Foundation lavora meglio in un apartment MTA.
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        public void Disconnect()
        {
            _stop = true;
            if (_thread != null)
            {
                if (!_thread.Join(700)) Abort();
                _thread.Join(2000);
                _thread = null;
            }
            lock (_lock) { _front = null; _width = _height = 0; }
            Fps = 0;
        }

        // Interrompe una lettura bloccata (es. una scheda senza segnale).
        protected virtual void Abort() { }

        protected abstract void Run(string name);

        // Copia un fotogramma BGRX: 'stride' puo' essere negativo (immagine dal basso verso l'alto).
        protected void PublishFrame(IntPtr scan0, int w, int h, int stride, float aspect, double fps)
        {
            if (w <= 0 || h <= 0 || scan0 == IntPtr.Zero) return;
            int rowBytes = w * 4, size = rowBytes * h;
            if (_back == null || _back.Length != size) _back = new byte[size];
            if (stride == rowBytes) Marshal.Copy(scan0, _back, 0, size);
            else
                for (int y = 0; y < h; y++)
                    Marshal.Copy(new IntPtr(scan0.ToInt64() + (long)y * stride), _back, y * rowBytes, rowBytes);

            lock (_lock)
            {
                var t = _front; _front = _back; _back = t;
                _width = w; _height = h;
                _aspect = aspect > 0 ? aspect : (float)w / h;
                _lastFrameUtc = DateTime.UtcNow;
            }
            if (fps > 0) Fps = fps;
            var handler = FrameArrived;
            if (handler != null) handler();
        }

        // Disegna l'ultimo fotogramma adattato (letterbox) dentro 'area' sull'HDC indicato.
        public bool Draw(IntPtr hdc, Rectangle area)
        {
            lock (_lock)
            {
                if (_front == null || _width <= 0 || area.Width <= 0 || area.Height <= 0) return false;
                float ar = _aspect > 0 ? _aspect : (float)_width / _height;
                int w = area.Width, h = (int)Math.Round(w / ar);
                if (h > area.Height) { h = area.Height; w = (int)Math.Round(h * ar); }
                int x = area.X + (area.Width - w) / 2, y = area.Y + (area.Height - h) / 2;

                var bmi = new Gdi.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf(typeof(Gdi.BITMAPINFOHEADER)),
                    biWidth = _width,
                    biHeight = -_height, // top-down
                    biPlanes = 1,
                    biBitCount = 32
                };
                bool same = w == _width && h == _height;
                Gdi.SetStretchBltMode(hdc, same ? Gdi.COLORONCOLOR : Gdi.HALFTONE);
                if (!same) Gdi.SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero);
                Gdi.StretchDIBits(hdc, x, y, w, h, 0, 0, _width, _height, _front, ref bmi, 0, 0x00CC0020);
                return true;
            }
        }

        public virtual void Dispose()
        {
            Disconnect();
        }
    }
}
