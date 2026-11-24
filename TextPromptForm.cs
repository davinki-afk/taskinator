using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    public class TextPromptForm : Form
    {
        private readonly TextBox valueTextBox;
        private readonly TextBox descriptionTextBox;
        private readonly PictureBox iconPictureBox;
        private string iconKey;

        public TextPromptForm(string title, string label, string initialValue, string initialIconKey)
            : this(title, label, initialValue, initialIconKey, null)
        {
        }

        internal TextPromptForm(string title, TodoSubtask subtask)
            : this(title, L10n.Get(TextId.SubtaskTitle), subtask.Title, null, subtask.Description ?? String.Empty)
        {
        }

        private TextPromptForm(string title, string label, string initialValue, string initialIconKey,
            string initialDescription)
        {
            ThemeStyle.StyleDialog(this);
            Text = title;
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            int buttonsTop = initialDescription == null ? 116 : 170;
            ClientSize = new Size(390, buttonsTop + 37);

            Label captionLabel = new Label();
            captionLabel.AutoSize = true;
            captionLabel.Location = new Point(12, 13);
            captionLabel.Text = label;
            Controls.Add(captionLabel);

            valueTextBox = new TextBox();
            valueTextBox.Location = new Point(15, 33);
            valueTextBox.Size = new Size(360, 21);
            valueTextBox.Text = initialValue ?? String.Empty;
            Controls.Add(valueTextBox);

            if (initialDescription == null)
            {
                Label iconLabel = new Label();
                iconLabel.AutoSize = true;
                iconLabel.Location = new Point(12, 69);
                iconLabel.Text = L10n.Get(TextId.Icon);
                Controls.Add(iconLabel);

                iconPictureBox = new PictureBox();
                iconPictureBox.Location = new Point(iconLabel.Right + 8, 64);
                iconPictureBox.Size = new Size(24, 24);
                iconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
                Controls.Add(iconPictureBox);

                Button selectIconButton = new Button();
                selectIconButton.Location = new Point(iconPictureBox.Right + 7, 65);
                selectIconButton.Size = new Size(126, 25);
                selectIconButton.Text = L10n.Get(TextId.ChooseIcon);
                selectIconButton.Click += SelectIconButton_Click;
                Controls.Add(selectIconButton);

                iconKey = String.IsNullOrEmpty(initialIconKey) ? ListIconCatalog.DefaultIconKey : initialIconKey;
                UpdateIconPreview();
            }
            else
            {
                Controls.Add(new Label
                {
                    AutoSize = true,
                    Location = new Point(12, 63),
                    Text = L10n.Get(TextId.Description)
                });
                descriptionTextBox = new TextBox
                {
                    AcceptsReturn = true,
                    Location = new Point(15, 82),
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Size = new Size(360, 77),
                    Text = initialDescription
                };
                Controls.Add(descriptionTextBox);
            }

            Button okButton = new Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(219, buttonsTop);
            okButton.Size = new Size(75, 25);
            okButton.Text = L10n.Get(TextId.OK);
            okButton.Click += OkButton_Click;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(300, buttonsTop);
            cancelButton.Size = new Size(75, 25);
            cancelButton.Text = L10n.Get(TextId.Cancel);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public string Value
        {
            get { return valueTextBox.Text.Trim(); }
        }

        public string IconKey
        {
            get { return iconKey; }
        }

        internal string Description
        {
            get { return String.IsNullOrWhiteSpace(descriptionTextBox.Text) ? null : descriptionTextBox.Text; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            valueTextBox.Focus();
            valueTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (Value.Length == 0)
            {
                MessageBox.Show(this, L10n.Get(TextId.NameRequired), "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            }
        }

        private void SelectIconButton_Click(object sender, EventArgs e)
        {
            using (IconPickerForm dialog = new IconPickerForm(iconKey))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    iconKey = dialog.SelectedKey;
                    UpdateIconPreview();
                }
            }
        }

        private void UpdateIconPreview()
        {
            iconPictureBox.Image = ListIconCatalog.GetImage(iconKey);
        }
    }
}
