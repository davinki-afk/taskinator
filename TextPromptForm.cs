using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    public class TextPromptForm : Form
    {
        private readonly TextBox valueTextBox;
        private readonly PictureBox iconPictureBox;
        private string iconKey;

        public TextPromptForm(string title, string label, string initialValue, string initialIconKey)
        {
            Text = title;
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(390, 153);

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

            Label iconLabel = new Label();
            iconLabel.AutoSize = true;
            iconLabel.Location = new Point(12, 69);
            iconLabel.Text = "Значок:";
            Controls.Add(iconLabel);

            iconPictureBox = new PictureBox();
            iconPictureBox.Location = new Point(70, 64);
            iconPictureBox.Size = new Size(24, 24);
            iconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
            Controls.Add(iconPictureBox);

            Button selectIconButton = new Button();
            selectIconButton.Location = new Point(101, 65);
            selectIconButton.Size = new Size(126, 25);
            selectIconButton.Text = "Выбрать значок...";
            selectIconButton.Click += SelectIconButton_Click;
            Controls.Add(selectIconButton);

            iconKey = String.IsNullOrEmpty(initialIconKey) ? ListIconCatalog.DefaultIconKey : initialIconKey;
            UpdateIconPreview();

            Button okButton = new Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(219, 116);
            okButton.Size = new Size(75, 25);
            okButton.Text = "ОК";
            okButton.Click += OkButton_Click;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(300, 116);
            cancelButton.Size = new Size(75, 25);
            cancelButton.Text = "Отмена";
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
                MessageBox.Show(this, "Введите название.", "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
