using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VaultForge
{
    /// <summary>Small status lamp used next to Connect/Disconnect.</summary>
    internal sealed class StatusLamp : Control
    {
        private Color _lampColor = Color.FromArgb(150, 150, 150);

        public Color LampColor
        {
            get { return _lampColor; }
            set { _lampColor = value; Invalidate(); }
        }

        public StatusLamp()
        {
            Width = 16;
            Height = 16;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(2, 2, Width - 5, Height - 5);
            using (var glow = new SolidBrush(Color.FromArgb(45, _lampColor)))
                e.Graphics.FillEllipse(glow, rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);
            using (var fill = new SolidBrush(_lampColor))
                e.Graphics.FillEllipse(fill, rect);
            using (var pen = new Pen(Color.FromArgb(130, 0, 0, 0)))
                e.Graphics.DrawEllipse(pen, rect);
        }
    }
}
