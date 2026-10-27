using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LiveCount
{
    // ------------------------------------------------------------------
    // Finestra di regia (schermo principale)
    // ------------------------------------------------------------------
    public class ControlForm : Form
    {
        readonly Settings _s;
        readonly TimerEngine _engine = new TimerEngine();
        readonly Renderer _renderer;
        readonly DisplayForm _display;
        readonly NdiReceiver _ndi;
        readonly UsbCapture _usb = new UsbCapture();
        List<string> _usbDevices = new List<string>();
        const string NdiPrefix = "NDI: ", UsbPrefix = "USB: ";
        readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer();
        bool _loading;
        int _tickCount;
        int _framePending;

        NumericUpDown _h, _m, _sec;
        Label _preview, _state;
        Button _btnStart, _btnReset, _btnDisplayShow, _btnDisplayHide;
        ListBox _presetList;
        TextBox _presetName, _msgBox;
        Button _btnMsgShow;
        ComboBox _monitor, _timerFontBox, _msgFontBox, _ndiCombo;
        CheckBox _fullscreen, _timerBold, _msgBold, _showBar, _ndiEnable;
        NumericUpDown _timerSize, _msgSize, _warnPct, _dangerPct, _barHeight, _boxOpacity;
        Button _cBack, _cNormal, _cWarn, _cDanger, _cMsg, _cBox;
        Label _msgOnAir, _ndiStatus;
        PreviewControl _monitorPreview;
        Form _bigPreviewWindow;
        PreviewControl _bigPreview;

        public ControlForm()
        {
            _loading = true;
            _s = Settings.Load();
            _renderer = new Renderer(_engine, _s);
            _display = new DisplayForm(_renderer);

            if (NdiLoader.TryLoad())
            {
                try { _ndi = new NdiReceiver(); }
                catch (Exception ex) { _ndi = null; NdiLoader.Error = "Errore NDI: " + ex.Message; }
            }
            if (_ndi != null) _ndi.FrameArrived += OnVideoFrame;
            _usb.FrameArrived += OnVideoFrame;
            // Compatibilita': le versioni precedenti salvavano solo il nome NDI senza prefisso.
            if (!string.IsNullOrEmpty(_s.NdiSource) && !_s.NdiSource.StartsWith(NdiPrefix) && !_s.NdiSource.StartsWith(UsbPrefix))
                _s.NdiSource = NdiPrefix + _s.NdiSource;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "LiveCount - Regia";
            Icon = Program.AppIcon;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(1412, 700);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            AutoScroll = true;
            BuildUi();
            ResumeLayout(false);
            PerformLayout();

            LoadIntoUi();
            _loading = false;

            _engine.SetTotal(UiTotalSeconds() * 1000L);
            _msgBox.Text = _s.MessageText ?? "";
            ApplyNdiState();

            _tick.Interval = 33;
            _tick.Tick += delegate { Refresh_(); };
            _tick.Start();

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        }

        // ---------------- costruzione interfaccia ----------------
        void BuildUi()
        {
            // --- Colonna sinistra ---
            var gTimer = Group("Timer", 12, 8, 450, 250);
            gTimer.Controls.Add(Lbl("Ore", 16, 26));
            gTimer.Controls.Add(Lbl("Minuti", 96, 26));
            gTimer.Controls.Add(Lbl("Secondi", 176, 26));
            _h = Num(16, 46, 0, 99); _m = Num(96, 46, 0, 59); _sec = Num(176, 46, 0, 59);
            foreach (var n in new[] { _h, _m, _sec })
            {
                n.Width = 70; n.Font = new Font("Segoe UI", 14F);
                n.ValueChanged += delegate { OnHmsChanged(); };
                gTimer.Controls.Add(n);
            }
            gTimer.Controls.Add(Btn("Imposta", 260, 46, 90, 34, delegate { SetFromUi(); }));

            _preview = new Label
            {
                Location = new Point(16, 92), Size = new Size(418, 70),
                Font = new Font("Segoe UI", 34F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Black, ForeColor = Color.LimeGreen
            };
            gTimer.Controls.Add(_preview);
            _state = Lbl("", 16, 166); _state.AutoSize = false; _state.Size = new Size(418, 18); _state.TextAlign = ContentAlignment.MiddleCenter;
            gTimer.Controls.Add(_state);

            _btnStart = Btn("AVVIA  (F5)", 16, 190, 140, 44, delegate { ToggleStart(); });
            _btnStart.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _btnReset = Btn("RESET  (F8)", 162, 190, 110, 44, delegate { _engine.Reset(); });
            gTimer.Controls.Add(_btnStart);
            gTimer.Controls.Add(_btnReset);
            gTimer.Controls.Add(Btn("-1 min", 278, 190, 76, 44, delegate { _engine.AddTime(-60000); }));
            gTimer.Controls.Add(Btn("+1 min", 358, 190, 76, 44, delegate { _engine.AddTime(60000); }));

            var gPreset = Group("Preset", 12, 264, 450, 210);
            _presetList = new ListBox { Location = new Point(16, 24), Size = new Size(280, 140), IntegralHeight = false };
            _presetList.DoubleClick += delegate { LoadPreset(); };
            gPreset.Controls.Add(_presetList);
            gPreset.Controls.Add(Btn("Carica", 304, 24, 130, 30, delegate { LoadPreset(); }));
            gPreset.Controls.Add(Btn("Elimina", 304, 58, 130, 30, delegate { DeletePreset(); }));
            gPreset.Controls.Add(Btn("Su", 304, 92, 63, 30, delegate { MovePreset(-1); }));
            gPreset.Controls.Add(Btn("Giu'", 371, 92, 63, 30, delegate { MovePreset(1); }));
            gPreset.Controls.Add(Lbl("Nome:", 16, 176));
            _presetName = new TextBox { Location = new Point(60, 172), Size = new Size(236, 24) };
            gPreset.Controls.Add(_presetName);
            gPreset.Controls.Add(Btn("Salva ore/min/sec", 304, 170, 130, 30, delegate { SavePreset(); }));

            var gMsg = Group("Messaggio sotto il timer", 12, 480, 450, 210);
            _msgBox = new TextBox { Location = new Point(16, 24), Size = new Size(418, 100), Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 11F) };
            _msgBox.TextChanged += delegate
            {
                _renderer.MessageText = _msgBox.Text;
                _s.MessageText = _msgBox.Text;
                RepaintOutputs();
            };
            _msgBox.Leave += delegate { _s.Save(); };
            gMsg.Controls.Add(_msgBox);
            _btnMsgShow = Btn("MOSTRA  (F9)", 16, 132, 150, 40, delegate { ToggleMessage(); });
            _btnMsgShow.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            gMsg.Controls.Add(_btnMsgShow);
            gMsg.Controls.Add(Btn("Pulisci", 172, 132, 80, 40, delegate { _msgBox.Clear(); }));
            _msgOnAir = Lbl("", 262, 144); _msgOnAir.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gMsg.Controls.Add(_msgOnAir);
            gMsg.Controls.Add(Lbl("Il testo si aggiorna in diretta mentre e' visibile.", 16, 180));

            // --- Colonna centrale ---
            var gDisp = Group("Display", 476, 8, 450, 110);
            gDisp.Controls.Add(Lbl("Monitor:", 16, 30));
            _monitor = new ComboBox { Location = new Point(80, 26), Size = new Size(354, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            _monitor.SelectedIndexChanged += delegate { if (!_loading) { _s.DisplayDevice = SelectedScreen().DeviceName; PlaceDisplay(); _s.Save(); } };
            gDisp.Controls.Add(_monitor);
            _fullscreen = new CheckBox { Text = "Schermo pieno", Location = new Point(80, 60), AutoSize = true };
            _fullscreen.CheckedChanged += delegate { if (!_loading) { _s.Fullscreen = _fullscreen.Checked; PlaceDisplay(); _s.Save(); } };
            gDisp.Controls.Add(_fullscreen);
            // Due pulsanti separati: quello attivo e' evidenziato, cosi' lo stato si vede a colpo d'occhio.
            _btnDisplayShow = Btn("MOSTRA", 200, 56, 115, 30, delegate { if (!_display.Visible) PlaceDisplay(); });
            _btnDisplayHide = Btn("NASCONDI", 319, 56, 115, 30, delegate { _display.Hide(); });
            foreach (var b in new[] { _btnDisplayShow, _btnDisplayHide })
            {
                b.FlatStyle = FlatStyle.Flat;
                b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                gDisp.Controls.Add(b);
            }

            var gLook = Group("Aspetto timer", 476, 124, 450, 330);
            gLook.Controls.Add(Lbl("Font:", 16, 30));
            _timerFontBox = FontCombo(80, 26, 220);
            gLook.Controls.Add(_timerFontBox);
            _timerBold = new CheckBox { Text = "Grassetto", Location = new Point(310, 28), AutoSize = true };
            gLook.Controls.Add(_timerBold);
            gLook.Controls.Add(Lbl("Dimensione (px):", 16, 64));
            _timerSize = Num(130, 60, 20, 2000); _timerSize.Increment = 10;
            gLook.Controls.Add(_timerSize);

            int cy = 100;
            _cBack = ColorRow(gLook, "Sfondo", 16, cy);
            _cNormal = ColorRow(gLook, "Normale (verde)", 16, cy + 36);
            _cWarn = ColorRow(gLook, "Avviso (arancio)", 16, cy + 72);
            _cDanger = ColorRow(gLook, "Critico / extra (rosso)", 16, cy + 108);

            gLook.Controls.Add(Lbl("Avviso sotto il", 270, cy + 76));
            _warnPct = Num(370, cy + 72, 1, 99); _warnPct.Width = 50;
            gLook.Controls.Add(_warnPct); gLook.Controls.Add(Lbl("%", 424, cy + 76));
            gLook.Controls.Add(Lbl("Critico sotto il", 270, cy + 112));
            _dangerPct = Num(370, cy + 108, 0, 99); _dangerPct.Width = 50;
            gLook.Controls.Add(_dangerPct); gLook.Controls.Add(Lbl("%", 424, cy + 112));

            _showBar = new CheckBox { Text = "Barra di avanzamento in alto", Location = new Point(16, cy + 150), AutoSize = true };
            gLook.Controls.Add(_showBar);
            gLook.Controls.Add(Lbl("Altezza barra (px):", 16, cy + 186));
            _barHeight = Num(140, cy + 182, 2, 400);
            gLook.Controls.Add(_barHeight);

            var gMsgLook = Group("Aspetto messaggio", 476, 460, 450, 140);
            gMsgLook.Controls.Add(Lbl("Font:", 16, 30));
            _msgFontBox = FontCombo(80, 26, 220);
            gMsgLook.Controls.Add(_msgFontBox);
            _msgBold = new CheckBox { Text = "Grassetto", Location = new Point(310, 28), AutoSize = true };
            gMsgLook.Controls.Add(_msgBold);
            gMsgLook.Controls.Add(Lbl("Dimensione (px):", 16, 64));
            _msgSize = Num(130, 60, 8, 1000); _msgSize.Increment = 5;
            gMsgLook.Controls.Add(_msgSize);
            _cMsg = ColorRow(gMsgLook, "Colore testo", 16, 96);

            var help = Lbl("Scorciatoie: F5 avvia/pausa  -  F8 reset  -  F9 mostra/nascondi messaggio", 476, 612);
            help.ForeColor = SystemColors.GrayText;
            Controls.Add(help);

            // --- Colonna destra: NDI e monitoraggio ---
            var gNdi = Group("Video (NDI o scheda USB) e posizione del timer", 940, 8, 460, 682);
            gNdi.Controls.Add(Lbl("Sorgente:", 16, 30));
            _ndiCombo = new ComboBox { Location = new Point(80, 26), Size = new Size(280, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            _ndiCombo.DropDown += delegate { RefreshSources(true); };
            _ndiCombo.SelectedIndexChanged += delegate
            {
                if (_loading) return;
                _s.NdiSource = _ndiCombo.SelectedItem as string ?? "";
                _s.Save();
                ApplyNdiState();
            };
            gNdi.Controls.Add(_ndiCombo);
            gNdi.Controls.Add(Btn("Aggiorna", 366, 24, 78, 28, delegate { RefreshSources(true); }));
            _ndiEnable = new CheckBox { Text = "Usa il video come sfondo del display", Location = new Point(16, 62), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _ndiEnable.CheckedChanged += delegate
            {
                if (_loading) return;
                _s.NdiEnabled = _ndiEnable.Checked;
                _s.Save();
                ApplyNdiState();
            };
            gNdi.Controls.Add(_ndiEnable);
            _ndiStatus = Lbl("", 16, 90); _ndiStatus.AutoSize = false; _ndiStatus.Size = new Size(428, 36);
            gNdi.Controls.Add(_ndiStatus);

            gNdi.Controls.Add(Lbl("Monitor dello schermo secondario:", 16, 130));
            _monitorPreview = MakePreview();
            _monitorPreview.Location = new Point(16, 150);
            _monitorPreview.Size = new Size(428, 245);
            gNdi.Controls.Add(_monitorPreview);
            var hint = Lbl("Trascina il riquadro per spostarlo, l'angolo giallo per ridimensionarlo, la rotellina per ingrandirlo o ridurlo.", 16, 400);
            hint.AutoSize = false; hint.Size = new Size(428, 34); hint.ForeColor = SystemColors.GrayText;
            gNdi.Controls.Add(hint);

            gNdi.Controls.Add(Lbl("Posizione rapida:", 16, 444));
            string[] arrows = { "↖", "↑", "↗", "←", "●", "→", "↙", "↓", "↘" };
            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3;
                var b = Btn(arrows[i], 16 + col * 46, 466 + row * 32, 42, 28, null);
                b.Tag = i;
                b.Click += delegate(object sender, EventArgs e) { SnapBox((int)((Button)sender).Tag); };
                gNdi.Controls.Add(b);
            }

            _cBox = ColorRow(gNdi, "Sfondo riquadro", 170, 466);
            gNdi.Controls.Add(Lbl("Opacita' sfondo (%):", 170, 504));
            _boxOpacity = Num(320, 500, 0, 100); _boxOpacity.Increment = 5;
            gNdi.Controls.Add(_boxOpacity);
            gNdi.Controls.Add(Btn("Apri anteprima grande", 170, 536, 274, 30, delegate { OpenBigPreview(); }));

            var attribution = new LinkLabel
            {
                Text = "NDI® is a registered trademark of Vizrt NDI AB - ndi.video",
                Location = new Point(16, 636), AutoSize = true, LinkColor = SystemColors.GrayText
            };
            attribution.LinkArea = new LinkArea(attribution.Text.Length - 9, 9);
            attribution.LinkClicked += delegate { try { Process.Start("https://ndi.video"); } catch { } };
            gNdi.Controls.Add(attribution);

            var contacts = new LinkLabel { Text = "Altre app e contatti: GitHub", Location = new Point(16, 656), AutoSize = true };
            contacts.LinkArea = new LinkArea(contacts.Text.Length - 6, 6);
            contacts.LinkClicked += delegate { try { Process.Start("https://github.com/ncrdavide-svg"); } catch { } };
            gNdi.Controls.Add(contacts);

            foreach (var n in new[] { _timerSize, _warnPct, _dangerPct, _barHeight, _msgSize, _boxOpacity })
                n.ValueChanged += delegate { ReadLookFromUi(); };
            foreach (var c in new[] { _timerBold, _msgBold, _showBar })
                c.CheckedChanged += delegate { ReadLookFromUi(); };
            foreach (var c in new[] { _timerFontBox, _msgFontBox })
                c.SelectedIndexChanged += delegate { ReadLookFromUi(); };
        }

        PreviewControl MakePreview()
        {
            var p = new PreviewControl { R = _renderer, S = _s, OutputSize = OutputSize };
            p.BoxChanged += delegate { RepaintOutputs(); };
            p.BoxCommitted += delegate { _s.Save(); };
            return p;
        }

        GroupBox Group(string text, int x, int y, int w, int h)
        {
            var g = new GroupBox { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
            Controls.Add(g);
            return g;
        }

        static Label Lbl(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        }

        static NumericUpDown Num(int x, int y, int min, int max)
        {
            return new NumericUpDown { Location = new Point(x, y), Minimum = min, Maximum = max, Width = 70 };
        }

        static Button Btn(string text, int x, int y, int w, int h, EventHandler click)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
            if (click != null) b.Click += click;
            return b;
        }

        static ComboBox FontCombo(int x, int y, int w)
        {
            var c = new ComboBox { Location = new Point(x, y), Size = new Size(w, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            using (var fonts = new InstalledFontCollection())
                foreach (var f in fonts.Families) c.Items.Add(f.Name);
            return c;
        }

        Button ColorRow(Control parent, string label, int x, int y)
        {
            parent.Controls.Add(Lbl(label, x, y + 4));
            var b = new Button { Location = new Point(x + 150, y), Size = new Size(90, 26), FlatStyle = FlatStyle.Flat };
            b.Click += delegate
            {
                using (var dlg = new ColorDialog { Color = b.BackColor, FullOpen = true })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        SetSwatch(b, dlg.Color);
                        ReadLookFromUi();
                    }
                }
            };
            parent.Controls.Add(b);
            return b;
        }

        static void SetSwatch(Button b, Color c)
        {
            b.BackColor = c;
            b.FlatAppearance.MouseOverBackColor = c;
            b.Text = Settings.ToHex(c);
            b.ForeColor = (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 140 ? Color.Black : Color.White;
        }

        // ---------------- impostazioni <-> UI ----------------
        void LoadIntoUi()
        {
            _h.Value = Clamp(_s.Hours, 0, 99);
            _m.Value = Clamp(_s.Minutes, 0, 59);
            _sec.Value = Clamp(_s.Seconds, 0, 59);
            RefreshPresets();
            RefreshMonitors();
            _fullscreen.Checked = _s.Fullscreen;
            SelectFont(_timerFontBox, _s.TimerFont);
            SelectFont(_msgFontBox, _s.MessageFont);
            _timerBold.Checked = _s.TimerBold;
            _msgBold.Checked = _s.MessageBold;
            _timerSize.Value = Clamp(_s.TimerSize, 20, 2000);
            _msgSize.Value = Clamp(_s.MessageSize, 8, 1000);
            _warnPct.Value = Clamp(_s.WarnPercent, 1, 99);
            _dangerPct.Value = Clamp(_s.DangerPercent, 0, 99);
            _showBar.Checked = _s.ShowBar;
            _barHeight.Value = Clamp(_s.BarHeight, 2, 400);
            SetSwatch(_cBack, Settings.ParseColor(_s.BackColor, Color.Black));
            SetSwatch(_cNormal, Settings.ParseColor(_s.NormalColor, Color.LimeGreen));
            SetSwatch(_cWarn, Settings.ParseColor(_s.WarnColor, Color.Orange));
            SetSwatch(_cDanger, Settings.ParseColor(_s.DangerColor, Color.Red));
            SetSwatch(_cMsg, Settings.ParseColor(_s.MessageColor, Color.White));
            SetSwatch(_cBox, Settings.ParseColor(_s.BoxColor, Color.Black));
            _boxOpacity.Value = Clamp(_s.BoxOpacity, 0, 100);
            _ndiEnable.Checked = _s.NdiEnabled;
            RefreshSources(true);
        }

        static decimal Clamp(int v, int min, int max) { return Math.Max(min, Math.Min(max, v)); }

        static void SelectFont(ComboBox c, string name)
        {
            int i = c.FindStringExact(name);
            if (i < 0) i = c.FindStringExact("Segoe UI");
            c.SelectedIndex = Math.Max(0, i);
        }

        void ReadLookFromUi()
        {
            if (_loading) return;
            _s.TimerFont = (string)_timerFontBox.SelectedItem ?? "Segoe UI";
            _s.MessageFont = (string)_msgFontBox.SelectedItem ?? "Segoe UI";
            _s.TimerBold = _timerBold.Checked;
            _s.MessageBold = _msgBold.Checked;
            _s.TimerSize = (int)_timerSize.Value;
            _s.MessageSize = (int)_msgSize.Value;
            _s.WarnPercent = (int)_warnPct.Value;
            _s.DangerPercent = (int)_dangerPct.Value;
            _s.ShowBar = _showBar.Checked;
            _s.BarHeight = (int)_barHeight.Value;
            _s.BackColor = Settings.ToHex(_cBack.BackColor);
            _s.NormalColor = Settings.ToHex(_cNormal.BackColor);
            _s.WarnColor = Settings.ToHex(_cWarn.BackColor);
            _s.DangerColor = Settings.ToHex(_cDanger.BackColor);
            _s.MessageColor = Settings.ToHex(_cMsg.BackColor);
            _s.BoxColor = Settings.ToHex(_cBox.BackColor);
            _s.BoxOpacity = (int)_boxOpacity.Value;
            _renderer.ApplySettings(_s);
            RepaintOutputs();
            _s.Save();
        }

        int UiTotalSeconds() { return (int)_h.Value * 3600 + (int)_m.Value * 60 + (int)_sec.Value; }

        void OnHmsChanged()
        {
            if (_loading) return;
            _s.Hours = (int)_h.Value; _s.Minutes = (int)_m.Value; _s.Seconds = (int)_sec.Value;
            // Se il timer e' fermo e mai partito, il display segue subito i valori impostati.
            if (!_engine.Running && _engine.ElapsedMs == 0) _engine.SetTotal(UiTotalSeconds() * 1000L);
            _s.Save();
        }

        void SetFromUi()
        {
            _engine.SetTotal(UiTotalSeconds() * 1000L);
        }

        // ---------------- preset ----------------
        void RefreshPresets()
        {
            _presetList.Items.Clear();
            foreach (var p in _s.Presets) _presetList.Items.Add(p);
        }

        void SavePreset()
        {
            string name = _presetName.Text.Trim();
            if (name.Length == 0) name = TimerEngine.FormatHms(UiTotalSeconds());
            var existing = _s.Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (MessageBox.Show(this, "Sovrascrivere il preset \"" + name + "\"?", "Preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                existing.Seconds = UiTotalSeconds();
            }
            else _s.Presets.Add(new Preset { Name = name, Seconds = UiTotalSeconds() });
            _s.Save();
            RefreshPresets();
            _presetName.Clear();
        }

        void LoadPreset()
        {
            var p = _presetList.SelectedItem as Preset;
            if (p == null) return;
            _loading = true;
            _h.Value = Clamp(p.Seconds / 3600, 0, 99);
            _m.Value = (p.Seconds % 3600) / 60;
            _sec.Value = p.Seconds % 60;
            _loading = false;
            _s.Hours = (int)_h.Value; _s.Minutes = (int)_m.Value; _s.Seconds = (int)_sec.Value;
            _s.Save();
            SetFromUi();
        }

        void DeletePreset()
        {
            var p = _presetList.SelectedItem as Preset;
            if (p == null) return;
            if (MessageBox.Show(this, "Eliminare il preset \"" + p.Name + "\"?", "Preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _s.Presets.Remove(p);
            _s.Save();
            RefreshPresets();
        }

        void MovePreset(int dir)
        {
            int i = _presetList.SelectedIndex, j = i + dir;
            if (i < 0 || j < 0 || j >= _s.Presets.Count) return;
            var p = _s.Presets[i];
            _s.Presets.RemoveAt(i);
            _s.Presets.Insert(j, p);
            _s.Save();
            RefreshPresets();
            _presetList.SelectedIndex = j;
        }

        // ---------------- NDI ----------------
        void ApplyNdiState()
        {
            _renderer.Overlay = _s.NdiEnabled;
            string key = _s.NdiSource ?? "";
            VideoSource want = null;
            string name = null;
            if (_s.NdiEnabled && key.StartsWith(NdiPrefix) && _ndi != null) { want = _ndi; name = key.Substring(NdiPrefix.Length); }
            else if (_s.NdiEnabled && key.StartsWith(UsbPrefix)) { want = _usb; name = key.Substring(UsbPrefix.Length); }

            foreach (VideoSource v in new VideoSource[] { _ndi, _usb })
                if (v != null && v != want && v.IsConnected) v.Disconnect();
            if (want != null && (!want.IsConnected || want.SourceName != name)) want.Connect(name);
            _renderer.Video = want;

            UpdateNdiStatus();
            RepaintOutputs();
        }

        // Elenco unico: sorgenti NDI in rete + schede di acquisizione USB.
        void RefreshSources(bool includeUsb)
        {
            if (includeUsb) _usbDevices = UsbCapture.ListDevices();
            var names = new List<string>();
            if (_ndi != null) foreach (var n in _ndi.GetSources()) names.Add(NdiPrefix + n);
            foreach (var n in _usbDevices) names.Add(UsbPrefix + n);
            if (!string.IsNullOrEmpty(_s.NdiSource) && !names.Contains(_s.NdiSource)) names.Insert(0, _s.NdiSource);
            var current = _ndiCombo.Items.Cast<string>().ToList();
            if (current.SequenceEqual(names)) return;

            bool wasLoading = _loading;
            _loading = true;
            _ndiCombo.BeginUpdate();
            _ndiCombo.Items.Clear();
            foreach (var n in names) _ndiCombo.Items.Add(n);
            _ndiCombo.SelectedIndex = names.IndexOf(_s.NdiSource);
            _ndiCombo.EndUpdate();
            _loading = wasLoading;
        }

        void UpdateNdiStatus()
        {
            string text;
            Color col = SystemColors.ControlText;
            var v = _renderer.Video;
            string ndiInfo = _ndi != null ? "NDI pronto (libreria " + NdiLoader.Origin + ")" : NdiLoader.Error;
            if (!_s.NdiEnabled)
            {
                text = "Video disattivato: il timer occupa tutto lo schermo.\n" + ndiInfo;
                col = SystemColors.GrayText;
            }
            else if (string.IsNullOrEmpty(_s.NdiSource))
            {
                text = "Scegli una sorgente NDI o una scheda USB dall'elenco.";
                col = Color.DarkOrange;
            }
            else if (v == null)
            {
                text = ndiInfo + " Il riquadro del timer funziona comunque, senza video.";
                col = Color.Firebrick;
            }
            else if (v.HasSignal)
            {
                var sz = v.FrameSize;
                text = "● IN RICEZIONE: " + _s.NdiSource + "\n" + sz.Width + "x" + sz.Height +
                       (v.Fps > 0 ? " @ " + v.Fps.ToString("0.##") + " fps" : "");
                col = Color.ForestGreen;
            }
            else
            {
                text = "In attesa del segnale da: " + _s.NdiSource + (string.IsNullOrEmpty(v.LastError) ? "" : "\n" + v.LastError);
                col = Color.DarkOrange;
            }
            if (_ndiStatus.Text != text) _ndiStatus.Text = text;
            if (_ndiStatus.ForeColor != col) _ndiStatus.ForeColor = col;
        }

        void OnVideoFrame()
        {
            // Chiamato dal thread di ricezione: un solo ridisegno in coda alla volta.
            if (Interlocked.CompareExchange(ref _framePending, 1, 0) != 0) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    Interlocked.Exchange(ref _framePending, 0);
                    if (_display.Visible) _display.Invalidate();
                });
            }
            catch { Interlocked.Exchange(ref _framePending, 0); }
        }

        void SnapBox(int index)
        {
            const double m = 0.03;
            int col = index % 3, row = index / 3;
            double w = _s.BoxW, h = _s.BoxH;
            _s.BoxX = col == 0 ? m : col == 1 ? (1 - w) / 2 : 1 - w - m;
            _s.BoxY = row == 0 ? m : row == 1 ? (1 - h) / 2 : 1 - h - m;
            _s.BoxX = Math.Max(0, Math.Min(1 - w, _s.BoxX));
            _s.BoxY = Math.Max(0, Math.Min(1 - h, _s.BoxY));
            _s.Save();
            RepaintOutputs();
        }

        void OpenBigPreview()
        {
            if (_bigPreviewWindow != null) { _bigPreviewWindow.Activate(); return; }
            var wa = Screen.PrimaryScreen.WorkingArea;
            _bigPreview = MakePreview();
            _bigPreview.Dock = DockStyle.Fill;
            _bigPreviewWindow = new Form
            {
                Text = "Anteprima schermo secondario",
                Icon = Program.AppIcon,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(wa.X + wa.Width / 2 - 520, wa.Y + wa.Height / 2 - 320, 1040, 640),
                ShowInTaskbar = false
            };
            _bigPreviewWindow.Controls.Add(_bigPreview);
            _bigPreviewWindow.FormClosed += delegate { _bigPreviewWindow = null; _bigPreview = null; };
            _bigPreviewWindow.Show(this);
        }

        Size OutputSize()
        {
            if (_display.Visible && _display.ClientSize.Width > 0) return _display.ClientSize;
            return SelectedScreen().Bounds.Size;
        }

        void RepaintOutputs()
        {
            if (_display.Visible) _display.Invalidate();
            InvalidatePreviews();
        }

        void InvalidatePreviews()
        {
            if (_monitorPreview != null) _monitorPreview.Invalidate();
            if (_bigPreview != null) _bigPreview.Invalidate();
        }

        // ---------------- monitor / display ----------------
        void RefreshMonitors()
        {
            bool wasLoading = _loading;
            _loading = true;
            _monitor.Items.Clear();
            var screens = Screen.AllScreens;
            int pick = -1;
            for (int i = 0; i < screens.Length; i++)
            {
                var sc = screens[i];
                _monitor.Items.Add((i + 1) + ":  " + sc.DeviceName.Replace("\\\\.\\", "") + "  " + sc.Bounds.Width + "x" + sc.Bounds.Height + (sc.Primary ? "  (principale)" : ""));
                if (sc.DeviceName == _s.DisplayDevice) pick = i;
            }
            if (pick < 0)
            {
                // Default: il primo monitor secondario, altrimenti il principale.
                for (int i = 0; i < screens.Length; i++) if (!screens[i].Primary) { pick = i; break; }
                if (pick < 0) pick = 0;
            }
            _monitor.SelectedIndex = pick;
            _loading = wasLoading;
        }

        Screen SelectedScreen()
        {
            var screens = Screen.AllScreens;
            int i = _monitor.SelectedIndex;
            return i >= 0 && i < screens.Length ? screens[i] : Screen.PrimaryScreen;
        }

        void PlaceDisplay()
        {
            var sc = SelectedScreen();
            // Con un solo monitor lo schermo pieno coprirebbe la regia: si usa una finestra.
            bool full = _s.Fullscreen && !(sc.Primary && Screen.AllScreens.Length == 1);
            // Senza owner: minimizzare la regia non deve nascondere il display.
            if (!_display.Visible) _display.Show();
            _display.PlaceOn(sc, full);
            if (!sc.Primary) Activate();
            InvalidatePreviews();
        }

        void OnDisplaysChanged(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                RefreshMonitors();
                if (_display.Visible) PlaceDisplay();
            });
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // La regia sta sempre sul monitor principale (CenterScreen userebbe quello del mouse).
            var wa = Screen.PrimaryScreen.WorkingArea;
            if (Width > wa.Width) Width = wa.Width;
            if (Height > wa.Height) Height = wa.Height;
            Location = new Point(wa.X + Math.Max(0, (wa.Width - Width) / 2), wa.Y + Math.Max(0, (wa.Height - Height) / 2));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PlaceDisplay();
            // A ogni avvio, come in LiveCue: invito a offrire un caffe'. Il display continua a funzionare.
            using (var support = new SupportForm()) support.ShowDialog(this);
        }

        // ---------------- comandi ----------------
        void ToggleStart()
        {
            if (_engine.Running) _engine.Pause(); else _engine.Start();
        }

        void ToggleMessage()
        {
            _renderer.MessageVisible = !_renderer.MessageVisible;
            RepaintOutputs();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5: ToggleStart(); return true;
                case Keys.F8: _engine.Reset(); return true;
                case Keys.F9: ToggleMessage(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- refresh periodico ----------------
        string _lastText, _lastDisplayText;
        Color _lastColor;
        int _lastBarStep = -1;

        void Refresh_()
        {
            _tickCount++;
            string text = _engine.DisplayText;
            Color col = _renderer.CurrentColor;

            if (text != _lastText || col != _lastColor)
            {
                _preview.Text = text;
                _preview.ForeColor = col;
                _preview.BackColor = _renderer.Back;
                _lastText = text; _lastColor = col;
            }

            // Ridisegna il display quando cambia il testo o la barra avanza di un pixel (su 2000).
            int barStep = (int)(_engine.RemainingFraction * 2000);
            if (_display.Visible && (barStep != _lastBarStep || text != _lastDisplayText))
            {
                _lastBarStep = barStep;
                _lastDisplayText = text;
                _display.Invalidate();
            }

            if (_tickCount % 2 == 0) InvalidatePreviews();
            if (_tickCount % 15 == 0) UpdateNdiStatus();
            if (_tickCount % 90 == 0 && !_ndiCombo.DroppedDown) RefreshSources(false);

            string state;
            if (_engine.Running) state = _engine.RemainingMs <= 0 ? "IN ONDA - TEMPO SCADUTO (conteggio extra)" : "IN ONDA";
            else if (_engine.ElapsedMs == 0) state = "PRONTO";
            else state = "IN PAUSA";
            if (_state.Text != state) _state.Text = state;

            string startLbl = _engine.Running ? "PAUSA  (F5)" : "AVVIA  (F5)";
            if (_btnStart.Text != startLbl) _btnStart.Text = startLbl;

            string onAir = _renderer.MessageVisible ? "MESSAGGIO IN ONDA" : "";
            if (_msgOnAir.Text != onAir) { _msgOnAir.Text = onAir; _msgOnAir.ForeColor = Color.Red; }
            string msgLbl = _renderer.MessageVisible ? "NASCONDI  (F9)" : "MOSTRA  (F9)";
            if (_btnMsgShow.Text != msgLbl) _btnMsgShow.Text = msgLbl;

            UpdateDisplayButtons();
        }

        void UpdateDisplayButtons()
        {
            bool on = _display.Visible;
            StyleToggle(_btnDisplayShow, on, Color.FromArgb(34, 160, 80));
            StyleToggle(_btnDisplayHide, !on, Color.FromArgb(200, 60, 60));
        }

        static void StyleToggle(Button b, bool active, Color activeColor)
        {
            Color back = active ? activeColor : SystemColors.Control;
            Color fore = active ? Color.White : SystemColors.GrayText;
            if (b.BackColor == back && b.ForeColor == fore) return;
            b.BackColor = back;
            b.ForeColor = fore;
            b.FlatAppearance.BorderColor = active ? activeColor : SystemColors.ControlDark;
            b.FlatAppearance.MouseOverBackColor = active ? activeColor : SystemColors.ControlLight;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
            _tick.Stop();
            _s.MessageText = _msgBox.Text;
            _s.Save();
            if (_ndi != null) { _ndi.FrameArrived -= OnVideoFrame; _ndi.Dispose(); }
            _usb.FrameArrived -= OnVideoFrame;
            _usb.Dispose();
            _display.AllowClose = true;
            _display.Close();
            base.OnFormClosed(e);
        }
    }
}
