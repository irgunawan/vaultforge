using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VaultForge
{
    internal sealed class ProcessGaugePanel : Panel
    {
        private sealed class Proc
        {
            public string Name;
            public int Pid;
            public double Pct;
            public long Ram;
        }

        private readonly MiniGauge[] _gauges = new MiniGauge[3];
        private readonly Timer _timer;
        private readonly Dictionary<int, long> _last = new Dictionary<int, long>();
        private DateTime _lastTick;

        public ProcessGaugePanel()
        {
            Height = 104;
            BackColor = Theme.Surface2;
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Surface2,
                Margin = Padding.Empty,
                Padding = new Padding(4)
            };
            for (int i = 0; i < 3; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
                _gauges[i] = new MiniGauge();
                row.Controls.Add(_gauges[i], i, 0);
            }
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(row);

            _lastTick = DateTime.Now;
            _timer = new Timer { Interval = 2000 };
            _timer.Tick += (s, e) => RefreshProcesses();
            Disposed += (s, e) => _timer.Stop();
            RefreshProcesses();
            _timer.Start();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface2;
            foreach (var gauge in _gauges) gauge.ApplyTheme();
        }

        private void RefreshProcesses()
        {
            DateTime now = DateTime.Now;
            double elapsed = (now - _lastTick).TotalSeconds;
            var list = new List<Proc>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    string name = process.ProcessName;
                    if (name == "Idle" || name == "System" || name == "Registry") continue;
                    long cpu = process.TotalProcessorTime.Ticks;
                    long previous;
                    long delta = 0;
                    if (_last.TryGetValue(process.Id, out previous) && elapsed > 0)
                        delta = cpu - previous;
                    _last[process.Id] = cpu;

                    double pct = elapsed > 0
                        ? (delta / (double)TimeSpan.TicksPerSecond) / elapsed /
                          Math.Max(1, Environment.ProcessorCount) * 100.0
                        : 0;
                    if (pct < 0) pct = 0;
                    if (pct > 100) pct = 100;

                    long ram = 0;
                    try { ram = process.WorkingSet64 / (1024 * 1024); }
                    catch { }

                    list.Add(new Proc { Name = name, Pid = process.Id, Pct = pct, Ram = ram });
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            _lastTick = now;
            var alive = new HashSet<int>();
            foreach (var item in list) alive.Add(item.Pid);
            foreach (var key in new List<int>(_last.Keys))
                if (!alive.Contains(key)) _last.Remove(key);

            list.Sort((a, b) => b.Pct.CompareTo(a.Pct));
            for (int i = 0; i < _gauges.Length; i++)
            {
                if (i < list.Count)
                    _gauges[i].Update(list[i].Name, list[i].Pid, list[i].Pct, list[i].Ram);
                else
                    _gauges[i].Update("-", 0, 0, 0);
            }
        }
    }

    internal sealed class MiniGauge : Control
    {
        private string _name = "-";
        private int _pid;
        private double _pct;
        private long _ram;

        public MiniGauge()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Invalidate();
        }

        public void Update(string name, int pid, double pct, long ram)
        {
            _name = name;
            _pid = pid;
            _pct = pct;
            _ram = ram;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int size = Math.Min(58, Height - 8);
            var rect = new Rectangle(5, (Height - size) / 2, size, size);
            using (var pen = new Pen(Theme.Border, 5))
                g.DrawArc(pen, rect, 135, 270);
            using (var pen = new Pen(Theme.Accent, 5))
                g.DrawArc(pen, rect, 135, (float)(270 * _pct / 100.0));

            double angle = (135 + 270 * _pct / 100.0) * Math.PI / 180.0;
            var center = new PointF(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            var tip = new PointF(center.X + (float)(Math.Cos(angle) * (size / 2f - 8)),
                                 center.Y + (float)(Math.Sin(angle) * (size / 2f - 8)));
            using (var pen = new Pen(Theme.Accent, 2))
                g.DrawLine(pen, center, tip);

            TextRenderer.DrawText(g, ((int)_pct) + "%", Theme.Ui(8f, FontStyle.Bold), rect,
                Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int left = rect.Right + 8;
            int textWidth = Math.Max(10, Width - left - 4);
            var nameRect = new Rectangle(left, rect.Y + 6, textWidth, 18);
            var subRect = new Rectangle(left, rect.Y + 24, textWidth, 16);
            var ramRect = new Rectangle(left, rect.Y + 40, textWidth, 16);
            TextRenderer.DrawText(g, _name, Theme.Ui(8.5f, FontStyle.Bold), nameRect, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _pid > 0 ? "PID " + _pid : "-", Theme.Ui(8f), subRect, Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _pid > 0 ? "RAM " + _ram + " MB" : "", Theme.Ui(8f), ramRect, Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
