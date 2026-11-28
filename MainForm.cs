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
        private const string AppVersion = "1.0.7";

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
        private FlatButton _otaButton;
        private UpdateInfo _latestInfo;
        private FlatButton _themeButton;
        private ProcessGaugePanel _gauges;
        private SystemIndicators _system;
        private bool _fullscreen;
        private Rectangle _restoreBounds;

        public MainForm()
        {
            _settings = AppSettings.Load();
            _vault = new VaultService(_settings);
            Theme.SetDark(_settings.DarkTheme);

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
            Theme.Apply(this);
            ApplyThemeExtras();
            Shown += OnShown;
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
                BackColor = Theme.BannerBg,
                Visible = false,
                Padding = new Padding(12, 6, 12, 6)
            };
            _updateLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.BannerText,
                Font = Theme.Ui(9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };
            var updateButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _otaButton = new FlatButton("UPDATE OTA", Theme.Accent, Theme.AccentDark)
            {
                Width = 122,
                Height = 28
            };
            _otaButton.Click += (s, e) => StartOta();
            var manualButton = new FlatButton("UNDUH", Theme.Surface2, Color.FromArgb(51, 65, 85))
            {
                Width = 70,
                Height = 28
            };
            manualButton.Click += (s, e) => OpenUpdate();
            _themeButton = new FlatButton(ThemeText(), Theme.Surface2, Theme.Surface2)
            {
                Width = 74,
                Height = 28
            };
            _themeButton.Click += (s, e) => ToggleTheme();
            updateButtons.Controls.Add(_otaButton);
            updateButtons.Controls.Add(manualButton);
            updateButtons.Controls.Add(_themeButton);
            _updateBanner.Controls.Add(_updateLabel);
            _updateBanner.Controls.Add(updateButtons);

            Controls.Add(content);
            Controls.Add(status);
            Controls.Add(_updateBanner);
            Controls.Add(BuildMenuBar());
        }

        private MenuStrip BuildMenuBar()
        {
            var menu = new MenuStrip
            {
                Dock = DockStyle.Top,
                BackColor = Theme.Surface2,
                ForeColor = Theme.Text,
                RenderMode = ToolStripRenderMode.System
            };
            MainMenuStrip = menu;

            var file = new ToolStripMenuItem("File");
            var exit = new ToolStripMenuItem("Keluar");
            exit.Click += (s, e) => Close();
            file.DropDownItems.Add(exit);

            var view = new ToolStripMenuItem("View");
            var full = new ToolStripMenuItem("Penuh");
            full.Click += (s, e) => ToggleFullscreen();
            var sixteenth = new ToolStripMenuItem("1/16 Layar");
            sixteenth.Click += (s, e) => SetSixteenth();
            var minimize = new ToolStripMenuItem("Kecilkan");
            minimize.Click += (s, e) => WindowState = FormWindowState.Minimized;
            view.DropDownItems.Add(full);
            view.DropDownItems.Add(sixteenth);
            view.DropDownItems.Add(minimize);

            var settingsItem = new ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) => OpenSettings();

            var help = new ToolStripMenuItem("Help");
            var about = new ToolStripMenuItem("Tentang VaultForge");
            about.Click += (s, e) => MessageBox.Show(this,
                "VaultForge v" + AppVersion + "\nGUI Hermes local vault via PowerShell bridge.\nirgun.web.id",
                "Tentang", MessageBoxButtons.OK, MessageBoxIcon.Information);
            help.DropDownItems.Add(about);

            menu.Items.Add(file);
            menu.Items.Add(view);
            menu.Items.Add(settingsItem);
            menu.Items.Add(help);
            return menu;
        }

        private static string ThemeText()
        {
            return Theme.Dark ? "\u2600 Light" : "\u263e Dark";
        }

        private void ToggleTheme()
        {
            Theme.SetDark(!Theme.Dark);
            _settings.DarkTheme = Theme.Dark;
            _settings.Save();
            Theme.Apply(this);
            ApplyThemeExtras();
            _themeButton.Text = ThemeText();
        }

        private void ApplyThemeExtras()
        {
            if (_updateBanner != null)
            {
                _updateBanner.BackColor = Theme.BannerBg;
                _updateLabel.ForeColor = Theme.BannerText;
            }
            if (_gauges != null) _gauges.ApplyTheme();
            if (_system != null) _system.ApplyTheme();
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

            _system = new SystemIndicators { Dock = DockStyle.Bottom, Height = 118 };
            card.Controls.Add(grid);
            card.Controls.Add(_system);
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
                BorderStyle = BorderStyle.FixedSingle,
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
            Theme.StyleGrid(_grid);
            _grid.DefaultCellStyle.Font = Theme.Ui(9f);
            _grid.ColumnHeadersDefaultCellStyle.Font = Theme.Ui(9f, FontStyle.Bold);

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

            _gauges = new ProcessGaugePanel
            {
                Dock = DockStyle.Bottom,
                Height = 104,
                Margin = Padding.Empty
            };
            card.Controls.Add(_grid);
            card.Controls.Add(bottom);
            card.Controls.Add(_gauges);
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
                BackColor = Theme.Surface2,
                Padding = new Padding(12, 4, 12, 4)
            };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Siap. Pastikan Hermes dan Python sudah terpasang.",
                ForeColor = Theme.Text,
                Font = Theme.Ui(9f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var version = new Label
            {
                Dock = DockStyle.Right,
                Width = 150,
                Text = "v" + AppVersion + "  •  irgun.web.id",
                ForeColor = Theme.Muted,
                Font = Theme.Mono(8f),
                TextAlign = ContentAlignment.MiddleRight
            };

            _checkUpdateButton = new FlatButton("CEK UPDATE", Theme.Surface2, Color.FromArgb(51, 65, 85))
            {
                Dock = DockStyle.Right,
                Width = 108,
                Height = 24,
                Font = Theme.Ui(9f),
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
                ForeColor = Theme.Text,
                Font = Theme.Ui(9f),
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 2)
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
                        _latestInfo = info;
                        _updateBanner.Visible = true;
                        _updateLabel.Text = "Update v" + info.Version + " tersedia — klik UPDATE OTA";
                        SetStatus("Update v" + info.Version + " tersedia di GitHub.", Theme.Accent);
                        if (_settings.AutoInstallUpdates) StartOta();
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

        private void StartOta()
        {
            if (_latestInfo == null || string.IsNullOrWhiteSpace(_latestInfo.MsiUrl))
            {
                SetStatus("Info update belum tersedia. Klik CEK UPDATE dulu.", Theme.Warning);
                return;
            }
            _otaButton.Enabled = false;
            SetStatus("Menyiapkan update OTA...", Theme.Muted);
            var progress = new Progress<string>(text =>
            {
                _updateLabel.Text = text;
                SetStatus(text, Theme.Muted);
            });

            OtaUpdater.InstallAsync(_latestInfo, progress).ContinueWith(t =>
            {
                BeginInvoke((Action)(() =>
                {
                    var result = t.Result;
                    if (result.Ok)
                    {
                        _updateLabel.Text = "Installer siap. Menutup VaultForge untuk pemasangan...";
                        SetStatus("Memasang update v" + _latestInfo.Version + "...", Theme.Accent);
                        Application.Exit();
                    }
                    else
                    {
                        _otaButton.Enabled = true;
                        _updateLabel.Text = "Update gagal: " + FirstLine(result.Error);
                        SetStatus("Update gagal: " + FirstLine(result.Error), Theme.Danger);
                    }
                }));
            });
        }

        private void OpenUpdate()
        {
            try
            {
                var info = _latestInfo ?? UpdateChecker.Check(AppVersion);
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
