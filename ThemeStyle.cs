using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Taskinator
{
    internal sealed class ThemeStyle
    {
        internal const string DefaultKey = "office-blue";
        internal const int CompositedWindowStyle = 0x02000000; // WS_EX_COMPOSITED
        private const int DefaultIndex = 1;

        internal static readonly Color Selection = Color.FromArgb(255, 224, 157);
        internal static readonly Color SelectionBorder = Color.FromArgb(178, 121, 42);

        private static readonly ThemeStyle[] Themes =
        {
            new ThemeStyle("system", TextId.ThemeSystem, 0, 0, 0, 0, 0, 0),
            new ThemeStyle(DefaultKey, TextId.ThemeOfficeBlue,
                0xDEE8F5, 0xF5F9FF, 0xB6CDEA, 0x7B9BC5, 0x233B5D, 0xEDF4FC),
            new ThemeStyle("office-silver", TextId.ThemeOfficeSilver,
                0xE8E7EF, 0xFBFAFD, 0xC9C8DA, 0x918DA8, 0x38354B, 0xF4F3F8),
            new ThemeStyle("office-olive", TextId.ThemeOfficeOlive,
                0xE7EAD9, 0xF8FAF0, 0xC6D1AB, 0x879669, 0x384629, 0xF2F5E9)
        };

        private static ThemeStyle selected = Themes[DefaultIndex];

        private ThemeStyle(string key, TextId name, int surface, int light, int shade, int border, int ink, int button)
        {
            Key = key;
            Name = name;
            Surface = FromRgb(surface);
            Light = FromRgb(light);
            Shade = FromRgb(shade);
            Border = FromRgb(border);
            Ink = FromRgb(ink);
            ButtonFace = FromRgb(button);
        }

        private readonly string Key;
        private readonly TextId Name;
        internal readonly Color Surface;
        internal readonly Color Light;
        internal readonly Color Shade;
        internal readonly Color Border;
        internal readonly Color Ink;
        private readonly Color ButtonFace;

        internal static ThemeStyle Current
        {
            get { return selected == Themes[0] || SystemInformation.HighContrast ? null : selected; }
        }

        internal static int GetIndex(string key)
        {
            int index = Array.FindIndex(Themes, theme => theme.Key == key);
            return index < 0 ? DefaultIndex : index;
        }

        internal static string GetKey(int index)
        {
            return Themes[index].Key;
        }

        internal static string[] GetNames()
        {
            return Array.ConvertAll(Themes, theme => L10n.Get(theme.Name));
        }

        internal static void Select(string key)
        {
            selected = Themes[GetIndex(key)];
        }

        internal static void StyleDialog(Form form)
        {
            form.ShowIcon = false;
            form.Load += delegate { ApplyTo(form); };
            form.SystemColorsChanged += delegate { ApplyTo(form); };
        }

        internal static void ApplyTo(Form form)
        {
            ThemeStyle theme = Current;
            form.BackColor = theme == null ? SystemColors.Control : theme.Surface;
            form.ForeColor = theme == null ? SystemColors.ControlText : theme.Ink;
            StyleButtons(form, theme);
            form.Invalidate(true);
        }

        private static void StyleButtons(Control parent, ThemeStyle theme)
        {
            foreach (Control child in parent.Controls)
            {
                Button button = child as Button;
                if (button != null)
                {
                    button.BackColor = theme == null ? Color.Empty : theme.ButtonFace;
                    button.UseVisualStyleBackColor = theme == null;
                    button.FlatStyle = theme == null ? FlatStyle.Standard : FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = theme == null ? Color.Empty : theme.Border;
                    button.FlatAppearance.MouseOverBackColor = theme == null ? Color.Empty : Selection;
                    button.FlatAppearance.MouseDownBackColor = theme == null ? Color.Empty : Color.FromArgb(247, 196, 110);
                }

                if (child.HasChildren)
                {
                    StyleButtons(child, theme);
                }
            }
        }

        internal static void PaintGradient(Graphics graphics, Rectangle bounds, Color start, Color end)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            if (!Win32xxTheme.TryPaint(graphics, bounds, start, end))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(bounds, start, end, LinearGradientMode.Vertical))
                {
                    graphics.FillRectangle(brush, bounds);
                }
            }
        }

        internal static void PaintSelection(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (SolidBrush brush = new SolidBrush(Selection))
            using (Pen pen = new Pen(SelectionBorder))
            {
                graphics.FillRectangle(brush, bounds);
                graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }
        }

        internal static void PaintHeader(Graphics graphics, Rectangle bounds, ThemeStyle theme)
        {
            using (SolidBrush brush = new SolidBrush(theme.Surface))
            {
                graphics.FillRectangle(brush, bounds);
            }
            PaintBottomBorder(graphics, bounds, theme.Border);
        }

        internal static void PaintBottomBorder(Graphics graphics, Rectangle bounds, Color border)
        {
            using (Pen pen = new Pen(border))
            {
                graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
            }
        }

        internal static void DrawRowFocus(DrawItemEventArgs e, bool themedSelection, Color textColor)
        {
            if (!themedSelection)
            {
                e.DrawFocusRectangle();
            }
            else if ((e.State & (DrawItemState.Focus | DrawItemState.NoFocusRect)) == DrawItemState.Focus)
            {
                Rectangle bounds = Rectangle.Inflate(e.Bounds, -2, -2);
                ControlPaint.DrawFocusRectangle(e.Graphics, bounds, textColor, Selection);
            }
        }

        private static Color FromRgb(int rgb)
        {
            return Color.FromArgb(unchecked((int)0xff000000) | rgb);
        }
    }

    internal sealed class ThemeFrame : Panel
    {
        internal const int BorderWidth = 1;
        private readonly Control content;
        private Size borderSize;

        public ThemeFrame(Control content)
        {
            this.content = content;
            Bounds = content.Bounds;
            Padding = new Padding(BorderWidth);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            content.Dock = DockStyle.Fill;
            Controls.Add(content);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            Size previous = borderSize;
            Size current = ClientSize;
            borderSize = current;
            base.OnSizeChanged(e);

            if (current.Width != previous.Width)
            {
                Invalidate(new Rectangle(previous.Width - BorderWidth, 0, BorderWidth, current.Height));
                Invalidate(new Rectangle(current.Width - BorderWidth, 0, BorderWidth, current.Height));
            }
            if (current.Height != previous.Height)
            {
                Invalidate(new Rectangle(0, previous.Height - BorderWidth, current.Width, BorderWidth));
                Invalidate(new Rectangle(0, current.Height - BorderWidth, current.Width, BorderWidth));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!content.Visible)
            {
                base.OnPaintBackground(e);
            }
            ThemeStyle theme = ThemeStyle.Current;
            Color border = theme == null ? SystemColors.WindowFrame : theme.Border;
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, border, ButtonBorderStyle.Solid);
        }
    }

    internal sealed class ThemeHeader : Panel
    {
        public ThemeHeader()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            ThemeStyle theme = ThemeStyle.Current;
            if (theme == null)
            {
                base.OnPaintBackground(e);
                return;
            }

            ThemeStyle.PaintHeader(e.Graphics, ClientRectangle, theme);
        }
    }

    internal sealed class ThemeToolbarRenderer : ToolStripSystemRenderer, IDisposable
    {
        private ThemeStyle gradientTheme;
        private Bitmap gradientStrip;
        private TextureBrush gradientBrush;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            ThemeStyle theme = ThemeStyle.Current;
            if (theme == null)
            {
                base.OnRenderToolStripBackground(e);
            }
            else if (e.ToolStrip.ClientSize.Width > 0 && e.ToolStrip.ClientSize.Height > 0)
            {
                Rectangle bounds = e.ToolStrip.ClientRectangle;
                if (gradientBrush == null || gradientTheme != theme || gradientStrip.Height != bounds.Height)
                {
                    Dispose();
                    gradientStrip = new Bitmap(1, bounds.Height, PixelFormat.Format24bppRgb);
                    using (Graphics graphics = Graphics.FromImage(gradientStrip))
                    {
                        ThemeStyle.PaintGradient(graphics, new Rectangle(0, 0, 1, bounds.Height), theme.Light, theme.Shade);
                    }
                    gradientBrush = new TextureBrush(gradientStrip, WrapMode.Tile);
                    gradientTheme = theme;
                }
                e.Graphics.FillRectangle(gradientBrush, Rectangle.Intersect(bounds, e.AffectedBounds));
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            ThemeStyle theme = ThemeStyle.Current;
            if (theme == null)
            {
                base.OnRenderToolStripBorder(e);
                return;
            }

            ThemeStyle.PaintBottomBorder(e.Graphics, e.ToolStrip.ClientRectangle, theme.Border);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (ThemeStyle.Current == null)
            {
                base.OnRenderButtonBackground(e);
                return;
            }

            PaintActiveItem(e);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (ThemeStyle.Current == null)
            {
                base.OnRenderDropDownButtonBackground(e);
                return;
            }

            PaintActiveItem(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            ThemeStyle theme = ThemeStyle.Current;
            if (theme != null && e.Item.Enabled && e.Item is ToolStripDropDownButton)
            {
                e.ArrowColor = theme.Ink;
            }
            base.OnRenderArrow(e);
        }

        private static void PaintActiveItem(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                ThemeStyle.PaintSelection(e.Graphics, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        public void Dispose()
        {
            if (gradientBrush != null)
            {
                gradientBrush.Dispose();
                gradientBrush = null;
            }
            if (gradientStrip != null)
            {
                gradientStrip.Dispose();
                gradientStrip = null;
            }
            gradientTheme = null;
        }
    }
}
