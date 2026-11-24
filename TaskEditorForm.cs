using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Taskinator
{
    public class TaskEditorForm : Form
    {
        private const int SubtaskListTop = 16;
        private const int SubtaskButtonHeight = 24;
        private const int SubtaskButtonGap = 4;
        private const int SubtaskSectionHeight = SubtaskListTop + 3 * SubtaskButtonHeight + 2 * SubtaskButtonGap;
        private readonly TextBox titleTextBox;
        private readonly ComboBox titleColorComboBox;
        private readonly TextBox descriptionTextBox;
        private readonly CheckedListBox subtasksListBox;
        private readonly Panel tagsPanel;
        private readonly TaskReminderEditor reminderEditor;
        private readonly CheckBox completedCheckBox;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= ThemeStyle.CompositedWindowStyle;
                return parameters;
            }
        }

        public TaskEditorForm(TodoItem item)
            : this(item, new TodoTag[0])
        {
        }

        public TaskEditorForm(TodoItem item, IEnumerable<TodoTag> tags)
        {
            const int margin = 8;
            const int clientWidth = 350;
            const int contentWidth = clientWidth - 2 * margin;
            const int colorWidth = 124;
            const int subtasksTop = 114;
            const int tagsTop = subtasksTop + SubtaskSectionHeight + 20;
            const int tagRows = 4;
            const int tagPadding = 2;
            const int tagBorder = ThemeFrame.BorderWidth;
            const int buttonHeight = 24;

            ThemeStyle.StyleDialog(this);
            Text = L10n.Get(item == null ? TextId.NewTask : TextId.EditTask);
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            tagsPanel = new Panel
            {
                AccessibleName = L10n.Get(TextId.Tags),
                AutoScroll = true,
                BackColor = SystemColors.Window
            };
            ToolTip tagToolTip = new ToolTip();
            Disposed += delegate { tagToolTip.Dispose(); };
            tagsPanel.SuspendLayout();
            foreach (TodoTag tag in tags)
            {
                int index = tagsPanel.Controls.Count;
                TagEntry entry = new TagEntry(tag)
                {
                    Location = new Point(tagPadding + index / tagRows * TagEntry.ColumnWidth,
                        tagPadding + index % tagRows * TagEntry.RowHeight),
                    Checked = item != null && item.TagIds.Contains(tag.Id)
                };
                tagsPanel.Controls.Add(entry);
                tagToolTip.SetToolTip(entry, entry.Text);
            }
            int columnCount = (tagsPanel.Controls.Count + tagRows - 1) / tagRows;
            int tagContentWidth = columnCount * TagEntry.ColumnWidth + 2 * tagPadding;
            int tagsHeight = tagRows * TagEntry.RowHeight + 2 * (tagPadding + tagBorder);
            if (tagContentWidth > contentWidth - 2 * tagBorder)
            {
                tagsHeight += SystemInformation.HorizontalScrollBarHeight;
            }
            bool hasTags = tagsPanel.Controls.Count > 0;
            ThemeFrame tagsFrame = new ThemeFrame(tagsPanel)
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(margin, tagsTop),
                Size = new Size(contentWidth, tagsHeight),
                Visible = hasTags
            };
            tagsPanel.AutoScrollMinSize = new Size(tagContentWidth, 0);
            tagsPanel.ResumeLayout(false);
            int reminderTop = hasTags ? tagsFrame.Bottom + 6 : tagsTop + 2;
            reminderEditor = new TaskReminderEditor(item);
            reminderEditor.Location = new Point(margin, reminderTop);
            reminderEditor.Width = contentWidth;

            int footerTop = reminderEditor.Bottom + 6;
            ClientSize = new Size(clientWidth, footerTop + buttonHeight + margin);
            MinimumSize = SizeFromClientSize(ClientSize);

            Label titleLabel = new Label();
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(margin, 7);
            titleLabel.Text = L10n.Get(TextId.Task);
            Controls.Add(titleLabel);

            titleTextBox = new TextBox();
            titleTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleTextBox.Location = new Point(margin, 22);
            titleTextBox.Width = contentWidth - colorWidth - 6;
            titleTextBox.Text = item == null ? String.Empty : item.Title;
            Controls.Add(titleTextBox);

            Label titleColorLabel = new Label();
            titleColorLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            titleColorLabel.AutoSize = true;
            titleColorLabel.Text = L10n.Get(TextId.TitleColor);
            titleColorLabel.Location = new Point(clientWidth - margin - colorWidth, 7);
            Controls.Add(titleColorLabel);

            titleColorComboBox = new ComboBox();
            titleColorComboBox.AccessibleName = L10n.Get(TextId.TitleColor);
            titleColorComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            titleColorComboBox.DrawMode = DrawMode.OwnerDrawFixed;
            titleColorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            titleColorComboBox.ItemHeight = 16;
            titleColorComboBox.Location = new Point(titleColorLabel.Left, 22);
            titleColorComboBox.Width = colorWidth;
            titleColorComboBox.DrawItem += TitleColorComboBox_DrawItem;
            titleColorComboBox.Items.Add(L10n.Get(TextId.Default));
            titleColorComboBox.Items.AddRange(TagPalette.GetColorNames());
            titleColorComboBox.SelectedIndex = item != null && TagPalette.HasColor(item.TitleColorIndex)
                ? item.TitleColorIndex + 1 : 0;
            Controls.Add(titleColorComboBox);

            Label descriptionLabel = new Label();
            descriptionLabel.AutoSize = true;
            descriptionLabel.Location = new Point(margin, 49);
            descriptionLabel.Text = L10n.Get(TextId.Description);
            Controls.Add(descriptionLabel);

            descriptionTextBox = new TextBox();
            descriptionTextBox.AcceptsReturn = true;
            descriptionTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            descriptionTextBox.Location = new Point(margin, 64);
            descriptionTextBox.Multiline = true;
            descriptionTextBox.ScrollBars = ScrollBars.Vertical;
            descriptionTextBox.Size = new Size(contentWidth, 45);
            descriptionTextBox.Text = item == null ? String.Empty : item.Description;
            Controls.Add(descriptionTextBox);

            subtasksListBox = new SubtaskListBox
            {
                AccessibleName = L10n.Get(TextId.Subtasks),
                DisplayMember = "Title",
                CheckOnClick = true,
                HorizontalScrollbar = true,
                IntegralHeight = false,
                BorderStyle = BorderStyle.None
            };
            if (item != null)
            {
                foreach (TodoSubtask subtask in item.Subtasks)
                {
                    subtasksListBox.Items.Add(subtask, subtask.IsComplete);
                }
            }
            Controls.Add(CreateSubtasksPanel(new Rectangle(margin, subtasksTop, contentWidth, SubtaskSectionHeight)));

            Label tagLabel = new Label();
            tagLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            tagLabel.AutoSize = true;
            tagLabel.Location = new Point(margin, tagsTop - 15);
            tagLabel.Text = L10n.Get(hasTags ? TextId.Tags : TextId.NoTags);
            Controls.Add(tagLabel);

            Controls.Add(tagsFrame);

            reminderEditor.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(reminderEditor);

            completedCheckBox = new CheckBox();
            completedCheckBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            completedCheckBox.AutoSize = true;
            completedCheckBox.Location = new Point(margin, footerTop + 4);
            completedCheckBox.Text = L10n.Get(TextId.Completed);
            completedCheckBox.Checked = item != null && item.IsComplete;
            Controls.Add(completedCheckBox);

            Button okButton = new Button();
            okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(clientWidth - margin - 156, footerTop);
            okButton.Size = new Size(75, buttonHeight);
            okButton.Text = L10n.Get(TextId.OK);
            okButton.Click += OkButton_Click;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(clientWidth - margin - 75, footerTop);
            cancelButton.Size = new Size(75, buttonHeight);
            cancelButton.Text = L10n.Get(TextId.Cancel);
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

        public List<TodoSubtask> Subtasks
        {
            get
            {
                List<TodoSubtask> subtasks = new List<TodoSubtask>();
                for (int i = 0; i < subtasksListBox.Items.Count; i++)
                {
                    TodoSubtask draft = (TodoSubtask)subtasksListBox.Items[i];
                    subtasks.Add(new TodoSubtask
                    {
                        Title = draft.Title,
                        Description = draft.Description,
                        IsComplete = subtasksListBox.GetItemChecked(i)
                    });
                }
                return subtasks;
            }
        }

        public bool IsComplete
        {
            get { return completedCheckBox.Checked; }
        }

        public int TitleColorIndex
        {
            get { return titleColorComboBox.SelectedIndex - 1; }
        }

        public List<string> TagIds
        {
            get
            {
                List<string> ids = new List<string>();
                foreach (TagEntry entry in tagsPanel.Controls)
                {
                    if (entry.Checked)
                    {
                        ids.Add(entry.TagId);
                    }
                }
                return ids;
            }
        }

        public DateTime? ReminderUtc { get; private set; }
        public int ReminderAdvanceMinutes { get; private set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            titleTextBox.Focus();
        }

        private Panel CreateSubtasksPanel(Rectangle bounds)
        {
            const int buttonWidth = 110;
            int buttonLeft = bounds.Width - buttonWidth;

            Panel panel = new Panel
            {
                Bounds = bounds,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            panel.Controls.Add(new Label { AutoSize = true, Text = L10n.Get(TextId.Subtasks) });
            panel.Controls.Add(new ThemeFrame(subtasksListBox)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Bounds = new Rectangle(0, SubtaskListTop, buttonLeft - 6, bounds.Height - SubtaskListTop)
            });

            Button addButton = new Button { Text = L10n.Get(TextId.Add) };
            Button editButton = new Button { Text = L10n.Get(TextId.Edit), Enabled = false };
            Button deleteButton = new Button { Text = L10n.Get(TextId.Delete), Enabled = false };
            Button[] buttons = { addButton, editButton, deleteButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Anchor = AnchorStyles.Top | AnchorStyles.Right;
                buttons[i].Bounds = new Rectangle(buttonLeft,
                    SubtaskListTop + i * (SubtaskButtonHeight + SubtaskButtonGap), buttonWidth, SubtaskButtonHeight);
                panel.Controls.Add(buttons[i]);
            }
            subtasksListBox.SelectedIndexChanged += delegate
            {
                editButton.Enabled = deleteButton.Enabled = subtasksListBox.SelectedIndex >= 0;
            };
            addButton.Click += delegate { EditSubtask(-1); };
            editButton.Click += delegate { EditSubtask(subtasksListBox.SelectedIndex); };
            deleteButton.Click += delegate
            {
                int index = subtasksListBox.SelectedIndex;
                subtasksListBox.Items.RemoveAt(index);
                subtasksListBox.SelectedIndex = Math.Min(index, subtasksListBox.Items.Count - 1);
                editButton.Enabled = deleteButton.Enabled = subtasksListBox.SelectedIndex >= 0;
            };
            return panel;
        }

        private void EditSubtask(int index)
        {
            bool isNew = index < 0;
            TodoSubtask previous = isNew ? new TodoSubtask() : (TodoSubtask)subtasksListBox.Items[index];
            using (TextPromptForm dialog = new TextPromptForm(
                L10n.Get(isNew ? TextId.Add : TextId.EditSubtask), previous))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                TodoSubtask updated = new TodoSubtask { Title = dialog.Value, Description = dialog.Description };
                if (isNew)
                {
                    subtasksListBox.SelectedIndex = subtasksListBox.Items.Add(updated);
                }
                else
                {
                    bool isComplete = subtasksListBox.GetItemChecked(index);
                    subtasksListBox.Items[index] = updated;
                    subtasksListBox.SetItemChecked(index, isComplete);
                }
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (TaskTitle.Length == 0)
            {
                MessageBox.Show(this, L10n.Get(TextId.TaskTitleRequired), "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            DateTime? utc;
            int advanceMinutes;
            string error = reminderEditor.ReadSchedule(out utc, out advanceMinutes);
            if (error != null)
            {
                MessageBox.Show(this, error, L10n.Get(TextId.Reminder), MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            ReminderUtc = utc;
            ReminderAdvanceMinutes = advanceMinutes;
        }

        private void TitleColorComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= titleColorComboBox.Items.Count)
            {
                return;
            }

            Rectangle bounds = e.Bounds;
            bounds.Inflate(-3, 0);
            if (e.Index > 0)
            {
                bounds = TagPalette.DrawSwatch(e.Graphics, bounds, e.Index - 1);
            }
            TextRenderer.DrawText(e.Graphics, titleColorComboBox.GetItemText(titleColorComboBox.Items[e.Index]),
                e.Font, bounds, e.ForeColor, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            e.DrawFocusRectangle();
        }

        private sealed class SubtaskListBox : CheckedListBox
        {
            private const int MouseMoveMessage = 0x0200;
            private const int LeftButtonDownMessage = 0x0201;
            private const int LeftButtonUpMessage = 0x0202;
            private const int LeftButtonDoubleClickMessage = 0x0203;
            private const int LeftButtonMask = 0x0001;
            private const int CheckBoxInset = 1;
            private bool suppressMouseCheck;

            protected override void WndProc(ref Message message)
            {
                bool buttonClick = message.Msg == LeftButtonDownMessage || message.Msg == LeftButtonUpMessage ||
                    message.Msg == LeftButtonDoubleClickMessage;
                bool dragging = message.Msg == MouseMoveMessage && (message.WParam.ToInt64() & LeftButtonMask) != 0;
                if (!dragging && (!buttonClick || IsCheckBoxHit(message.LParam)))
                {
                    base.WndProc(ref message);
                    return;
                }

                // Keep native row selection and keyboard handling while blocking checks from text clicks.
                bool previous = suppressMouseCheck;
                suppressMouseCheck = true;
                try
                {
                    base.WndProc(ref message);
                }
                finally
                {
                    suppressMouseCheck = previous;
                }
            }

            protected override void OnItemCheck(ItemCheckEventArgs e)
            {
                if (suppressMouseCheck)
                {
                    e.NewValue = e.CurrentValue;
                    return;
                }
                base.OnItemCheck(e);
            }

            private bool IsCheckBoxHit(IntPtr mousePosition)
            {
                int position = mousePosition.ToInt32();
                Point point = new Point(unchecked((short)position), unchecked((short)(position >> 16)));
                int index = IndexFromPoint(point);
                if (index == NoMatches)
                {
                    return false;
                }

                Rectangle row = GetItemRectangle(index);
                using (Graphics graphics = CreateGraphics())
                {
                    Size size = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);
                    int x = RightToLeft == RightToLeft.Yes
                        ? row.Right - CheckBoxInset - size.Width : row.Left + CheckBoxInset;
                    int y = row.Top + Math.Max(0, (row.Height - size.Height) / 2);
                    return row.Contains(point) && new Rectangle(x, y, size.Width, size.Height).Contains(point);
                }
            }
        }

        private sealed class TagEntry : CheckBox
        {
            internal const int RowHeight = 18;
            internal const int ColumnWidth = 164;
            private readonly TodoTag tag;

            internal TagEntry(TodoTag tag)
            {
                this.tag = tag;
                Text = tag.ToString();
                UseMnemonic = false;
                Size = new Size(ColumnWidth, RowHeight);
            }

            internal string TagId
            {
                get { return tag.Id; }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Color background = SystemColors.Window;
                Color textColor = SystemColors.WindowText;
                e.Graphics.FillRectangle(SystemBrushes.Window, ClientRectangle);

                CheckBoxState state = Checked ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
                Size glyph = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                Point checkBox = new Point(2, (ClientSize.Height - glyph.Height) / 2);
                CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBox, state);

                Rectangle tagBounds = Rectangle.FromLTRB(checkBox.X + glyph.Width + 5, 0, ClientSize.Width - 4, ClientSize.Height);
                TagPalette.DrawTag(e.Graphics, tag, Font, tagBounds, textColor, background);
                if (Focused && ShowFocusCues)
                {
                    ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2), textColor, background);
                }
            }
        }
    }
}
