using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace LiveCount
{
    // Finestra "Ti piace LiveCount?" mostrata a ogni avvio, come in LiveCue:
    // QR code e link Buy Me a Coffee nei colori del marchio. "No, grazie" si abilita
    // dopo qualche secondo, cosi' il QR code si nota davvero.
    public class SupportForm : Form
    {
        const string SupportUrl = "https://buymeacoffee.com/cin8";
        const int WaitSeconds = 3;
        static readonly Color Yellow = ColorTranslator.FromHtml("#FFDD00");
        static readonly Color Ink = ColorTranslator.FromHtml("#0D0C22");

        readonly Button _noThanks;
        readonly Timer _timer = new Timer { Interval = 1000 };
        int _secondsLeft = WaitSeconds;

        public SupportForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "LiveCount";
            Icon = Program.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Yellow;
            ClientSize = new Size(324, 450);
            Font = new Font("Segoe UI", 9F);

            Controls.Add(CenteredLabel("Ti piace LiveCount?", new Font("Segoe UI", 13.5F, FontStyle.Bold), 24, 30));
            Controls.Add(CenteredLabel("Se ti è utile, offrimi un caffè", new Font("Segoe UI", 10F), 56, 22));

            var qrPanel = new QrPanel { Location = new Point((324 - 200) / 2, 92), Size = new Size(200, 200) };
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("BuyMeACoffeeQR.png"))
                if (s != null) qrPanel.Image = Image.FromStream(s);
            Controls.Add(qrPanel);

            var link = CenteredLabel("buymeacoffee.com/cin8", new Font("Segoe UI", 10.5F, FontStyle.Underline | FontStyle.Bold), 306, 24);
            link.Cursor = Cursors.Hand;
            link.Click += delegate
            {
                try { Process.Start(SupportUrl); } catch { }
            };
            Controls.Add(link);

            _noThanks = new Button
            {
                Size = new Size(140, 38), Location = new Point((324 - 140) / 2, 360),
                FlatStyle = FlatStyle.Flat, BackColor = Ink, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F), Cursor = Cursors.Default
            };
            _noThanks.FlatAppearance.BorderSize = 0;
            // Non si usa Enabled=false: WinForms renderebbe il testo illeggibile sul nero.
            _noThanks.FlatAppearance.MouseOverBackColor = Ink;
            _noThanks.FlatAppearance.MouseDownBackColor = Ink;
            _noThanks.ForeColor = Color.FromArgb(150, 150, 160);
            _noThanks.Click += delegate { if (_secondsLeft <= 0) Close(); };
            Controls.Add(_noThanks);

            // Chi ha un codice sostenitore lo inserisce qui una volta: da li' in poi nessuna app
            // della suite mostra questa finestra (vedi SupporterCode).
            var codeLink = CenteredLabel("Ho un codice sostenitore", new Font("Segoe UI", 8.5F, FontStyle.Underline), 412, 20);
            codeLink.Cursor = Cursors.Hand;
            codeLink.Click += delegate { if (AskCode(this)) Close(); };
            Controls.Add(codeLink);
            ResumeLayout(false);

            UpdateButtonText();
            _timer.Tick += delegate
            {
                _secondsLeft--;
                if (_secondsLeft <= 0)
                {
                    _timer.Stop();
                    _noThanks.ForeColor = Color.White;
                    _noThanks.Cursor = Cursors.Hand;
                    _noThanks.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 44, 70);
                    _noThanks.Text = "No, grazie";
                }
                else UpdateButtonText();
            };
            _timer.Start();
        }

        void UpdateButtonText() { _noThanks.Text = "No, grazie (" + _secondsLeft + ")"; }

        // Finestrella in cui incollare il codice: si salva in %APPDATA%\LIVE e vale per tutte le app.
        static bool AskCode(IWin32Window owner)
        {
            using (var f = new Form())
            {
                f.AutoScaleDimensions = new SizeF(96F, 96F);
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.Text = "Codice sostenitore";
                f.Icon = Program.AppIcon;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Yellow;
                f.Font = new Font("Segoe UI", 9F);
                f.ClientSize = new Size(420, 200);

                var label = new Label
                {
                    Text = "Incolla il codice sostenitore che hai ricevuto (va bene anche tutto il testo del file sostenitore.txt).",
                    ForeColor = Ink, Location = new Point(16, 12), Size = new Size(388, 38)
                };
                var box = new TextBox
                {
                    Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9F),
                    Location = new Point(16, 54), Size = new Size(388, 90)
                };
                var ok = new Button
                {
                    Text = "Attiva", Location = new Point(228, 156), Size = new Size(84, 30),
                    FlatStyle = FlatStyle.Flat, BackColor = Ink, ForeColor = Color.White, Cursor = Cursors.Hand
                };
                ok.FlatAppearance.BorderSize = 0;
                var cancel = new Button
                {
                    Text = "Annulla", Location = new Point(320, 156), Size = new Size(84, 30),
                    DialogResult = DialogResult.Cancel, BackColor = Color.White
                };
                f.CancelButton = cancel;
                ok.Click += delegate
                {
                    string name;
                    try { name = SupporterCode.Save(box.Text); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(f, "Non riesco a salvare il codice: " + ex.Message, f.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (name == null)
                    {
                        MessageBox.Show(f, "Questo codice non è valido. Controlla di averlo copiato tutto, firma compresa.",
                            f.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    MessageBox.Show(f, "Grazie, " + name + "!\n\nDa ora la finestra del caffè non comparirà più, in tutte le app della suite LIVE.",
                        f.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.AddRange(new Control[] { label, box, ok, cancel });
                return f.ShowDialog(owner) == DialogResult.OK;
            }
        }

        static Label CenteredLabel(string text, Font font, int y, int h)
        {
            return new Label
            {
                Text = text, Font = font, ForeColor = Ink, BackColor = Color.Transparent,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, y), Size = new Size(324, h)
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        // Riquadro bianco con angoli arrotondati che contiene il QR code.
        class QrPanel : Control
        {
            public Image Image;

            public QrPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Yellow);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float r = 16;
                using (var p = new GraphicsPath())
                {
                    var rc = new RectangleF(0, 0, Width - 1, Height - 1);
                    p.AddArc(rc.X, rc.Y, r, r, 180, 90);
                    p.AddArc(rc.Right - r, rc.Y, r, r, 270, 90);
                    p.AddArc(rc.Right - r, rc.Bottom - r, r, r, 0, 90);
                    p.AddArc(rc.X, rc.Bottom - r, r, r, 90, 90);
                    p.CloseFigure();
                    g.FillPath(Brushes.White, p);
                }
                if (Image != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    int pad = Width / 18;
                    g.DrawImage(Image, new Rectangle(pad, pad, Width - 2 * pad, Height - 2 * pad));
                }
            }
        }
    }
}
