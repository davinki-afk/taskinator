using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    internal sealed class TagEditorForm : Form
    {
        private readonly TextBox nameTextBox;
        private int colorIndex;

        public TagEditorForm(TodoTag tag)
        {
            ThemeStyle.StyleDialog(this);
            Text = L10n.Get(tag == null ? TextId.NewTag : TextId.EditTag);
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(310, 186);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            Controls.Add(new Label { Text = L10n.Get(TextId.Name), Location = new Point(12, 12), AutoSize = true });
            nameTextBox = new TextBox();
            nameTextBox.Location = new Point(12, 30);
            nameTextBox.Width = 286;
            nameTextBox.Text = tag == null ? String.Empty : tag.Name;
            Controls.Add(nameTextBox);

            Controls.Add(new Label { Text = L10n.Get(TextId.Color), Location = new Point(12, 60), AutoSize = true });
            Panel colorsPanel = new Panel { Location = new Point(12, 79), Size = new Size(286, 64) };
            colorIndex = tag == null ? TagPalette.DefaultIndex : TagPalette.ValidIndex(tag.ColorIndex);
            for (int i = 0; i < TagPalette.Colors.Length; i++)
            {
                Color color = TagPalette.Colors[i];
                RadioButton swatch = new RadioButton();
                swatch.Appearance = Appearance.Button;
                swatch.FlatStyle = FlatStyle.Flat;
                swatch.Location = new Point(i % 8 * 36, i / 8 * 34);
                swatch.Size = new Size(32, 29);
                swatch.TextAlign = ContentAlignment.MiddleCenter;
                swatch.AccessibleName = TagPalette.GetColorName(i);
                swatch.Tag = i;
                swatch.UseVisualStyleBackColor = false;
                swatch.BackColor = color;
                int brightness = color.R * 299 + color.G * 587 + color.B * 114;
                swatch.ForeColor = brightness >= 128000 ? Color.Black : Color.White;
                swatch.FlatAppearance.BorderColor = SystemColors.ControlDarkDark;
                swatch.FlatAppearance.CheckedBackColor = color;
                swatch.FlatAppearance.MouseDownBackColor = color;
                swatch.FlatAppearance.MouseOverBackColor = color;
                swatch.CheckedChanged += Color_CheckedChanged;
                colorsPanel.Controls.Add(swatch);
                swatch.TabStop = i == colorIndex;
                swatch.Checked = i == colorIndex;
            }
            Controls.Add(colorsPanel);

            Button okButton = new Button { Text = L10n.Get(TextId.OK), Location = new Point(137, 150), Size = new Size(78, 25) };
            okButton.DialogResult = DialogResult.OK;
            okButton.Click += OkButton_Click;
            Controls.Add(okButton);
            Button cancelButton = new Button { Text = L10n.Get(TextId.Cancel), Location = new Point(220, 150), Size = new Size(78, 25) };
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
            ActiveControl = nameTextBox;
        }

        public string TagName
        {
            get { return nameTextBox.Text.Trim(); }
        }

        public int ColorIndex
        {
            get { return colorIndex; }
        }

        private void Color_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton swatch = (RadioButton)sender;
            swatch.Text = swatch.Checked ? "•" : String.Empty;
            swatch.FlatAppearance.BorderSize = swatch.Checked ? 2 : 1;
            swatch.FlatAppearance.BorderColor = swatch.Checked ? swatch.ForeColor : SystemColors.ControlDarkDark;
            if (swatch.Checked)
            {
                colorIndex = (int)swatch.Tag;
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (TagName.Length == 0)
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, L10n.Get(TextId.TagNameRequired), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                nameTextBox.Focus();
            }
        }
    }
}
