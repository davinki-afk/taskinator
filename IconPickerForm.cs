using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    public class IconPickerForm : Form
    {
        private readonly IconGridControl iconsPanel;
        public IconPickerForm(string selectedKey)
        {
            Text = "Выбор значка";
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 360);
            Label prompt = new Label();
            prompt.AutoSize = true;
            prompt.Location = new Point(12, 12);
            prompt.Text = "Выберите значок для списка:";
            Controls.Add(prompt);

            iconsPanel = new IconGridControl(ListIconCatalog.GetOptions());
            iconsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            iconsPanel.Location = new Point(12, 32);
            iconsPanel.Size = new Size(396, 270);
            iconsPanel.SelectedKey = selectedKey;
            iconsPanel.IconDoubleClick += IconGrid_DoubleClick;
            Controls.Add(iconsPanel);

            Button okButton = new Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(252, 320);
            okButton.Size = new Size(75, 25);
            okButton.Text = "ОК";
            okButton.Click += OkButton_Click;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(333, 320);
            cancelButton.Size = new Size(75, 25);
            cancelButton.Text = "Отмена";
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public string SelectedKey
        {
            get
            {
                return iconsPanel.SelectedKey;
            }
        }

        private void IconGrid_DoubleClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(iconsPanel.SelectedKey))
            {
                MessageBox.Show(this, "Выберите значок.", "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            }
        }

        private class IconGridControl : Panel
        {
            private const int TileSize = 32;
            private const int Gap = 12;
            private const int PaddingSize = 8;
            private readonly IList<ListIconOption> options;
            private string selectedKey;

            public IconGridControl(IList<ListIconOption> options)
            {
                this.options = options;
                AutoScroll = true;
                BackColor = SystemColors.Window;
                BorderStyle = BorderStyle.Fixed3D;
                DoubleBuffered = true;
                TabStop = true;
                Cursor = Cursors.Hand;
            }

            public event EventHandler IconDoubleClick;

            public string SelectedKey
            {
                get
                {
                    return String.IsNullOrEmpty(selectedKey) && options.Count > 0
                        ? options[0].Key
                        : selectedKey;
                }
                set
                {
                    selectedKey = value;
                    if (!ContainsKey(selectedKey) && options.Count > 0)
                    {
                        selectedKey = options[0].Key;
                    }
                    Invalidate();
                }
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                UpdateScrollSize();
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                int columns = GetColumnCount();
                e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

                for (int index = 0; index < options.Count; index++)
                {
                    int column = index % columns;
                    int row = index / columns;
                    Rectangle tile = new Rectangle(
                        PaddingSize + column * (TileSize + Gap),
                        PaddingSize + row * (TileSize + Gap),
                        TileSize,
                        TileSize);

                    if (options[index].Key == selectedKey)
                    {
                        using (Brush selectionBrush = new SolidBrush(SystemColors.Highlight))
                        {
                            e.Graphics.FillRectangle(selectionBrush, tile);
                        }
                    }

                    Image image = options[index].Image;
                    int imageX = tile.Left + (tile.Width - image.Width) / 2;
                    int imageY = tile.Top + (tile.Height - image.Height) / 2;
                    e.Graphics.DrawImageUnscaled(image, imageX, imageY);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                int index = GetIndexAt(e.Location);
                if (index >= 0)
                {
                    selectedKey = options[index].Key;
                    Invalidate();
                }
            }

            protected override void OnDoubleClick(EventArgs e)
            {
                base.OnDoubleClick(e);
                if (IconDoubleClick != null)
                {
                    IconDoubleClick(this, e);
                }
            }

            private int GetColumnCount()
            {
                int availableWidth = ClientSize.Width - PaddingSize * 2;
                return Math.Max(1, (availableWidth + Gap) / (TileSize + Gap));
            }

            private int GetIndexAt(Point point)
            {
                Point contentPoint = new Point(
                    point.X - AutoScrollPosition.X,
                    point.Y - AutoScrollPosition.Y);
                int columns = GetColumnCount();
                int column = (contentPoint.X - PaddingSize) / (TileSize + Gap);
                int row = (contentPoint.Y - PaddingSize) / (TileSize + Gap);
                if (column < 0 || row < 0 || column >= columns)
                {
                    return -1;
                }

                int tileX = PaddingSize + column * (TileSize + Gap);
                int tileY = PaddingSize + row * (TileSize + Gap);
                if (contentPoint.X < tileX || contentPoint.Y < tileY ||
                    contentPoint.X >= tileX + TileSize || contentPoint.Y >= tileY + TileSize)
                {
                    return -1;
                }

                int index = row * columns + column;
                return index < options.Count ? index : -1;
            }

            private bool ContainsKey(string key)
            {
                foreach (ListIconOption option in options)
                {
                    if (option.Key == key)
                    {
                        return true;
                    }
                }
                return false;
            }

            private void UpdateScrollSize()
            {
                int columns = GetColumnCount();
                int rows = (options.Count + columns - 1) / columns;
                AutoScrollMinSize = new Size(
                    PaddingSize * 2 + columns * TileSize + Math.Max(0, columns - 1) * Gap,
                    PaddingSize * 2 + rows * TileSize + Math.Max(0, rows - 1) * Gap);
            }
        }
    }
}
