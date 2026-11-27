using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VaultForge
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(11, 17, 32);
        public static readonly Color Surface = Color.FromArgb(17, 24, 39);
        public static readonly Color Surface2 = Color.FromArgb(30, 41, 59);
        public static readonly Color Border = Color.FromArgb(36, 50, 68);
        public static readonly Color Text = Color.FromArgb(226, 232, 240);
        public static readonly Color Muted = Color.FromArgb(148, 163, 184);
        public static readonly Color Accent = Color.FromArgb(16, 185, 129);
        public static readonly Color AccentDark = Color.FromArgb(5, 150, 105);
        public static readonly Color Purple = Color.FromArgb(124, 58, 237);
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);

        public static Font Ui(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static Font Mono(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Consolas", size, style, GraphicsUnit.Point);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void StyleTextBox(TextBox t)
        {
            t.BackColor = Color.FromArgb(15, 23, 42);
            t.ForeColor = Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = Ui(10f);
        }
    }

    internal sealed class FlatButton : Button
    {
        private bool _hover;
        private bool _down;
        public Color FillColor { get; set; }
        public Color HoverColor { get; set; }
        public Color TextColor { get; set; }
        public int Radius { get; set; }

        public FlatButton(string text, Color fill, Color hover)
        {
            Text = text;
            FillColor = fill;
            HoverColor = hover;
            TextColor = Color.White;
            Radius = 7;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Ui(9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Height = 34;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = _down ? ControlPaint.Dark(HoverColor, 0.08f) : (_hover ? HoverColor : FillColor);
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (var pen = new Pen(Theme.Border))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
