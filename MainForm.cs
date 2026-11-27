using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VaultForge
{
    internal sealed class MainForm : Form
    {
        private const string AppVersion = "1.0.1";

        private readonly AppSettings _settings;
        private VaultService _vault;
        private DataGridView _grid;
        private TextBox _labelBox;
        private TextBox _originBox;
        private ComboBox _idTypeBox;
        private TextBox _identifierBox;
        private TextBox _passwordBox;
        private TextBox _otpBox;
        private Label _statusLabel;
        private Label _updateLabel;
        private Panel _updateBanner;
        private FlatButton _saveButton;
        private FlatButton _refreshButton;
        private FlatButton _deleteButton;
        private FlatButton _checkUpdateButton;
        private bool _fullscreen;
        private Rectangle _restoreBounds;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public MainForm()
        {
            _settings = AppSettings.Load();
            _vault = new VaultService(_settings);

            Text = "VaultForge";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui(9.5f);
            MinimumSize = new Size(920, 620);
            Size = new Size(1120, 740);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            BuildUi();
            Shown += OnShown;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            }
            catch
            {
            }
        }

        private void BuildUi()
        {
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(16, 12, 16, 8),
                ColumnCount = 2,
                RowCount = 1
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            content.Controls.Add(BuildFormCard(), 0, 0);
            content.Controls.Add(BuildListCard(), 1, 0);

            var status = BuildStatusBar();

            _updateBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(6, 78, 59),
                Visible = false,
                Padding = new Padding(16, 6, 16, 6)
            };
            _updateLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(167, 243, 208),
                Font = Theme.Ui(9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };
            var downloadButton = new FlatButton("UNDUH UPDATE", Theme.Accent, Theme.AccentDark)
            {
                Dock = DockStyle.Right,
                Width = 150,
                Height = 28
            };
            downloadButton.Click += (s, e) => OpenUpdate();
            _updateBanner.Controls.Add(_updateLabel);
            _updateBanner.Controls.Add(downloadButton);

            Controls.Add(content);
            Controls.Add(status);
            Controls.Add(_updateBanner);
            Controls.Add(BuildTopBar());
        }

        private Panel BuildTopBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Theme.Bg,
                Padding = new Padding(20, 10, 14, 6)
            };

            var title = new Label
            {
                Text = "HERMES VAULT",
                ForeColor = Theme.Accent,
                Font = Theme.Mono(14f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 12)
            };
            var subtitle = new Label
            {
                Text = "PowerShell bridge  •  Windows 10 / 11",
                ForeColor = Theme.Muted,
                Font = Theme.Ui(9f),
                AutoSize = true,
                Location = new Point(22, 37)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 240,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Theme.Bg
            };

            var close = WindowButton("✕", Theme.Danger);
            close.Click += (s, e) => Close();
            var minimize = WindowButton("—", Theme.Surface2);
            minimize.Click += (s, e) => WindowState = FormWindowState.Minimized;
            var sixteenth = WindowButton("▣", Theme.Surface2);
            sixteenth.Click += (s, e) => SetSixteenth();
            var full = WindowButton("⛶", Theme.Surface2);
            full.Click += (s, e) => ToggleFullscreen();
            var settings = WindowButton("⚙", Theme.Surface2);
            settings.Click += (s, e) => OpenSettings();

            buttons.Controls.Add(close);
            buttons.Controls.Add(minimize);
            buttons.Controls.Add(sixteenth);
            buttons.Controls.Add(full);
            buttons.Controls.Add(settings);

            bar.Controls.Add(title);
            bar.Controls.Add(subtitle);
            bar.Controls.Add(buttons);
            return bar;
        }

        private FlatButton WindowButton(string text, Color color)
        {
            return new FlatButton(text, color, ControlPaint.Light(color, 0.12f))
            {
                Width = 38,
                Height = 28,
                Margin = new Padding(4, 0, 0, 0),
                Font = Theme.Ui(9.5f, FontStyle.Bold),
                TabStop = false
            };
        }

        private Control BuildFormCard()
        {
            var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoScroll = true,
                BackColor = Theme.Surface
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var heading = new Label
            {
                Text = "Tambah Login",
                ForeColor = Theme.Text,
                Font = Theme.Ui(12f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            grid.Controls.Add(heading);

            grid.Controls.Add(FieldLabel("Label"));
            _labelBox = FieldBox();
            _labelBox.MaxLength = 120;
            grid.Controls.Add(_labelBox);

            grid.Controls.Add(FieldLabel("Site origin (https://...)"));
            _originBox = FieldBox();
            _originBox.MaxLength = 300;
            grid.Controls.Add(_originBox);

            grid.Controls.Add(FieldLabel("Identifier type"));
            _idTypeBox = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.Ui(10f),
                Margin = new Padding(0, 0, 0, 8)
            };
            _idTypeBox.Items.AddRange(new object[] { "email", "username", "phone" });
            _idTypeBox.SelectedIndex = 0;
            grid.Controls.Add(_idTypeBox);

            grid.Controls.Add(FieldLabel("Identifier"));
            _identifierBox = FieldBox();
            _identifierBox.MaxLength = 200;
            grid.Controls.Add(_identifierBox);

            grid.Controls.Add(FieldLabel("Password"));
            _passwordBox = FieldBox(true);
            _passwordBox.MaxLength = 400;
            grid.Controls.Add(_passwordBox);

            grid.Controls.Add(FieldLabel("Authenticator key (opsional)"));
            _otpBox = FieldBox();
            _otpBox.MaxLength = 400;
            grid.Controls.Add(_otpBox);

            _saveButton = new FlatButton("SIMPAN KE VAULT", Theme.Accent, Theme.AccentDark)
            {
                Dock = DockStyle.Top,
                Height = 38,
                Margin = new Padding(0, 14, 0, 6)
            };
            _saveButton.Click += (s, e) => SaveLogin();
            grid.Controls.Add(_saveButton);

            var clear = new FlatButton("BERSIHKAN", Theme.Surface2, Color.FromArgb(51, 65, 85))
            {
                Dock = DockStyle.Top,
                Height = 32,
                Margin = new Padding(0, 0, 0, 0)
            };
            clear.Click += (s, e) => ClearForm();
            grid.Controls.Add(clear);

            var hint = new Label
            {
                Text = "Password tidak pernah ditampilkan kembali. Vault memakai kunci lokal Hermes.",
                ForeColor = Theme.Muted,
                Font = Theme.Ui(8.5f),
                AutoSize = true,
                MaximumSize = new Size(320, 0),
                Margin = new Padding(0, 12, 0, 0)
            };
            grid.Controls.Add(hint);

            card.Controls.Add(grid);
            return card;
        }

        private Control BuildListCard()
        {
            var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };

            var header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Theme.Surface };
            var title = new Label
            {
                Text = "Item Vault",
                ForeColor = Theme.Text,
                Font = Theme.Ui(12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 6)
            };
            _refreshButton = new FlatButton("REFRESH", Theme.Purple, Color.FromArgb(109, 40, 217))
            {
                Dock = DockStyle.Right,
                Width = 100,
                Height = 30
            };
            _refreshButton.Click += (s, e) => LoadItems();
            header.Controls.Add(title);
            header.Controls.Add(_refreshButton);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Theme.Surface,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                GridColor = Theme.Border,
                ColumnHeadersHeight = 32,
                RowTemplate = { Height = 30 }
            };
            _grid.DefaultCellStyle.BackColor = Theme.Surface;
            _grid.DefaultCellStyle.ForeColor = Theme.Text;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(6, 78, 59);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.DefaultCellStyle.Font = Theme.Ui(9f);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface2;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
            _grid.ColumnHeadersDefaultCellStyle.Font = Theme.Ui(8.5f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface2;

            _grid.Columns.Add(NewColumn("handle", "HANDLE", 22));
            _grid.Columns.Add(NewColumn("kind", "KIND", 10));
            _grid.Columns.Add(NewColumn("label", "LABEL", 26));
            _grid.Columns.Add(NewColumn("identifier", "IDENTIFIER", 22));
            _grid.Columns.Add(NewColumn("origin", "ORIGIN", 20));

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Surface, Padding = new Padding(0, 8, 0, 0) };
            _deleteButton = new FlatButton("HAPUS ITEM", Theme.Danger, Color.FromArgb(220, 38, 38))
            {
                Dock = DockStyle.Right,
                Width = 120,
                Height = 32
            };
            _deleteButton.Click += (s, e) => DeleteSelected();
            bottom.Controls.Add(_deleteButton);

            card.Controls.Add(_grid);
            card.Controls.Add(bottom);
            card.Controls.Add(header);
            return card;
        }

        private static DataGridViewTextBoxColumn NewColumn(string name, string header, int fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private Panel BuildStatusBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = Color.FromArgb(8, 13, 25),
                Padding = new Padding(16, 6, 12, 4)
            };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Siap. Pastikan Hermes dan Python sudah terpasang.",
                ForeColor = Theme.Muted,
                Font = Theme.Ui(8.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var version = new Label
            {
                Dock = DockStyle.Right,
                Width = 150,
                Text = "v" + AppVersion + "  •  irgun.web.id",
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = Theme.Mono(8f),
                TextAlign = ContentAlignment.MiddleRight
            };

            _checkUpdateButton = new FlatButton("CEK UPDATE", Theme.Surface2, Color.FromArgb(51, 65, 85))
            {
                Dock = DockStyle.Right,
                Width = 108,
                Height = 24,
                Font = Theme.Ui(8f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _checkUpdateButton.Click += (s, e) => CheckUpdates(true);

            bar.Controls.Add(_statusLabel);
            bar.Controls.Add(version);
            bar.Controls.Add(_checkUpdateButton);
            return bar;
        }

        private static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Theme.Muted,
                Font = Theme.Ui(8.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 3)
            };
        }

        private static TextBox FieldBox(bool password = false)
        {
            var box = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                Margin = new Padding(0, 0, 0, 2),
                UseSystemPasswordChar = password
            };
            Theme.StyleTextBox(box);
            return box;
        }

        private void OnShown(object sender, EventArgs e)
        {
            LoadItems();
            if (_settings.CheckUpdates)
            {
                CheckUpdates(false);
            }
        }

        private void SaveLogin()
        {
            string label = _labelBox.Text.Trim();
            string origin = _originBox.Text.Trim();
            string identifier = _identifierBox.Text.Trim();
            string password = _passwordBox.Text;
            string otp = _otpBox.Text.Trim();
            string idType = Convert.ToString(_idTypeBox.SelectedItem ?? "email");

            if (label.Length == 0 || origin.Length == 0 || identifier.Length == 0 || password.Length == 0)
            {
                SetStatus("Label, origin, identifier, dan password wajib diisi.", Theme.Warning);
                return;
            }
            if (!origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Origin harus dimulai http:// atau https://", Theme.Warning);
                return;
            }

            _saveButton.Enabled = false;
            SetStatus("Menyimpan ke vault Hermes...", Theme.Muted);

            Task.Run(() => _vault.AddLogin(label, origin, idType, identifier, password, otp))
                .ContinueWith(t =>
                {
                    BeginInvoke((Action)(() =>
                    {
                        _saveButton.Enabled = true;
                        var result = t.Result;
                        if (result.Ok)
                        {
                            SetStatus("Tersimpan. Handle=" + result.Handle, Theme.Accent);
                            _passwordBox.Text = "";
                            _otpBox.Text = "";
                            LoadItems();
                        }
                        else
                        {
                            SetStatus("Gagal: " + FirstLine(result.Error), Theme.Danger);
                        }
                    }));
                });
        }

        private void LoadItems()
        {
            _refreshButton.Enabled = false;
            SetStatus("Membaca vault...", Theme.Muted);
            Task.Run(() => _vault.List()).ContinueWith(t =>
            {
                BeginInvoke((Action)(() =>
                {
                    _refreshButton.Enabled = true;
                    var result = t.Result;
                    _grid.Rows.Clear();
                    if (!result.Ok)
                    {
                        SetStatus("Gagal baca vault: " + FirstLine(result.Error), Theme.Danger);
                        return;
                    }
                    foreach (var item in result.Items)
                    {
                        _grid.Rows.Add(item.Handle, item.Kind, item.Label, item.Identifier, item.Origin);
                    }
                    SetStatus(result.Items.Count + " item di vault.", Theme.Muted);
                }));
            });
        }

        private void DeleteSelected()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                SetStatus("Pilih item yang mau dihapus dulu.", Theme.Warning);
                return;
            }
            string handle = Convert.ToString(_grid.SelectedRows[0].Cells[0].Value);
            if (string.IsNullOrWhiteSpace(handle)) return;
            if (MessageBox.Show(this, "Hapus item " + handle + "?", "Konfirmasi",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            SetStatus("Menghapus " + handle + "...", Theme.Muted);
            Task.Run(() => _vault.Remove(handle)).ContinueWith(t =>
            {
                BeginInvoke((Action)(() =>
                {
                    var result = t.Result;
                    if (result.Ok)
                    {
                        SetStatus("Item dihapus: " + handle, Theme.Accent);
                        LoadItems();
                    }
                    else
                    {
                        SetStatus("Gagal hapus: " + FirstLine(result.Error), Theme.Danger);
                    }
                }));
            });
        }

        private void ClearForm()
        {
            _labelBox.Text = "";
            _originBox.Text = "";
            _identifierBox.Text = "";
            _passwordBox.Text = "";
            _otpBox.Text = "";
            _idTypeBox.SelectedIndex = 0;
            _labelBox.Focus();
        }

        private void CheckUpdates(bool manual)
        {
            _checkUpdateButton.Enabled = false;
            if (manual) SetStatus("Mengecek update GitHub...", Theme.Muted);

            Task.Run(() => UpdateChecker.Check(AppVersion)).ContinueWith(t =>
            {
                BeginInvoke((Action)(() =>
                {
                    _checkUpdateButton.Enabled = true;
                    var info = t.Result;
                    if (info.Available)
                    {
                        _updateBanner.Visible = true;
                        _updateLabel.Text = "Update tersedia: v" + info.Version + " — klik UNDUH UPDATE";
                        SetStatus("Update v" + info.Version + " tersedia di GitHub.", Theme.Accent);
                    }
                    else if (manual)
                    {
                        if (!string.IsNullOrWhiteSpace(info.Error) && info.Error != "Belum ada release di GitHub.")
                        {
                            SetStatus("Cek update gagal: " + FirstLine(info.Error), Theme.Warning);
                        }
                        else
                        {
                            SetStatus("Sudah versi terbaru (v" + AppVersion + ").", Theme.Accent);
                        }
                    }
                }));
            });
        }

        private void OpenUpdate()
        {
            try
            {
                var info = UpdateChecker.Check(AppVersion);
                string url = !string.IsNullOrWhiteSpace(info.MsiUrl) ? info.MsiUrl : info.ReleaseUrl;
                if (!string.IsNullOrWhiteSpace(url)) Process.Start(url);
            }
            catch (Exception ex)
            {
                SetStatus("Gagal membuka update: " + ex.Message, Theme.Danger);
            }
        }

        private void OpenSettings()
        {
            using (var dialog = new SettingsForm(_settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _vault = new VaultService(_settings);
                    SetStatus("Pengaturan disimpan.", Theme.Accent);
                    LoadItems();
                }
            }
        }

        private void ToggleFullscreen()
        {
            if (!_fullscreen)
            {
                _restoreBounds = Bounds;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                _fullscreen = true;
            }
            else
            {
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.Sizable;
                Bounds = _restoreBounds;
                _fullscreen = false;
            }
        }

        private void SetSixteenth()
        {
            if (_fullscreen) ToggleFullscreen();
            var area = Screen.FromControl(this).WorkingArea;
            Width = Math.Max(640, area.Width / 4);
            Height = Math.Max(420, area.Height / 4);
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }

        private void SetStatus(string text, Color color)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(tanpa pesan)";
            int idx = text.IndexOfAny(new[] { '\r', '\n' });
            return idx >= 0 ? text.Substring(0, idx) : text;
        }
    }
}
