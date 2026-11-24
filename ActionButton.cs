using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    internal enum ActionGlyph
    {
        Plus,
        Pencil,
        Cross,
        Eraser
    }

    internal sealed class ActionButton : Button
    {
        public ActionButton(ActionGlyph glyph)
        {
            Size = new Size(32, 26);
            UseVisualStyleBackColor = true;
            ImageAlign = ContentAlignment.MiddleCenter;

            switch (glyph)
            {
                case ActionGlyph.Plus:
                    Image = ListIconCatalog.GetImage("103.png");
                    break;
                case ActionGlyph.Pencil:
                    Image = ListIconCatalog.GetImage("018.png");
                    break;
                case ActionGlyph.Cross:
                    Image = ListIconCatalog.GetImage("101.png");
                    break;
                case ActionGlyph.Eraser:
                    Image = ListIconCatalog.GetImage("eraser.png");
                    break;
            }
        }
    }
}
