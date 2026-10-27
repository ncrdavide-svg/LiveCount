// LiveCount - regia + display a schermo pieno su monitor secondario.
// Compilato con il csc di .NET Framework 4 (C# 5): nessuna installazione richiesta.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyTitle("LiveCount")]
[assembly: System.Reflection.AssemblyProduct("LiveCount")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]

namespace LiveCount
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ControlForm());
        }

        static Icon _icon;

        // Logo dell'app (incorporato nell'exe), usato da tutte le finestre.
        public static Icon AppIcon
        {
            get
            {
                if (_icon == null)
                {
                    using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LiveCount.ico"))
                        _icon = s != null ? new Icon(s) : SystemIcons.Application;
                }
                return _icon;
            }
        }
    }

    // ------------------------------------------------------------------
    // Impostazioni persistenti
    // ------------------------------------------------------------------
    public class Preset
    {
        public string Name;
        public int Seconds;
        public override string ToString()
        {
            return Name + "   [" + TimerEngine.FormatHms(Seconds) + "]";
        }
    }

    public class Settings
    {
        public int Hours = 0, Minutes = 10, Seconds = 0;
        public List<Preset> Presets = new List<Preset>();

        public string DisplayDevice = "";
        public bool Fullscreen = true;

        public string TimerFont = "Segoe UI";
        public bool TimerBold = true;
        public int TimerSize = 300;
        public string BackColor = "#000000";
        public string NormalColor = "#22C55E";
        public string WarnColor = "#F59E0B";
        public string DangerColor = "#EF4444";
        public int WarnPercent = 10;
        public int DangerPercent = 5;
        public bool ShowBar = true;
        public int BarHeight = 40;

        public string MessageText = "";
        public string MessageFont = "Segoe UI";
        public bool MessageBold = true;
        public int MessageSize = 90;
        public string MessageColor = "#FFFFFF";

        // NDI: sorgente video di sfondo e riquadro del timer (coordinate 0..1 rispetto allo schermo).
        public bool NdiEnabled = false;
        public string NdiSource = "";
        public double BoxX = 0.60, BoxY = 0.62, BoxW = 0.37, BoxH = 0.34;
        public string BoxColor = "#000000";
        public int BoxOpacity = 65;

        static string _path;

        static string FilePath
        {
            get
            {
                if (_path != null) return _path;
                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LiveCount.settings.xml");
                try
                {
                    // Portabile: le impostazioni stanno accanto all'exe se la cartella e' scrivibile.
                    string probe = local + ".tmp";
                    File.WriteAllText(probe, "x");
                    File.Delete(probe);
                    _path = local;
                }
                catch
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveCount");
                    Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, "LiveCount.settings.xml");
                }
                return _path;
            }
        }

        public static Settings Load()
        {
            try
            {
                // Impostazioni della versione precedente (quando si chiamava TimerCountdown).
                string legacy = Path.Combine(Path.GetDirectoryName(FilePath), "TimerCountdown.settings.xml");
                string source = File.Exists(FilePath) ? FilePath : legacy;
                if (File.Exists(source))
                {
                    using (var fs = File.OpenRead(source))
                        return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(fs);
                }
            }
            catch { }
            var s = new Settings();
            s.Presets.Add(new Preset { Name = "Intervento 5 min", Seconds = 300 });
            s.Presets.Add(new Preset { Name = "Intervento 10 min", Seconds = 600 });
            s.Presets.Add(new Preset { Name = "Pausa caffe' 15 min", Seconds = 900 });
            return s;
        }

        public void Save()
        {
            try
            {
                string tmp = FilePath + ".tmp";
                using (var fs = File.Create(tmp))
                    new XmlSerializer(typeof(Settings)).Serialize(fs, this);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch { }
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        public static string ToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
    }

    // ------------------------------------------------------------------
    // Motore del timer (basato su Stopwatch: niente deriva)
    // ------------------------------------------------------------------
    public enum TimerPhase { Normal, Warning, Danger, Overtime }

    public class TimerEngine
    {
        readonly Stopwatch _sw = new Stopwatch();
        long _accumulatedMs;

        public long TotalMs { get; private set; }
        public bool Running { get { return _sw.IsRunning; } }

        public long ElapsedMs { get { return _accumulatedMs + _sw.ElapsedMilliseconds; } }
        public long RemainingMs { get { return TotalMs - ElapsedMs; } }

        public void SetTotal(long ms)
        {
            _sw.Reset();
            _accumulatedMs = 0;
            TotalMs = Math.Max(0, ms);
        }

        public void Start() { if (!_sw.IsRunning) _sw.Start(); }

        public void Pause()
        {
            if (!_sw.IsRunning) return;
            _sw.Stop();
            _accumulatedMs += _sw.ElapsedMilliseconds;
            _sw.Reset();
        }

        public void Reset() { SetTotal(TotalMs); }

        public void AddTime(long ms) { TotalMs = Math.Max(0, TotalMs + ms); }

        public double RemainingFraction
        {
            get
            {
                if (TotalMs <= 0) return 0;
                return Math.Max(0, Math.Min(1, (double)RemainingMs / TotalMs));
            }
        }

        public TimerPhase Phase(int warnPercent, int dangerPercent)
        {
            long rem = RemainingMs;
            if (rem <= 0) return TimerPhase.Overtime;
            double pct = TotalMs > 0 ? rem * 100.0 / TotalMs : 0;
            if (pct <= dangerPercent) return TimerPhase.Danger;
            if (pct <= warnPercent) return TimerPhase.Warning;
            return TimerPhase.Normal;
        }

        public string DisplayText
        {
            get
            {
                long rem = RemainingMs;
                if (rem > 0)
                {
                    // Arrotonda per eccesso: alla partenza mostra il totale e arriva a 00:00 esattamente a zero.
                    long secs = (rem + 999) / 1000;
                    return Format(secs, TotalMs >= 3600000);
                }
                long over = (-rem) / 1000;
                return (over > 0 ? "+" : "") + Format(over, TotalMs >= 3600000 || over >= 3600);
            }
        }

        static string Format(long secs, bool forceHours)
        {
            long h = secs / 3600, m = (secs % 3600) / 60, s = secs % 60;
            if (h > 0 || forceHours)
                return h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
            return m.ToString("00") + ":" + s.ToString("00");
        }

        public static string FormatHms(int secs)
        {
            return (secs / 3600).ToString("00") + ":" + ((secs % 3600) / 60).ToString("00") + ":" + (secs % 60).ToString("00");
        }
    }
}
