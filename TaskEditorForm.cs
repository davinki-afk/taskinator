using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    public class TaskEditorForm : Form
    {
        private readonly TextBox titleTextBox;
        private readonly TextBox descriptionTextBox;
        private readonly CheckBox completedCheckBox;

        public TaskEditorForm(TodoItem item)
        {
            Text = item == null ? "Новая задача" : "Изменить задачу";
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(390, 310);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 350);

            Label titleLabel = new Label();
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(12, 14);
            titleLabel.Text = "Задача:";
            Controls.Add(titleLabel);

            titleTextBox = new TextBox();
            titleTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleTextBox.Location = new Point(15, 33);
            titleTextBox.Size = new Size(440, 21);
            titleTextBox.Text = item == null ? String.Empty : item.Title;
            Controls.Add(titleTextBox);

            Label descriptionLabel = new Label();
            descriptionLabel.AutoSize = true;
            descriptionLabel.Location = new Point(12, 66);
            descriptionLabel.Text = "Описание:";
            Controls.Add(descriptionLabel);

            descriptionTextBox = new TextBox();
            descriptionTextBox.AcceptsReturn = true;
            descriptionTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            descriptionTextBox.Location = new Point(15, 85);
            descriptionTextBox.Multiline = true;
            descriptionTextBox.ScrollBars = ScrollBars.Vertical;
            descriptionTextBox.Size = new Size(440, 180);
            descriptionTextBox.Text = item == null ? String.Empty : item.Description;
            Controls.Add(descriptionTextBox);

            completedCheckBox = new CheckBox();
            completedCheckBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            completedCheckBox.AutoSize = true;
            completedCheckBox.Location = new Point(15, 278);
            completedCheckBox.Text = "Задача выполнена";
            completedCheckBox.Checked = item != null && item.IsComplete;
            Controls.Add(completedCheckBox);

            Button okButton = new Button();
            okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(299, 314);
            okButton.Size = new Size(75, 25);
            okButton.Text = "ОК";
            okButton.Click += OkButton_Click;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(380, 314);
            cancelButton.Size = new Size(75, 25);
            cancelButton.Text = "Отмена";
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public string TaskTitle
        {
            get { return titleTextBox.Text.Trim(); }
        }

        public string Description
        {
            get { return descriptionTextBox.Text; }
        }

        public bool IsComplete
        {
            get { return completedCheckBox.Checked; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            titleTextBox.Focus();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (TaskTitle.Length == 0)
            {
                MessageBox.Show(this, "Введите название задачи.", "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            }
        }
    }
}
