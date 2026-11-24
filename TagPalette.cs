using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    internal static class TagPalette
    {
        public const int DefaultIndex = 12;
        private const int SwatchSize = 10;
        internal const int TextInset = SwatchSize + 5;
        private const TextFormatFlags LabelFlags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static readonly Color[] Colors =
        {
            Color.Black, Color.Maroon, Color.Green, Color.Olive,
            Color.Navy, Color.Purple, Color.Teal, Color.Silver,
            Color.Gray, Color.Red, Color.Lime, Color.Yellow,
            Color.Blue, Color.Fuchsia, Color.Aqua, Color.White
        };

        private static readonly TextId[] ColorNameKeys =
        {
            TextId.Black, TextId.Maroon, TextId.Green, TextId.Olive,
            TextId.Navy, TextId.Purple, TextId.Teal, TextId.Silver,
            TextId.Gray, TextId.Red, TextId.Lime, TextId.Yellow,
            TextId.Blue, TextId.Magenta, TextId.Aqua, TextId.White
        };

        public static string[] GetColorNames()
        {
            return Array.ConvertAll(ColorNameKeys, L10n.Get);
        }

        public static string GetColorName(int index)
        {
            return L10n.Get(ColorNameKeys[index]);
        }

        public static int ValidIndex(int index)
        {
            return HasColor(index) ? index : DefaultIndex;
        }

        public static bool HasColor(int index)
        {
            return index >= 0 && index < Colors.Length;
        }

        public static Rectangle DrawSwatch(Graphics graphics, Rectangle bounds, int index)
        {
            Rectangle swatch = new Rectangle(bounds.Left, bounds.Top + (bounds.Height - SwatchSize) / 2, SwatchSize, SwatchSize);
            using (SolidBrush brush = new SolidBrush(Colors[ValidIndex(index)]))
            {
                graphics.FillRectangle(brush, swatch);
            }
            graphics.DrawRectangle(SystemPens.ControlDarkDark, swatch.Left, swatch.Top, SwatchSize - 1, SwatchSize - 1);
            return Rectangle.FromLTRB(bounds.Left + TextInset, bounds.Top, bounds.Right, bounds.Bottom);
        }

        public static int MeasureTag(Graphics graphics, TodoTag tag, Font font)
        {
            return TextInset + TextRenderer.MeasureText(graphics, tag.ToString(), font,
                new Size(int.MaxValue, int.MaxValue), LabelFlags).Width;
        }

        public static void DrawTag(Graphics graphics, TodoTag tag, Font font, Rectangle bounds,
            Color textColor, Color background, bool useTagColor = true)
        {
            Rectangle textBounds = DrawSwatch(graphics, bounds, tag.ColorIndex);
            DrawColoredText(graphics, tag.ToString(), font, textBounds,
                useTagColor ? ValidIndex(tag.ColorIndex) : -1, textColor, background, LabelFlags,
                useBlackBackgroundForBrightColors: true);
        }

        public static void DrawColoredText(Graphics graphics, string text, Font font, Rectangle bounds,
            int index, Color defaultColor, Color background, TextFormatFlags flags, bool useBlackBackgroundForBrightColors = false)
        {
            if (!HasColor(index) || SystemInformation.HighContrast)
            {
                TextRenderer.DrawText(graphics, text, font, bounds, defaultColor, flags);
                return;
            }

            Color color = Colors[index];
            if ((useBlackBackgroundForBrightColors && (color == Color.Lime || color == Color.Yellow || color == Color.Aqua)) ||
                (color == Color.White && 1.05 / (GetLuminance(background) + 0.05) < 4.5))
            {
                TextRenderer.DrawText(graphics, text, font, bounds, color, Color.Black, flags);
            }
            else
            {
                TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
            }
        }

        private static double GetLuminance(Color color)
        {
            return 0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);
        }

        private static double Linearize(byte component)
        {
            double value = component / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }
}
