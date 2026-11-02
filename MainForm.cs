using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    public class MainForm : Form
    {
        private readonly Storage storage;
        private TodoStore store;
        private ListBox listsListBox;
        private ListView tasksListView;
        private TextBox descriptionTextBox;
        private Label listTitleLabel;
        private Button addTaskButton;
        private Button editTaskButton;
        private Button deleteTaskButton;
        private SplitContainer mainSplit;
        private SplitContainer contentSplit;
        private bool loadingTasks;

        public MainForm()
        {
            storage = new Storage();
            InitializeComponent();

            string loadError;
            store = storage.Load(out loadError);
            LoadLists(null);

            if (loadError != null)
            {
                MessageBox.Show(
                    this,
                    "Не удалось прочитать сохранённые данные. Будет создан новый список задач.\r\n\r\n" + loadError,
                    "Taskinator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void InitializeComponent()
        {
            Text = "Taskinator — список задач";
            Font = new Font("Tahoma", 8.25F);
            ClientSize = new Size(790, 500);
            MinimumSize = new Size(620, 380);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;

            ToolStrip topStrip = new ToolStrip();
            topStrip.GripStyle = ToolStripGripStyle.Hidden;
            topStrip.RenderMode = ToolStripRenderMode.System;
            topStrip.Dock = DockStyle.Top;
            ToolStripLabel applicationName = new ToolStripLabel("Taskinator");
            applicationName.Font = new Font("Tahoma", 8.25F, FontStyle.Bold);
            topStrip.Items.Add(applicationName);
            topStrip.Items.Add(new ToolStripSeparator());
            ToolStripButton newListMenuButton = new ToolStripButton("Новый список");
            newListMenuButton.Click += NewListButton_Click;
            topStrip.Items.Add(newListMenuButton);
            ToolStripButton newTaskMenuButton = new ToolStripButton("Новая задача");
            newTaskMenuButton.Click += AddTaskButton_Click;
            topStrip.Items.Add(newTaskMenuButton);
            mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.FixedPanel = FixedPanel.Panel1;
            Controls.Add(mainSplit);
            Controls.Add(topStrip);

            GroupBox listsGroup = new GroupBox();
            listsGroup.Dock = DockStyle.Fill;
            listsGroup.Text = "Мои списки";
            mainSplit.Panel1.Controls.Add(listsGroup);

            Panel listButtonsPanel = new Panel();
            listButtonsPanel.Dock = DockStyle.Bottom;
            listButtonsPanel.Height = 68;
            Button newListButton = new Button();
            newListButton.Location = new Point(8, 8);
            newListButton.Size = new Size(89, 24);
            newListButton.Text = "Создать";
            newListButton.Click += NewListButton_Click;
            listButtonsPanel.Controls.Add(newListButton);

            Button renameListButton = new Button();
            renameListButton.Location = new Point(101, 8);
            renameListButton.Size = new Size(89, 24);
            renameListButton.Text = "Переименовать";
            renameListButton.Click += RenameListButton_Click;
            listButtonsPanel.Controls.Add(renameListButton);

            Button deleteListButton = new Button();
            deleteListButton.Location = new Point(8, 37);
            deleteListButton.Size = new Size(89, 24);
            deleteListButton.Text = "Удалить";
            deleteListButton.Click += DeleteListButton_Click;
            listButtonsPanel.Controls.Add(deleteListButton);

            listsListBox = new ListBox();
            listsListBox.BorderStyle = BorderStyle.FixedSingle;
            listsListBox.Dock = DockStyle.Fill;
            listsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            listsListBox.IntegralHeight = false;
            listsListBox.ItemHeight = 24;
            listsListBox.DrawItem += ListsListBox_DrawItem;
            listsListBox.SelectedIndexChanged += ListsListBox_SelectedIndexChanged;
            listsGroup.Controls.Add(listsListBox);
            listsGroup.Controls.Add(listButtonsPanel);

            contentSplit = new SplitContainer();
            contentSplit.Dock = DockStyle.Fill;
            contentSplit.Orientation = Orientation.Horizontal;
            mainSplit.Panel2.Controls.Add(contentSplit);

            Panel tasksHeader = new Panel();
            tasksHeader.Dock = DockStyle.Top;
            tasksHeader.Height = 37;

            listTitleLabel = new Label();
            listTitleLabel.AutoEllipsis = true;
            listTitleLabel.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            listTitleLabel.Location = new Point(8, 10);
            listTitleLabel.Size = new Size(200, 19);
            listTitleLabel.Text = "Выберите список";
            tasksHeader.Controls.Add(listTitleLabel);

            deleteTaskButton = new Button();
            deleteTaskButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            deleteTaskButton.Location = new Point(0, 6);
            deleteTaskButton.Size = new Size(75, 25);
            deleteTaskButton.Text = "Удалить";
            deleteTaskButton.Click += DeleteTaskButton_Click;
            tasksHeader.Controls.Add(deleteTaskButton);

            editTaskButton = new Button();
            editTaskButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            editTaskButton.Location = new Point(0, 6);
            editTaskButton.Size = new Size(91, 25);
            editTaskButton.Text = "Изменить";
            editTaskButton.Click += EditTaskButton_Click;
            tasksHeader.Controls.Add(editTaskButton);

            addTaskButton = new Button();
            addTaskButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            addTaskButton.Location = new Point(0, 6);
            addTaskButton.Size = new Size(104, 25);
            addTaskButton.Text = "Новая задача";
            addTaskButton.Click += AddTaskButton_Click;
            tasksHeader.Controls.Add(addTaskButton);

            tasksHeader.Resize += TasksHeader_Resize;

            tasksListView = new ListView();
            tasksListView.CheckBoxes = true;
            tasksListView.Dock = DockStyle.Fill;
            tasksListView.FullRowSelect = true;
            tasksListView.HideSelection = false;
            tasksListView.View = View.Details;
            tasksListView.Columns.Add("Задача", 260, HorizontalAlignment.Left);
            tasksListView.Columns.Add("Статус", 100, HorizontalAlignment.Left);
            tasksListView.ItemChecked += TasksListView_ItemChecked;
            tasksListView.SelectedIndexChanged += TasksListView_SelectedIndexChanged;
            tasksListView.DoubleClick += TasksListView_DoubleClick;
            contentSplit.Panel1.Controls.Add(tasksListView);
            contentSplit.Panel1.Controls.Add(tasksHeader);

            GroupBox descriptionGroup = new GroupBox();
            descriptionGroup.Dock = DockStyle.Fill;
            descriptionGroup.Text = "Описание выбранной задачи";
            contentSplit.Panel2.Controls.Add(descriptionGroup);

            descriptionTextBox = new TextBox();
            descriptionTextBox.BackColor = SystemColors.Window;
            descriptionTextBox.BorderStyle = BorderStyle.FixedSingle;
            descriptionTextBox.Dock = DockStyle.Fill;
            descriptionTextBox.Multiline = true;
            descriptionTextBox.ReadOnly = true;
            descriptionTextBox.ScrollBars = ScrollBars.Vertical;
            descriptionTextBox.TabStop = false;
            descriptionGroup.Controls.Add(descriptionTextBox);

            FormClosing += MainForm_FormClosing;
            Shown += MainForm_Shown;
        }

        private void TasksHeader_Resize(object sender, EventArgs e)
        {
            Panel header = (Panel)sender;
            int padding = 8;
            deleteTaskButton.Left = header.ClientSize.Width - deleteTaskButton.Width - padding;
            editTaskButton.Left = deleteTaskButton.Left - editTaskButton.Width - 5;
            addTaskButton.Left = editTaskButton.Left - addTaskButton.Width - 5;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // SplitContainer получает фактический размер только после показа формы.
            // Поэтому ограничения задаются здесь, а не в InitializeComponent.
            mainSplit.Panel1MinSize = 155;
            mainSplit.Panel2MinSize = 350;
            contentSplit.Panel1MinSize = 150;
            contentSplit.Panel2MinSize = 90;
            SetInitialSplitterDistance(mainSplit, 205);
            SetInitialSplitterDistance(contentSplit, 280);
        }

        private static void SetInitialSplitterDistance(SplitContainer splitter, int preferredDistance)
        {
            int availableLength = splitter.Orientation == Orientation.Vertical
                ? splitter.ClientSize.Width
                : splitter.ClientSize.Height;
            int maximumDistance = availableLength - splitter.Panel2MinSize - splitter.SplitterWidth;

            if (maximumDistance < splitter.Panel1MinSize)
            {
                return;
            }

            splitter.SplitterDistance = Math.Min(
                Math.Max(preferredDistance, splitter.Panel1MinSize),
                maximumDistance);
        }

        private TodoList CurrentList
        {
            get { return listsListBox.SelectedItem as TodoList; }
        }

        private TodoItem CurrentItem
        {
            get
            {
                if (tasksListView.SelectedItems.Count == 0)
                {
                    return null;
                }
                return tasksListView.SelectedItems[0].Tag as TodoItem;
            }
        }

        private void LoadLists(string selectedListId)
        {
            listsListBox.BeginUpdate();
            listsListBox.Items.Clear();
            foreach (TodoList list in store.Lists)
            {
                listsListBox.Items.Add(list);
            }
            listsListBox.EndUpdate();

            if (listsListBox.Items.Count == 0)
            {
                LoadTasks();
                return;
            }

            int selectedIndex = 0;
            if (selectedListId != null)
            {
                for (int i = 0; i < store.Lists.Count; i++)
                {
                    if (store.Lists[i].Id == selectedListId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            listsListBox.SelectedIndex = selectedIndex;
        }

        private void LoadTasks()
        {
            loadingTasks = true;
            tasksListView.BeginUpdate();
            tasksListView.Items.Clear();
            TodoList list = CurrentList;
            listTitleLabel.Text = list == null ? "Выберите или создайте список" : list.Name;

            if (list != null)
            {
                foreach (TodoItem item in list.Items)
                {
                    ListViewItem viewItem = new ListViewItem(item.Title ?? String.Empty);
                    viewItem.Checked = item.IsComplete;
                    viewItem.SubItems.Add(item.IsComplete ? "Выполнено" : "Не выполнено");
                    viewItem.Tag = item;
                    tasksListView.Items.Add(viewItem);
                }
            }
            tasksListView.EndUpdate();
            loadingTasks = false;
            descriptionTextBox.Text = String.Empty;
            UpdateTaskControls();
        }

        private void UpdateTaskControls()
        {
            bool hasList = CurrentList != null;
            bool hasItem = CurrentItem != null;
            addTaskButton.Enabled = hasList;
            editTaskButton.Enabled = hasItem;
            deleteTaskButton.Enabled = hasItem;
        }

        private void ListsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadTasks();
        }

        private void ListsListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= listsListBox.Items.Count)
            {
                return;
            }

            TodoList list = listsListBox.Items[e.Index] as TodoList;
            if (list == null)
            {
                return;
            }

            Rectangle iconBounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + 4, 16, 16);
            e.Graphics.DrawImage(ListIconCatalog.GetImage(list.IconKey), iconBounds);

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color textColor = selected ? SystemColors.HighlightText : SystemColors.WindowText;
            Rectangle textBounds = new Rectangle(e.Bounds.Left + 27, e.Bounds.Top, e.Bounds.Width - 31, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                list.Name ?? String.Empty,
                e.Font,
                textBounds,
                textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private void NewListButton_Click(object sender, EventArgs e)
        {
            using (TextPromptForm dialog = new TextPromptForm("Новый список", "Название списка:", String.Empty, ListIconCatalog.DefaultIconKey))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                TodoList list = new TodoList();
                list.Name = dialog.Value;
                list.IconKey = dialog.IconKey;
                store.Lists.Add(list);
                SaveData();
                LoadLists(list.Id);
            }
        }

        private void RenameListButton_Click(object sender, EventArgs e)
        {
            TodoList list = CurrentList;
            if (list == null)
            {
                return;
            }

            using (TextPromptForm dialog = new TextPromptForm("Переименовать список", "Название списка:", list.Name, list.IconKey))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                list.Name = dialog.Value;
                list.IconKey = dialog.IconKey;
                SaveData();
                LoadLists(list.Id);
            }
        }

        private void DeleteListButton_Click(object sender, EventArgs e)
        {
            TodoList list = CurrentList;
            if (list == null)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                "Удалить список «" + list.Name + "» вместе со всеми задачами?",
                "Taskinator",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            int index = listsListBox.SelectedIndex;
            store.Lists.Remove(list);
            SaveData();
            string nextListId = null;
            if (store.Lists.Count > 0)
            {
                nextListId = store.Lists[Math.Min(index, store.Lists.Count - 1)].Id;
            }
            LoadLists(nextListId);
        }

        private void AddTaskButton_Click(object sender, EventArgs e)
        {
            TodoList list = CurrentList;
            if (list == null)
            {
                MessageBox.Show(this, "Сначала создайте или выберите список.", "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (TaskEditorForm dialog = new TaskEditorForm(null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                TodoItem item = new TodoItem();
                item.Title = dialog.TaskTitle;
                item.Description = dialog.Description;
                item.IsComplete = dialog.IsComplete;
                list.Items.Add(item);
                SaveData();
                LoadTasks();
                SelectTask(item.Id);
            }
        }

        private void EditTaskButton_Click(object sender, EventArgs e)
        {
            TodoItem item = CurrentItem;
            if (item == null)
            {
                return;
            }

            using (TaskEditorForm dialog = new TaskEditorForm(item))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                item.Title = dialog.TaskTitle;
                item.Description = dialog.Description;
                item.IsComplete = dialog.IsComplete;
                SaveData();
                LoadTasks();
                SelectTask(item.Id);
            }
        }

        private void DeleteTaskButton_Click(object sender, EventArgs e)
        {
            TodoItem item = CurrentItem;
            TodoList list = CurrentList;
            if (item == null || list == null)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                "Удалить задачу «" + item.Title + "»?",
                "Taskinator",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            list.Items.Remove(item);
            SaveData();
            LoadTasks();
        }

        private void TasksListView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (loadingTasks)
            {
                return;
            }

            TodoItem item = e.Item.Tag as TodoItem;
            if (item == null)
            {
                return;
            }

            item.IsComplete = e.Item.Checked;
            e.Item.SubItems[1].Text = item.IsComplete ? "Выполнено" : "Не выполнено";
            SaveData();
        }

        private void TasksListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            TodoItem item = CurrentItem;
            descriptionTextBox.Text = item == null ? String.Empty : item.Description ?? String.Empty;
            UpdateTaskControls();
        }

        private void TasksListView_DoubleClick(object sender, EventArgs e)
        {
            if (CurrentItem != null)
            {
                EditTaskButton_Click(sender, e);
            }
        }

        private void SelectTask(string itemId)
        {
            foreach (ListViewItem viewItem in tasksListView.Items)
            {
                TodoItem item = viewItem.Tag as TodoItem;
                if (item != null && item.Id == itemId)
                {
                    viewItem.Selected = true;
                    viewItem.Focused = true;
                    viewItem.EnsureVisible();
                    break;
                }
            }
        }

        private void SaveData()
        {
            try
            {
                storage.Save(store);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "Не удалось сохранить данные.\r\n\r\n" + exception.Message,
                    "Taskinator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveData();
        }
    }
}
