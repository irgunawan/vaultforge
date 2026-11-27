using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Windows.Forms;

namespace VaultForge
{
    internal sealed class SystemIndicators : Panel
    {
        private sealed class Tile : Panel
        {
            private readonly Label _caption;
            private readonly Label _value;
            private readonly Panel _bar;
            private readonly Panel _fill;

            public Tile(string caption)
            {
                BackColor = Theme.Surface;
                BorderStyle = BorderStyle.FixedSingle;
                Padding = new Padding(6, 4, 6, 5);
                _caption = new Label
                {
                    Text = caption, Dock = DockStyle.Top, Height = 14,
                    Font = Theme.Ui(8f), ForeColor = Theme.Muted
                };
                _value = new Label
                {
                    Text = "-", Dock = DockStyle.Top, Height = 22,
                    Font = Theme.Ui(12f, FontStyle.Bold), ForeColor = Theme.Text
                };
                _bar = new Panel { Dock = DockStyle.Bottom, Height = 6, BackColor = Color.FromArgb(229, 229, 229) };
                _fill = new Panel { Dock = DockStyle.Left, Width = 0, BackColor = Theme.Accent };
                _bar.Controls.Add(_fill);
                Controls.Add(_bar);
                Controls.Add(_value);
                Controls.Add(_caption);
            }

            public void Set(string text, double pct, bool warm)
            {
                _value.Text = text;
                int full = Math.Max(1, _bar.Width - 2);
                _fill.Width = (int)(Math.Max(0, Math.Min(100, pct)) * full / 100.0);
                _fill.BackColor = warm ? Color.FromArgb(197, 102, 0) : Theme.Accent;
            }

            public void ApplyTheme()
            {
                BackColor = Theme.Surface;
                _caption.ForeColor = Theme.Muted;
                _value.ForeColor = Theme.Text;
                _fill.BackColor = Theme.Accent;
            }
        }

        private readonly Tile _cpu = new Tile("CPU");
        private readonly Tile _ram = new Tile("RAM");
        private readonly Tile _hdd = new Tile("HDD C:");
        private readonly Tile _temp = new Tile("Suhu CPU");
        private readonly Timer _timer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        public SystemIndicators()
        {
            BackColor = Theme.Surface2;
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Theme.Surface2,
                Margin = Padding.Empty,
                Padding = new Padding(4)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            grid.Controls.Add(_cpu, 0, 0);
            grid.Controls.Add(_ram, 1, 0);
            grid.Controls.Add(_hdd, 0, 1);
            grid.Controls.Add(_temp, 1, 1);
            Controls.Add(grid);

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();
            }
            catch { _cpuCounter = null; }
            try
            {
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _ramCounter.NextValue();
            }
            catch { _ramCounter = null; }

            _timer = new Timer { Interval = 2000 };
            _timer.Tick += (s, e) => RefreshValues();
            Disposed += (s, e) => _timer.Stop();
            RefreshValues();
            _timer.Start();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface2;
            _cpu.ApplyTheme();
            _ram.ApplyTheme();
            _hdd.ApplyTheme();
            _temp.ApplyTheme();
        }

        private void RefreshValues()
        {
            double cpu = 0;
            try { if (_cpuCounter != null) cpu = _cpuCounter.NextValue(); }
            catch { }
            _cpu.Set(((int)cpu) + "%", cpu, false);

            double availableMb = 0;
            try { if (_ramCounter != null) availableMb = _ramCounter.NextValue(); }
            catch { }
            double totalMb = 0;
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        totalMb = Convert.ToDouble(item["TotalVisibleMemorySize"]) / 1024.0;
                        break;
                    }
                }
            }
            catch { }
            double usedMb = Math.Max(0, totalMb - availableMb);
            _ram.Set(totalMb > 0
                ? usedMb.ToString("0.0") + " / " + (totalMb / 1024.0).ToString("0.0") + " GB"
                : "n/a",
                totalMb > 0 ? usedMb / totalMb * 100.0 : 0, false);

            try
            {
                var drive = new DriveInfo("C:");
                double total = drive.TotalSize / 1073741824.0;
                double free = drive.AvailableFreeSpace / 1073741824.0;
                double used = total - free;
                _hdd.Set(used.ToString("0.0") + " / " + total.ToString("0.0") + " GB",
                    total > 0 ? used / total * 100.0 : 0, false);
            }
            catch { _hdd.Set("n/a", 0, false); }

            bool hasTemp = false;
            double temp = 0;
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (var item in searcher.Get())
                    {
                        double celsius = Convert.ToDouble(item["CurrentTemperature"]) / 10.0 - 273.15;
                        if (celsius > 0 && celsius < 150)
                        {
                            temp = celsius;
                            hasTemp = true;
                            break;
                        }
                    }
                }
            }
            catch { }
            _temp.Set(hasTemp ? temp.ToString("0") + " °C" : "n/a",
                hasTemp ? Math.Min(100, temp) : 0, true);
        }
    }
}
