using System;
using System.Drawing;
using System.Windows.Forms;

namespace VaultForge
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private TextBox _hermesBox;
        private TextBox _pythonBox;
        private TextBox _homeBox;
        private CheckBox _updatesBox;
        private CheckBox _autoBox;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            Text = "Pengaturan";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui(9.5f);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(700, 340);

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
                BackColor = Theme.Bg
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));

            grid.Controls.Add(LabelFor("Hermes executable"), 0, 0);
            _hermesBox = Box(settings.HermesExe);
            grid.Controls.Add(_hermesBox, 1, 0);
            grid.Controls.Add(BrowseButton(() => BrowseFile(_hermesBox, "Executable|*.exe|Semua file|*.*")), 2, 0);

            grid.Controls.Add(LabelFor("Python executable"), 0, 1);
            _pythonBox = Box(settings.PythonExe);
            grid.Controls.Add(_pythonBox, 1, 1);
            grid.Controls.Add(BrowseButton(() => BrowseFile(_pythonBox, "Python|python*.exe|Executable|*.exe")), 2, 1);

            grid.Controls.Add(LabelFor("Hermes home"), 0, 2);
            _homeBox = Box(settings.HermesHome);
            grid.Controls.Add(_homeBox, 1, 2);
            grid.Controls.Add(BrowseButton(() => BrowseFolder(_homeBox)), 2, 2);

            _updatesBox = new CheckBox
            {
                Text = "Cek update GitHub saat aplikasi dibuka",
                Checked = settings.CheckUpdates,
                ForeColor = Theme.Text,
                Font = Theme.Ui(9.5f),
                AutoSize = true,
                Margin = new Padding(0, 16, 0, 0)
            };
            grid.Controls.Add(_updatesBox, 1, 3);

            _autoBox = new CheckBox
            {
                Text = "Pasang update otomatis (OTA) saat tersedia",
                Checked = settings.AutoInstallUpdates,
                ForeColor = Theme.Text,
                Font = Theme.Ui(9.5f),
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };
            grid.Controls.Add(_autoBox, 1, 4);

            var hint = new Label
            {
                Text = "Python harus bisa mengimpor paket agent Hermes. Biasanya python.exe ada di folder yang sama dengan hermes.exe.",
                ForeColor = Theme.Muted,
                Font = Theme.Ui(8.5f),
                AutoSize = true,
                MaximumSize = new Size(500, 0),
                Margin = new Padding(0, 10, 0, 0)
            };
            grid.Controls.Add(hint, 1, 5);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Theme.Bg
            };
            var save = new FlatButton("SIMPAN", Theme.Accent, Theme.AccentDark) { Width = 110, Height = 32 };
            save.Click += (s, e) => SaveAndClose();
            var cancel = new FlatButton("BATAL", Theme.Surface2, Color.FromArgb(51, 65, 85)) { Width = 100, Height = 32 };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            panel.Controls.Add(grid);
            panel.Controls.Add(buttons);
            Controls.Add(panel);
            Theme.Apply(this);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private static Label LabelFor(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Theme.Muted,
                Font = Theme.Ui(9f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 6)
            };
        }

        private static TextBox Box(string value)
        {
            var box = new TextBox
            {
                Text = value ?? "",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 6, 4),
                Height = 28
            };
            Theme.StyleTextBox(box);
            return box;
        }

        private static FlatButton BrowseButton(Action action)
        {
            var b = new FlatButton("...", Theme.Surface2, Color.FromArgb(51, 65, 85))
            {
                Width = 40,
                Height = 28,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            b.Click += (s, e) => action();
            return b;
        }

        private static void BrowseFile(TextBox box, string filter)
        {
            using (var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true })
            {
                if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.FileName;
            }
        }

        private static void BrowseFolder(TextBox box)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.SelectedPath;
            }
        }

        private void SaveAndClose()
        {
            _settings.HermesExe = _hermesBox.Text.Trim();
            _settings.PythonExe = _pythonBox.Text.Trim();
            _settings.HermesHome = _homeBox.Text.Trim();
            _settings.CheckUpdates = _updatesBox.Checked;
            _settings.AutoInstallUpdates = _autoBox.Checked;
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
