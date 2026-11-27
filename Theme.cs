using System;
using System.Drawing;
using System.Windows.Forms;

namespace VaultForge
{
    internal static class Theme
    {
        public static bool Dark { get; private set; }

        public static Color Bg { get { return Dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control; } }
        public static Color Surface { get { return Dark ? Color.FromArgb(45, 45, 45) : Color.White; } }
        public static Color Surface2 { get { return Dark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(240, 240, 240); } }
        public static Color Border { get { return Dark ? Color.FromArgb(82, 82, 82) : Color.FromArgb(208, 208, 208); } }
        public static Color Text { get { return Dark ? Color.FromArgb(235, 235, 235) : Color.Black; } }
        public static Color Muted { get { return Dark ? Color.FromArgb(165, 165, 165) : Color.FromArgb(89, 89, 89); } }
        public static Color Accent { get { return Dark ? Color.FromArgb(86, 166, 245) : Color.FromArgb(0, 120, 215); } }
        public static Color AccentDark { get { return Dark ? Color.FromArgb(58, 130, 205) : Color.FromArgb(0, 99, 177); } }
        public static Color Purple { get { return Accent; } }
        public static Color Danger { get { return Dark ? Color.FromArgb(255, 110, 110) : Color.FromArgb(232, 17, 35); } }
        public static Color Warning { get { return Dark ? Color.FromArgb(240, 175, 70) : Color.FromArgb(204, 102, 0); } }
        public static Color ButtonFace { get { return Dark ? Color.FromArgb(62, 62, 62) : Color.FromArgb(204, 204, 204); } }
        public static Color BannerBg { get { return Dark ? Color.FromArgb(26, 55, 76) : Color.FromArgb(222, 236, 249); } }
        public static Color BannerText { get { return Dark ? Color.FromArgb(190, 222, 247) : Color.FromArgb(12, 59, 94); } }

        public static Font Ui(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static Font Mono(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Consolas", size, style, GraphicsUnit.Point);
        }

        public static void SetDark(bool dark)
        {
            Dark = dark;
        }

        public static void StyleTextBox(TextBox t)
        {
            t.BackColor = Dark ? Color.FromArgb(51, 51, 51) : Color.White;
            t.ForeColor = Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = Ui(9f);
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Dark ? Color.FromArgb(40, 40, 40) : Color.White;
            grid.GridColor = Border;
            grid.DefaultCellStyle.BackColor = Dark ? Color.FromArgb(45, 45, 45) : Color.White;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Dark ? Color.FromArgb(38, 79, 120) : Color.FromArgb(204, 232, 255);
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface2;
        }

        public static void StyleMenu(MenuStrip menu)
        {
            menu.BackColor = Surface2;
            menu.ForeColor = Text;
        }

        public static void Apply(Control root)
        {
            if (root is Form)
            {
                root.BackColor = Bg;
                root.ForeColor = Text;
            }
            else if (root is DataGridView)
            {
                StyleGrid((DataGridView)root);
            }
            else if (root is MenuStrip)
            {
                StyleMenu((MenuStrip)root);
            }
            else if (root is TextBox || root is ComboBox)
            {
                var text = root as TextBox;
                if (text != null) StyleTextBox(text);
                else
                {
                    root.BackColor = Dark ? Color.FromArgb(51, 51, 51) : Color.White;
                    root.ForeColor = Text;
                }
            }
            else if (root is Panel || root is GroupBox || root is TableLayoutPanel || root is FlowLayoutPanel)
            {
                root.BackColor = Bg;
            }
            else if (root is Label)
            {
                root.ForeColor = ((string)root.Tag == "muted") ? Muted : Text;
            }
            else if (root is FlatButton)
            {
                root.Invalidate();
            }

            foreach (Control child in root.Controls) Apply(child);
            root.Invalidate(true);
        }
    }

    // Native Windows button in light mode; owner-painted dark variant when enabled.
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
            TextColor = Color.Black;
            Radius = 2;
            FlatStyle = FlatStyle.System;
            UseVisualStyleBackColor = true;
            Font = Theme.Ui(9f);
            Height = 26;
            TabStop = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Theme.Dark)
            {
                base.OnPaint(e);
                return;
            }
            Color fill = _down ? Color.FromArgb(48, 92, 140)
                               : (_hover ? Color.FromArgb(72, 72, 72) : Theme.ButtonFace);
            using (var brush = new SolidBrush(fill))
                e.Graphics.FillRectangle(brush, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(96, 96, 96)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            BackColor = SystemColors.Control;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(10);
        }
    }
}
