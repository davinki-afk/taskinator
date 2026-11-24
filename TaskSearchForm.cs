using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    internal sealed class TaskSearchForm : Form
    {
        private readonly TodoStore store;
        private readonly TextBox queryBox;
        private readonly CheckBox fromCheckBox;
        private readonly CheckBox toCheckBox;
        private readonly DateTimePicker fromPicker;
        private readonly DateTimePicker toPicker;
        private readonly CheckBox hideCompletedCheckBox;
        private readonly TaskListBox results;
        private readonly Label status;
        private readonly Button openButton;

        internal TaskSearchForm(TodoStore store)
        {
            this.store = store;
            ThemeStyle.StyleDialog(this);
            Text = L10n.Get(TextId.SearchTasks);
            Font = new Font("Tahoma", 8.25F);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 360);
            MinimumSize = SizeFromClientSize(new Size(540, 270));

            Label queryLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = L10n.Get(TextId.SearchQuery)
            };
            queryBox = new TextBox
            {
                AccessibleName = queryLabel.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 30),
                Width = 516
            };

            Label reminderDateLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 60),
                Text = L10n.Get(TextId.ReminderDate)
            };
            FlowLayoutPanel dates = new ReminderDatePanel
            {
                AccessibleName = reminderDateLabel.Text,
                AccessibleRole = AccessibleRole.Grouping,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(9, 78),
                MinimumSize = new Size(0, 28),
                WrapContents = false
            };
            fromCheckBox = new CheckBox { AutoSize = true, Text = L10n.Get(TextId.DateFrom) };
            toCheckBox = new CheckBox { AutoSize = true, Text = L10n.Get(TextId.DateTo) };
            fromPicker = AddDatePicker(dates, fromCheckBox);
            toPicker = AddDatePicker(dates, toCheckBox);

            hideCompletedCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(12, 110),
                Text = L10n.Get(TextId.HideCompletedTasks)
            };

            results = new TaskListBox
            {
                AccessibleName = L10n.Get(TextId.SearchResults),
                Location = new Point(12, 138),
                Size = new Size(516, 180)
            };
            results.SetTags(store.Tags);
            ThemeFrame resultsFrame = new ThemeFrame(results)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            status = new Label
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 326),
                Size = new Size(300, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };
            openButton = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false,
                Location = new Point(318, 326),
                Size = new Size(126, 24),
                Text = L10n.Get(TextId.OpenTask)
            };
            Button closeButton = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(452, 326),
                Size = new Size(76, 24),
                Text = L10n.Get(TextId.Close),
                DialogResult = DialogResult.Cancel
            };
            Controls.AddRange(new Control[] { queryLabel, queryBox, reminderDateLabel, dates, hideCompletedCheckBox, resultsFrame, status, openButton, closeButton });
            AcceptButton = openButton;
            CancelButton = closeButton;
            ActiveControl = queryBox;

            queryBox.TextChanged += UpdateResults;
            hideCompletedCheckBox.CheckedChanged += UpdateResults;
            results.SelectedIndexChanged += delegate
            {
                TaskListBox.SearchResult selected = results.SelectedItem as TaskListBox.SearchResult;
                openButton.Enabled = selected != null;
                openButton.Text = L10n.Get(selected != null && selected.Subtask != null ? TextId.OpenSubtask : TextId.OpenTask);
            };
            results.TaskActivated += OpenResult;
            openButton.Click += OpenResult;
            UpdateResults(this, EventArgs.Empty);
        }

        internal TaskListBox.SearchResult SelectedResult { get; private set; }

        private void OpenResult(object sender, EventArgs e)
        {
            TaskListBox.SearchResult result = results.SelectedItem as TaskListBox.SearchResult;
            if (result == null)
            {
                return;
            }

            SelectedResult = result;
            DialogResult = DialogResult.OK;
        }

        private DateTimePicker AddDatePicker(FlowLayoutPanel panel, CheckBox checkBox)
        {
            checkBox.Anchor = AnchorStyles.Left;
            panel.Controls.Add(checkBox);
            DateTimePicker picker = new DateTimePicker
            {
                AccessibleName = checkBox.Text,
                Enabled = false,
                Format = DateTimePickerFormat.Short,
                Width = 126,
                Margin = new Padding(3, 3, 12, 3)
            };
            panel.Controls.Add(picker);
            picker.ValueChanged += UpdateResults;
            checkBox.CheckedChanged += delegate(object sender, EventArgs e)
            {
                picker.Enabled = checkBox.Checked;
                UpdateResults(sender, e);
            };
            return picker;
        }

        private void UpdateResults(object sender, EventArgs e)
        {
            DateTime? from = fromCheckBox.Checked ? fromPicker.Value.Date : (DateTime?)null;
            DateTime? to = toCheckBox.Checked ? toPicker.Value.Date : (DateTime?)null;
            bool filterByDate = from.HasValue || to.HasValue;
            string query = queryBox.Text.Trim();
            results.SearchQuery = query;
            results.HighlightReminder = filterByDate;

            results.BeginUpdate();
            try
            {
                results.Items.Clear();
                openButton.Enabled = false;
                if (from > to)
                {
                    status.Text = L10n.Get(TextId.InvalidSearchDateRange);
                    return;
                }

                HashSet<string> matchingTags = new HashSet<string>();
                if (query.Length > 0)
                {
                    foreach (TodoTag tag in store.Tags)
                    {
                        if (TaskListBox.FindSearchText(tag.Name, query) >= 0)
                        {
                            matchingTags.Add(tag.Id);
                        }
                    }
                }

                foreach (TodoList list in store.Lists)
                {
                    foreach (TodoItem task in list.Items)
                    {
                        if (hideCompletedCheckBox.Checked && task.IsComplete)
                        {
                            continue;
                        }

                        DateTime? reminder = task.ReminderUtc;
                        if (filterByDate)
                        {
                            if (!reminder.HasValue)
                            {
                                continue;
                            }
                            DateTime date = reminder.Value.ToLocalTime().Date;
                            if (date < from || date > to)
                            {
                                continue;
                            }
                        }
                        if (query.Length == 0 || MatchesText(task.Title, TaskListBox.GetDescription(task), query) ||
                            task.TagIds.Exists(matchingTags.Contains))
                        {
                            results.Items.Add(new TaskListBox.SearchResult(list, task));
                        }
                        if (query.Length > 0)
                        {
                            foreach (TodoSubtask subtask in task.Subtasks)
                            {
                                if ((!hideCompletedCheckBox.Checked || !subtask.IsComplete) &&
                                    MatchesText(subtask.Title, TaskListBox.SingleLinePreview(subtask.Description), query))
                                {
                                    results.Items.Add(new TaskListBox.SearchResult(list, task, subtask));
                                }
                            }
                        }
                    }
                }

                status.Text = results.Items.Count == 0 ? L10n.Get(TextId.NoSearchResults) : String.Empty;
            }
            finally
            {
                results.EndUpdate();
            }
        }

        private static bool MatchesText(string title, string description, string query)
        {
            return TaskListBox.FindSearchText(title, query) >= 0 ||
                TaskListBox.FindSearchText(description, query) >= 0;
        }

        private sealed class ReminderDatePanel : FlowLayoutPanel
        {
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams parameters = base.CreateParams;
                    parameters.ExStyle |= ThemeStyle.CompositedWindowStyle;
                    return parameters;
                }
            }
        }
    }
}
