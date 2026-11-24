using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Taskinator
{
    public partial class MainForm
    {
        private sealed class MetaList
        {
            internal readonly TextId Caption;
            internal readonly string IconKey;

            internal MetaList(TextId caption, string iconKey)
            {
                Caption = caption;
                IconKey = iconKey;
            }

            public override string ToString()
            {
                return L10n.Get(Caption);
            }
        }

        private static readonly MetaList[] MetaLists =
        {
            new MetaList(TextId.MetaAllTasks, "186.png"),
            new MetaList(TextId.MetaCompletedTasks, "102.png"),
            new MetaList(TextId.MetaToday, "025.png"),
            new MetaList(TextId.MetaThisWeek, "026.png")
        };

        private DateTime taskViewDate;

        private MetaList CurrentMetaList
        {
            get { return listsListBox.SelectedItem as MetaList; }
        }

        internal static IEnumerable<TodoItem> GetMetaTasks(IEnumerable<TodoList> lists, TextId view, DateTime today)
        {
            IEnumerable<TodoItem> tasks = lists.SelectMany(list => list.Items);
            switch (view)
            {
                case TextId.MetaCompletedTasks:
                    return tasks.Where(task => task.IsComplete);
                case TextId.MetaToday:
                    return TasksScheduledBetween(tasks, today, 1);
                case TextId.MetaThisWeek:
                    int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                    return TasksScheduledBetween(tasks, today.AddDays(-daysSinceMonday), 7);
                default:
                    return tasks.OrderBy(task => task.IsComplete);
            }
        }

        private static IEnumerable<TodoItem> TasksScheduledBetween(IEnumerable<TodoItem> tasks, DateTime start, int days)
        {
            DateTime end = start.AddDays(days);
            return tasks.Where(task =>
            {
                if (!task.ReminderUtc.HasValue)
                {
                    return false;
                }
                DateTime scheduled = task.ReminderUtc.Value.ToLocalTime();
                return scheduled >= start && scheduled < end;
            }).OrderBy(task => task.ReminderUtc);
        }

        private void RefreshDatedMetaList()
        {
            MetaList metaList = CurrentMetaList;
            if (metaList == null || (metaList.Caption != TextId.MetaToday && metaList.Caption != TextId.MetaThisWeek) ||
                taskViewDate == DateTime.Today || draggedRow != null)
            {
                return;
            }

            TodoItem[] selected = tasksListBox.SelectedTasks();
            TaskListBox.SubtaskRow child = tasksListBox.SelectedSubtask;
            int top = tasksListBox.TopIndex;
            LoadTasks();
            if (child == null)
            {
                RestoreTaskSelection(selected, top);
            }
            else
            {
                SelectTask(child.Parent, child.Subtask);
            }
        }

        private TodoList ChooseTaskDestination()
        {
            if (store.Lists.Count == 1)
            {
                return store.Lists[0];
            }

            string caption = L10n.Get(TextId.ChooseTaskList);
            using (Form dialog = new Form
            {
                Text = L10n.Get(TextId.NewTask),
                Font = Font,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(320, 100),
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            })
            {
                ThemeStyle.StyleDialog(dialog);
                ComboBox lists = new ComboBox
                {
                    AccessibleName = caption,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Bounds = new Rectangle(12, 31, 296, 22)
                };
                lists.Items.AddRange(store.Lists.ToArray());
                lists.SelectedIndex = 0;
                Button ok = new Button
                {
                    Text = L10n.Get(TextId.OK), DialogResult = DialogResult.OK,
                    Bounds = new Rectangle(149, 63, 76, 25)
                };
                Button cancel = new Button
                {
                    Text = L10n.Get(TextId.Cancel), DialogResult = DialogResult.Cancel,
                    Bounds = new Rectangle(232, 63, 76, 25)
                };
                dialog.Controls.Add(new Label { Text = caption, AutoSize = true, Location = new Point(12, 10) });
                dialog.Controls.AddRange(new Control[] { lists, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? (TodoList)lists.SelectedItem : null;
            }
        }
    }
}
