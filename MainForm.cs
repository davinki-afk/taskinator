using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Taskinator
{
    public partial class MainForm : Form
    {
        private const int SectionHeaderHeight = 37;
        private const int ListActionsHeight = 31;
        private const int ReminderDisplayMilliseconds = 10000;
        private const uint InvalidateRegion = 0x0001; // RDW_INVALIDATE
        private const uint IncludeChildren = 0x0080; // RDW_ALLCHILDREN
        private const uint UpdateNow = 0x0100; // RDW_UPDATENOW

        private readonly Storage storage;
        private readonly Icon applicationIcon;
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip helpMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip listMoveMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip taskMoveMenu = new ContextMenuStrip();
        private readonly Timer reminderTimer = new Timer();
        private readonly Stopwatch lastReminder = new Stopwatch();
        private readonly Dictionary<Button, TextId> actionText = new Dictionary<Button, TextId>();
        private readonly Dictionary<TextId, Action> shortcutCommands = new Dictionary<TextId, Action>();
        private readonly Dictionary<Keys, Action> activeShortcuts = new Dictionary<Keys, Action>();
        private Dictionary<TextId, Keys> shortcuts = new Dictionary<TextId, Keys>();
        private readonly HashSet<string> expandedCompletedLists = new HashSet<string>();
        private readonly HashSet<string> tasksWithExpandedSubtasks = new HashSet<string>();
        private readonly ThemeToolbarRenderer themeToolbarRenderer = new ThemeToolbarRenderer();
        private static readonly bool LegacyWindowPainting = Environment.OSVersion.Version.Major < 6;
        private bool inSizeMove;
        private Rectangle listActionsBounds;
        private Rectangle taskActionsBounds;
        private bool exitRequested;
        private bool resourcesDisposed;
        private TodoStore store;
        private ListBox listsListBox;
        private TaskListBox tasksListBox;
        private readonly ToolTip actionToolTip = new ToolTip();
        private ToolStrip topStrip;
        private ToolStripButton settingsMenuButton;
        private ToolStripButton searchButton;
        private ToolStripDropDownButton helpButton;
        private ToolStripMenuItem helpMenuItem;
        private ToolStripMenuItem aboutMenuItem;
        private ToolStripItem trayOpenItem;
        private ToolStripItem trayExitItem;
        private Bitmap trayOpenImage;
        private Label listsTitleLabel;
        private Label listTitleLabel;
        private Button renameListButton;
        private Button deleteListButton;
        private Button addTaskButton;
        private Button editTaskButton;
        private Button deleteTaskButton;
        private Button deleteCompletedTasksButton;
        private SplitContainer mainSplit;
        private TableLayoutPanel listActions;
        private FlowLayoutPanel taskButtonsPanel;

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool RedrawWindow(IntPtr window, IntPtr rect, IntPtr region, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int ShellAboutW(IntPtr window, StringBuilder app, string credits, IntPtr icon);

        public MainForm()
        {
            storage = new Storage();
            string loadError;
            store = storage.Load(out loadError);
            L10n.SetLanguage(store.Language);
            ThemeStyle.Select(store.Theme);
            using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("Taskinator.taskinator.ico"))
            {
                applicationIcon = new Icon(stream);
            }
            InitializeComponent();
            InitializeTray();
            ApplyLanguage();
            ApplyTheme();
            LoadLists(null);

            if (loadError != null)
            {
                MessageBox.Show(
                    this,
                    L10n.Get(TextId.LoadFailed) + "\r\n\r\n" + loadError,
                    "Taskinator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                UpdateWindowsStartup(store.StartWithWindows, false);
            }
        }

        private void InitializeComponent()
        {
            Font = new Font("Tahoma", 8.25F);
            MinimumSize = new Size(620, 380);
            Size = MinimumSize;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = applicationIcon;

            topStrip = new ToolStrip();
            topStrip.GripStyle = ToolStripGripStyle.Hidden;
            topStrip.RenderMode = ToolStripRenderMode.System;
            topStrip.Dock = DockStyle.Top;
            settingsMenuButton = new ToolStripButton();
            settingsMenuButton.Image = ListIconCatalog.GetImage("041.png");
            settingsMenuButton.Click += SettingsMenuButton_Click;
            shortcutCommands.Add(TextId.Settings, settingsMenuButton.PerformClick);
            topStrip.Items.Add(settingsMenuButton);
            searchButton = new ToolStripButton();
            searchButton.Image = ListIconCatalog.GetImage("049.png");
            searchButton.Click += SearchButton_Click;
            shortcutCommands.Add(TextId.Search, searchButton.PerformClick);
            topStrip.Items.Add(searchButton);
            helpButton = new ToolStripDropDownButton
            {
                AutoToolTip = false,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Image = ListIconCatalog.GetImage("164.png"),
                DropDown = helpMenu
            };
            helpMenuItem = new ToolStripMenuItem { Image = helpButton.Image };
            aboutMenuItem = new ToolStripMenuItem { Image = ListIconCatalog.GetImage("088.png") };
            helpMenuItem.Click += delegate { OpenHelp(); };
            aboutMenuItem.Click += delegate { ShowAbout(); };
            helpMenu.Items.AddRange(new[] { helpMenuItem, aboutMenuItem });
            helpButton.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right)
                {
                    helpButton.ShowDropDown();
                }
            };
            shortcutCommands.Add(TextId.About, ShowAbout);
            topStrip.Items.Add(helpButton);

            Panel mainContent = new Panel();
            mainContent.Dock = DockStyle.Fill;
            mainContent.Padding = new Padding(5, 0, 5, 5);
            mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.FixedPanel = FixedPanel.Panel1;
            mainContent.Controls.Add(mainSplit);
            Controls.Add(mainContent);
            Controls.Add(topStrip);

            listsTitleLabel = CreateSectionTitle();
            Panel listsHeader = new ThemeHeader { Dock = DockStyle.Top, Height = SectionHeaderHeight };
            listsHeader.Controls.Add(listsTitleLabel);

            listActions = new TableLayoutPanel();
            listActions.Dock = DockStyle.Bottom;
            listActions.Height = ListActionsHeight;
            listActions.Padding = new Padding(0, 5, 0, 0);
            listActions.ColumnCount = 5;
            listActions.RowCount = 1;
            listActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F));
            listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5F));
            listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F));
            listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5F));
            listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F));
            AddListActionButton(listActions, 0, ActionGlyph.Plus, TextId.CreateList, NewListButton_Click);
            renameListButton = AddListActionButton(listActions, 2, ActionGlyph.Pencil, TextId.RenameList, RenameListButton_Click);
            deleteListButton = AddListActionButton(listActions, 4, ActionGlyph.Cross, TextId.DeleteList, DeleteListButton_Click);

            listsListBox = new BufferedListBox();
            listsListBox.BorderStyle = BorderStyle.None;
            listsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            listsListBox.IntegralHeight = false;
            listsListBox.ItemHeight = 24;
            listsListBox.DrawItem += ListsListBox_DrawItem;
            listsListBox.SelectedIndexChanged += ListsListBox_SelectedIndexChanged;
            mainSplit.Panel1.Controls.Add(new ThemeFrame(listsListBox) { Dock = DockStyle.Fill });
            mainSplit.Panel1.Controls.Add(listActions);
            mainSplit.Panel1.Controls.Add(listsHeader);

            Panel tasksHeader = new ThemeHeader();
            tasksHeader.Dock = DockStyle.Top;
            tasksHeader.Height = SectionHeaderHeight;

            listTitleLabel = CreateSectionTitle();
            tasksHeader.Controls.Add(listTitleLabel);

            taskButtonsPanel = new TaskActionsPanel();
            taskButtonsPanel.AutoSize = true;
            taskButtonsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            taskButtonsPanel.Dock = DockStyle.Right;
            taskButtonsPanel.Padding = new Padding(0, 2, 0, 0);
            taskButtonsPanel.WrapContents = false;
            addTaskButton = CreateActionButton(ActionGlyph.Plus, TextId.CreateTask, AddTaskButton_Click);
            editTaskButton = CreateActionButton(ActionGlyph.Pencil, TextId.EditTask, EditTaskButton_Click);
            deleteTaskButton = CreateActionButton(ActionGlyph.Cross, TextId.DeleteTask, DeleteTaskButton_Click);
            deleteCompletedTasksButton = CreateActionButton(ActionGlyph.Eraser, TextId.DeleteCompletedTasks, DeleteCompletedTasksButton_Click);
            deleteCompletedTasksButton.Margin = new Padding(3, 3, 0, 3);
            taskButtonsPanel.Controls.Add(addTaskButton);
            taskButtonsPanel.Controls.Add(editTaskButton);
            taskButtonsPanel.Controls.Add(deleteTaskButton);
            taskButtonsPanel.Controls.Add(deleteCompletedTasksButton);
            tasksHeader.Controls.Add(taskButtonsPanel);

            tasksListBox = new TaskListBox { SelectionMode = SelectionMode.MultiExtended };
            tasksListBox.CompletionRequested += TasksListBox_CompletionRequested;
            tasksListBox.SubtaskCompletionRequested += child => SetSubtaskComplete(child, !child.Subtask.IsComplete);
            tasksListBox.CompletedSectionToggled += TasksListBox_CompletedSectionToggled;
            tasksListBox.SubtasksToggled += TasksListBox_SubtasksToggled;
            tasksListBox.SelectedIndexChanged += TasksListBox_SelectedIndexChanged;
            tasksListBox.TaskActivated += EditTaskButton_Click;
            tasksListBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyData == Keys.Delete)
                {
                    e.SuppressKeyPress = true;
                    DeleteTaskButton_Click(sender, e);
                }
            };
            InitializeMoveMenus();
            EnableRowDragging(listsListBox);
            EnableRowDragging(tasksListBox);
            mainSplit.Panel2.Padding = new Padding(0, 0, 0, ListActionsHeight);
            mainSplit.Panel2.Controls.Add(new ThemeFrame(tasksListBox) { Dock = DockStyle.Fill });
            mainSplit.Panel2.Controls.Add(tasksHeader);

            FormClosing += MainForm_FormClosing;
            Shown += MainForm_Shown;
        }

        private void InitializeMoveMenus()
        {
            InitializeMoveMenu(listMoveMenu, listsListBox, offset => MoveList(CurrentList, offset));
            InitializeMoveMenu(taskMoveMenu, tasksListBox, StepSelectedTasks);
            ToolStripMenuItem moveToList = new ToolStripMenuItem { Image = ListIconCatalog.GetImage("131.png") };
            ToolStripMenuItem markComplete = new ToolStripMenuItem();
            ToolStripMenuItem markIncomplete = new ToolStripMenuItem();
            ToolStripMenuItem editSubtask = new ToolStripMenuItem();
            ToolStripMenuItem deleteSelected = new ToolStripMenuItem();
            ToolStripMenuItem selectAll = new ToolStripMenuItem();
            markComplete.Click += delegate { MarkSelectionComplete(true); };
            markIncomplete.Click += delegate { MarkSelectionComplete(false); };
            editSubtask.Click += EditTaskButton_Click;
            deleteSelected.Click += DeleteTaskButton_Click;
            selectAll.Click += delegate { tasksListBox.SelectAllTasks(); };
            taskMoveMenu.Items.AddRange(new ToolStripItem[]
            {
                moveToList, new ToolStripSeparator(), markComplete, markIncomplete, editSubtask, deleteSelected,
                new ToolStripSeparator(), selectAll
            });
            taskMoveMenu.Opening += delegate
            {
                TodoItem[] selected = tasksListBox.SelectedTasks();
                TaskListBox.SubtaskRow child = tasksListBox.SelectedSubtask;
                editSubtask.Text = L10n.Get(TextId.EditSubtask);
                editSubtask.Visible = child != null;
                markComplete.Text = L10n.Get(TextId.MarkTasksComplete);
                markComplete.Enabled = child != null ? !child.Subtask.IsComplete : selected.Any(item => !item.IsComplete);
                markIncomplete.Text = L10n.Get(TextId.MarkTasksIncomplete);
                markIncomplete.Enabled = child != null ? child.Subtask.IsComplete : selected.Any(item => item.IsComplete);
                TextId deleteCaption = selected.Length > 1 ? TextId.DeleteSelectedTasks : TextId.DeleteTask;
                deleteSelected.Text = L10n.Get(child != null ? TextId.DeleteSubtask : deleteCaption);
                deleteSelected.Enabled = child != null || selected.Length > 0;
                selectAll.Text = L10n.Get(TextId.SelectAllTasks);
                selectAll.Enabled = tasksListBox.Items.OfType<TodoItem>().Count() > selected.Length;
                moveToList.Text = L10n.Get(TextId.MoveToList);
                while (moveToList.DropDownItems.Count > 0)
                {
                    moveToList.DropDownItems[0].Dispose();
                }

                foreach (TodoList list in store.Lists)
                {
                    if (!selected.Any(item => !list.Items.Contains(item)))
                    {
                        continue;
                    }

                    TodoList destination = list;
                    ToolStripMenuItem target = new ToolStripMenuItem(
                        (destination.Name ?? String.Empty).Replace("&", "&&"),
                        ListIconCatalog.GetImage(destination.IconKey));
                    target.Click += delegate { MoveTasksToList(tasksListBox.SelectedTasks(), destination); };
                    moveToList.DropDownItems.Add(target);
                }
                moveToList.Enabled = selected.Length > 0 && moveToList.DropDownItems.Count > 0;
            };
        }

        private void InitializeMoveMenu(ContextMenuStrip menu, ListBox listBox, Action<int> move)
        {
            ToolStripMenuItem moveUp = new ToolStripMenuItem { Image = ListIconCatalog.GetImage("134.png") };
            ToolStripMenuItem moveDown = new ToolStripMenuItem { Image = ListIconCatalog.GetImage("133.png") };
            moveUp.Click += delegate { move(-1); };
            moveDown.Click += delegate { move(1); };
            menu.Items.Add(moveUp);
            menu.Items.Add(moveDown);
            menu.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                moveUp.Text = L10n.Get(TextId.MoveUp);
                moveDown.Text = L10n.Get(TextId.MoveDown);
                if (listBox == tasksListBox)
                {
                    TodoItem[] selected = tasksListBox.SelectedTasks();
                    e.Cancel = !tasksListBox.Items.OfType<TodoItem>().Any();
                    moveUp.Enabled = GetSteppedTaskOrder(selected, -1) != null;
                    moveDown.Enabled = GetSteppedTaskOrder(selected, 1) != null;
                }
                else
                {
                    int index = store.Lists.IndexOf(CurrentList);
                    e.Cancel = index < 0;
                    moveUp.Enabled = index > 0;
                    moveDown.Enabled = index >= 0 && index < store.Lists.Count - 1;
                }
            };
            listBox.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right)
                {
                    return;
                }

                int index = RowAtPoint(listBox, e.Location);
                if (index >= 0)
                {
                    if (listBox != tasksListBox)
                    {
                        listBox.SelectedIndex = index;
                    }
                    else if (!tasksListBox.GetSelected(index) || tasksListBox.TaskAt(index) == null)
                    {
                        tasksListBox.SelectOnly(index);
                    }
                }
            };
            listBox.ContextMenuStrip = menu;
        }

        private void ApplyLanguage()
        {
            shortcuts = KeyboardShortcuts.Load(store);
            activeShortcuts.Clear();
            foreach (KeyValuePair<TextId, Keys> binding in shortcuts)
            {
                if (binding.Value != Keys.None)
                {
                    activeShortcuts.Add(binding.Value, shortcutCommands[binding.Key]);
                }
            }

            Text = L10n.Get(TextId.WindowTitle);
            settingsMenuButton.Text = L10n.Get(TextId.Settings);
            settingsMenuButton.ToolTipText = KeyboardShortcuts.WithShortcut(settingsMenuButton.Text, shortcuts[TextId.Settings]);
            searchButton.Text = L10n.Get(TextId.Search);
            searchButton.ToolTipText = KeyboardShortcuts.WithShortcut(searchButton.Text, shortcuts[TextId.Search]);
            helpButton.Text = L10n.Get(TextId.Help);
            helpMenuItem.Text = L10n.Get(TextId.HelpTopics);
            helpMenuItem.ShortcutKeyDisplayString = KeyboardShortcuts.Format(Keys.F1);
            aboutMenuItem.Text = L10n.Get(TextId.About);
            aboutMenuItem.ShortcutKeyDisplayString = shortcuts[TextId.About] == Keys.None
                ? String.Empty
                : KeyboardShortcuts.Format(shortcuts[TextId.About]);
            trayOpenItem.Text = L10n.Get(TextId.OpenTaskinator);
            trayExitItem.Text = L10n.Get(TextId.Exit);
            listsTitleLabel.Text = L10n.Get(TextId.MyLists);
            listsListBox.AccessibleName = listsTitleLabel.Text;
            tasksListBox.AccessibleName = L10n.Get(TextId.Tasks);
            foreach (KeyValuePair<Button, TextId> action in actionText)
            {
                SetActionCaption(action.Key, action.Value, action.Value);
            }
        }

        private void SetActionCaption(Button button, TextId captionId, TextId shortcutId)
        {
            string caption = L10n.Get(captionId);
            Keys shortcut;
            shortcuts.TryGetValue(shortcutId, out shortcut);
            string tooltip = KeyboardShortcuts.WithShortcut(caption, shortcut);
            if (button.AccessibleName != caption)
            {
                button.AccessibleName = caption;
            }
            if (actionToolTip.GetToolTip(button) != tooltip)
            {
                actionToolTip.SetToolTip(button, tooltip);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Action command;
            if (keyData == Keys.F1)
            {
                command = OpenHelp;
            }
            else if (!activeShortcuts.TryGetValue(keyData, out command))
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            const long keyWasDown = 1L << 30;
            if ((msg.LParam.ToInt64() & keyWasDown) == 0)
            {
                command();
            }
            return true;
        }

        private void ApplyTheme()
        {
            ThemeStyle theme = ThemeStyle.Current;
            if (theme == null)
            {
                topStrip.RenderMode = ToolStripRenderMode.System;
            }
            else
            {
                topStrip.Renderer = themeToolbarRenderer;
            }
            helpButton.ForeColor = theme == null ? Color.Empty : theme.Ink;
            ThemeStyle.ApplyTo(this);
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            if (helpButton != null)
            {
                ApplyTheme();
            }
        }

        private static Label CreateSectionTitle()
        {
            Label label = new Label();
            label.AutoEllipsis = true;
            label.BackColor = Color.Transparent;
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            label.Padding = new Padding(8, 0, 0, 0);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private Button AddListActionButton(TableLayoutPanel panel, int column, ActionGlyph glyph, TextId action, EventHandler click)
        {
            Button button = CreateActionButton(glyph, action, click);
            button.Dock = DockStyle.Fill;
            button.Margin = Padding.Empty;
            panel.Controls.Add(button, column, 0);
            return button;
        }

        private Button CreateActionButton(ActionGlyph glyph, TextId action, EventHandler click)
        {
            Button button = new ActionButton(glyph);
            button.Click += click;
            actionText.Add(button, action);
            shortcutCommands.Add(action, button.PerformClick);
            return button;
        }

        internal void Run(bool automaticStartup)
        {
            bool startHidden = automaticStartup && !store.ShowWindowOnStartup;
            if (startHidden && !IsHandleCreated)
            {
                // A manual launch uses the window handle to activate this instance.
                CreateHandle();
            }

            trayIcon.Visible = true;
            reminderTimer.Start();
            if (!startHidden)
            {
                Application.Run(this);
                return;
            }

            FormClosed += delegate { Application.ExitThread(); };
            Application.Run();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // SplitContainer получает фактический размер только после показа формы.
            // Поэтому ограничения задаются здесь, а не в InitializeComponent.
            mainSplit.Panel1MinSize = 155;
            mainSplit.Panel2MinSize = 350;
            int maximumDistance = mainSplit.ClientSize.Width - mainSplit.Panel2MinSize - mainSplit.SplitterWidth;
            mainSplit.SplitterDistance = Math.Min(205, maximumDistance);
        }

        private void InitializeTray()
        {
            using (Icon menuIcon = new Icon(applicationIcon, trayMenu.ImageScalingSize))
            {
                trayOpenImage = menuIcon.ToBitmap();
            }
            trayOpenItem = new ToolStripMenuItem { Image = trayOpenImage };
            trayOpenItem.Click += ShowMainWindow_Click;
            trayExitItem = new ToolStripMenuItem { Image = ListIconCatalog.GetImage("door-open-out.png") };
            trayExitItem.Click += ExitFromTray_Click;
            trayMenu.Items.Add(trayOpenItem);
            trayMenu.Items.Add(trayExitItem);
            trayIcon.Icon = Icon;
            trayIcon.Text = "Taskinator";
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += ShowMainWindow_Click;
            trayIcon.BalloonTipClicked += ShowMainWindow_Click;
            reminderTimer.Interval = 1000;
            reminderTimer.Tick += ReminderTimer_Tick;
        }

        private void ShowMainWindow_Click(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            if (exitRequested)
            {
                return;
            }
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Activate();
        }

        private void ExitFromTray_Click(object sender, EventArgs e)
        {
            exitRequested = true;
            Close();
        }

        protected override void OnResizeBegin(EventArgs e)
        {
            if (LegacyWindowPainting && listActions != null && taskButtonsPanel != null)
            {
                listActionsBounds = listActions.Bounds;
                taskActionsBounds = taskButtonsPanel.Bounds;
                inSizeMove = true;
            }
            base.OnResizeBegin(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (inSizeMove)
            {
                UpdateActionStrip(listActions, ref listActionsBounds);
                UpdateActionStrip(taskButtonsPanel, ref taskActionsBounds);
                // Present both panes after layout while Windows XP is resizing the frame.
                RedrawWindow(mainSplit.Panel1.Handle, IntPtr.Zero, IntPtr.Zero, IncludeChildren | UpdateNow);
                RedrawWindow(mainSplit.Panel2.Handle, IntPtr.Zero, IntPtr.Zero, IncludeChildren | UpdateNow);
            }
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            inSizeMove = false;
            base.OnResizeEnd(e);
        }

        private static void UpdateActionStrip(Control strip, ref Rectangle previousBounds)
        {
            if (strip.Bounds != previousBounds)
            {
                previousBounds = strip.Bounds;
                // Preserve copied button pixels until their replacements are painted at the new location.
                RedrawWindow(strip.Handle, IntPtr.Zero, IntPtr.Zero, InvalidateRegion | IncludeChildren | UpdateNow);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != 0 && m.Msg == Program.ShowWindowMessage && store != null)
            {
                ShowMainWindow();
                return;
            }
            base.WndProc(ref m);
        }

        private TodoList CurrentList
        {
            get { return listsListBox.SelectedItem as TodoList; }
        }

        private TodoItem CurrentItem
        {
            get
            {
                TodoItem[] selected = tasksListBox.SelectedTasks();
                return selected.Length == 1 ? selected[0] : null;
            }
        }

        private void LoadLists(string selectedListId, MetaList selectedMetaList = null)
        {
            listsListBox.BeginUpdate();
            listsListBox.Items.Clear();
            listsListBox.Items.AddRange(MetaLists);
            foreach (TodoList list in store.Lists)
            {
                listsListBox.Items.Add(list);
            }
            listsListBox.EndUpdate();

            TodoList selectedList = store.Lists.Find(list => list.Id == selectedListId) ?? store.Lists.FirstOrDefault();
            listsListBox.SelectedItem = (object)selectedMetaList ?? (object)selectedList ?? MetaLists[0];
        }

        private void LoadTasks()
        {
            tasksListBox.BeginUpdate();
            tasksListBox.Items.Clear();
            tasksListBox.SetTags(store.Tags);
            TodoList list = CurrentList;
            MetaList metaList = CurrentMetaList;
            taskViewDate = DateTime.Today;

            bool hasCompletedTasks = false;
            if (metaList != null)
            {
                listTitleLabel.Text = metaList.ToString();
                foreach (TodoItem task in GetMetaTasks(store.Lists, metaList.Caption, taskViewDate))
                {
                    AddTaskRows(task);
                }
            }
            else if (list != null)
            {
                listTitleLabel.Text = list.Name;
                List<TodoItem> completed = new List<TodoItem>();
                foreach (TodoItem item in list.Items)
                {
                    if (item.IsComplete)
                    {
                        completed.Add(item);
                    }
                    else
                    {
                        AddTaskRows(item);
                    }
                }

                hasCompletedTasks = completed.Count > 0;
                if (hasCompletedTasks)
                {
                    bool expanded = expandedCompletedLists.Contains(list.Id);
                    tasksListBox.Items.Add(new TaskListBox.CompletedTasksHeader(completed.Count, expanded));
                    if (expanded)
                    {
                        foreach (TodoItem item in completed)
                        {
                            AddTaskRows(item);
                        }
                    }
                }
            }
            else
            {
                listTitleLabel.Text = L10n.Get(TextId.SelectOrCreateList);
            }
            tasksListBox.EndUpdate();
            listsListBox.Invalidate();
            deleteCompletedTasksButton.Enabled = hasCompletedTasks;
            UpdateTaskControls();
        }

        private void AddTaskRows(TodoItem task)
        {
            tasksListBox.Items.Add(task);
            if (tasksWithExpandedSubtasks.Contains(task.Id))
            {
                foreach (TodoSubtask subtask in task.Subtasks)
                {
                    tasksListBox.Items.Add(new TaskListBox.SubtaskRow(task, subtask));
                }
            }
        }

        private void TasksListBox_SubtasksToggled(TodoItem task)
        {
            int row = tasksListBox.Items.IndexOf(task);
            int top = tasksListBox.TopIndex;
            TodoItem[] selected = tasksListBox.SelectedTasks();
            if (tasksWithExpandedSubtasks.Remove(task.Id))
            {
                if (top > row)
                {
                    top = Math.Max(row, top - task.Subtasks.Count);
                }
            }
            else
            {
                tasksWithExpandedSubtasks.Add(task.Id);
                if (top > row)
                {
                    top += task.Subtasks.Count;
                }
            }

            LoadTasks();
            RestoreTaskSelection(selected, top);
        }

        private void TasksListBox_CompletedSectionToggled(object sender, EventArgs e)
        {
            string listId = CurrentList.Id;
            if (!expandedCompletedLists.Remove(listId))
            {
                expandedCompletedLists.Add(listId);
            }

            int headerIndex = tasksListBox.SelectedIndex;
            int topIndex = tasksListBox.TopIndex;
            LoadTasks();
            tasksListBox.SelectedIndex = headerIndex;
            tasksListBox.TopIndex = Math.Min(topIndex, headerIndex);
        }

        private void UpdateTaskControls()
        {
            bool hasList = CurrentList != null;
            int selectedCount = tasksListBox.SelectedTasks().Length;
            bool subtaskSelected = tasksListBox.SelectedSubtask != null;
            renameListButton.Enabled = deleteListButton.Enabled = hasList;
            addTaskButton.Enabled = store.Lists.Count > 0;
            editTaskButton.Enabled = subtaskSelected || selectedCount == 1;
            deleteTaskButton.Enabled = subtaskSelected || selectedCount > 0;
            SetActionCaption(editTaskButton, subtaskSelected ? TextId.EditSubtask : TextId.EditTask, TextId.EditTask);
            SetActionCaption(deleteTaskButton, subtaskSelected ? TextId.DeleteSubtask : TextId.DeleteTask, TextId.DeleteTask);
        }

        private void ListsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadTasks();
        }

        private void ListsListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            ThemeStyle theme = ThemeStyle.Current;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool themedSelection = selected && theme != null;
            if (themedSelection)
            {
                ThemeStyle.PaintSelection(e.Graphics, e.Bounds);
            }
            else
            {
                e.DrawBackground();
            }
            if (e.Index < 0 || e.Index >= listsListBox.Items.Count)
            {
                return;
            }

            object row = listsListBox.Items[e.Index];
            TodoList list = row as TodoList;
            Color textColor = themedSelection ? theme.Ink : e.ForeColor;
            const TextFormatFlags textFlags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

            Rectangle iconBounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + 4, 16, 16);
            Image image = ListIconCatalog.GetImage(list == null ? ((MetaList)row).IconKey : list.IconKey);
            if (image.Size == iconBounds.Size)
            {
                e.Graphics.DrawImageUnscaled(image, iconBounds.Location);
            }
            else
            {
                e.Graphics.DrawImage(image, iconBounds);
            }

            int nameRight = e.Bounds.Right - 4;
            if (list != null && list.Items.Count > 0)
            {
                Color countColor = SystemInformation.HighContrast || (selected && !themedSelection)
                    ? textColor : Color.FromArgb(96, 96, 96);
                int taskCount = store.CountOnlyIncompleteTasks ? list.Items.Count(item => !item.IsComplete) : list.Items.Count;
                string count = taskCount.ToString();
                int countWidth = TextRenderer.MeasureText(e.Graphics, count, e.Font, e.Bounds.Size, textFlags).Width;
                Rectangle countBounds = new Rectangle(nameRight - countWidth, e.Bounds.Top, countWidth, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, count, e.Font, countBounds, countColor, textFlags | TextFormatFlags.Right);
                nameRight = countBounds.Left - 6;
            }

            Rectangle nameBounds = Rectangle.FromLTRB(e.Bounds.Left + 27, e.Bounds.Top, nameRight, e.Bounds.Bottom);
            if (nameBounds.Width > 0)
            {
                TextRenderer.DrawText(e.Graphics, row.ToString() ?? String.Empty, e.Font, nameBounds, textColor,
                    textFlags | TextFormatFlags.EndEllipsis);
            }
            ThemeStyle.DrawRowFocus(e, themedSelection, textColor);
            if (e.Index == MetaLists.Length - 1)
            {
                ThemeStyle.PaintBottomBorder(e.Graphics, e.Bounds, theme == null ? SystemColors.ControlDark : theme.Border);
            }
        }

        private void NewListButton_Click(object sender, EventArgs e)
        {
            using (TextPromptForm dialog = new TextPromptForm(L10n.Get(TextId.NewList), L10n.Get(TextId.ListName), String.Empty, ListIconCatalog.DefaultIconKey))
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

        private void SearchButton_Click(object sender, EventArgs e)
        {
            using (TaskSearchForm dialog = new TaskSearchForm(store))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var result = dialog.SelectedResult;
                    listsListBox.SelectedItem = result.List;
                    SelectTask(result.Task, result.Subtask);
                    tasksListBox.Focus();
                }
            }
        }

        private void ShowAbout()
        {
            // Windows XP requires a writable buffer when the caption and heading are separated by #.
            StringBuilder title = new StringBuilder(L10n.Get(TextId.About) + "#" + L10n.Get(TextId.AboutTitle));
            ShellAboutW(Handle, title, L10n.Get(TextId.AboutCredits), applicationIcon.Handle);
        }

        private void OpenHelp()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "help", L10n.LanguageCode, "Taskinator.chm");
            if (!File.Exists(path))
            {
                MessageBox.Show(this, L10n.Get(TextId.HelpFileMissing) + "\r\n\r\n" + path, helpButton.Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Help.ShowHelp(this, path, HelpNavigator.TableOfContents);
        }

        private void SettingsMenuButton_Click(object sender, EventArgs e)
        {
            TodoList selectedList = CurrentList;
            MetaList selectedMetaList = CurrentMetaList;
            TodoItem selectedTask = CurrentItem;
            using (SettingsForm dialog = new SettingsForm(store))
            {
                while (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    TodoStore proposed = dialog.GetStore();
                    proposed.RemoveMissingTags();
                    bool startupChanged = proposed.StartWithWindows != store.StartWithWindows;
                    if (startupChanged && !UpdateWindowsStartup(proposed.StartWithWindows, true))
                    {
                        continue;
                    }

                    if (!TrySaveData(proposed, delegate
                    {
                        if (startupChanged)
                        {
                            UpdateWindowsStartup(store.StartWithWindows, true);
                        }
                    }))
                    {
                        continue;
                    }

                    store = proposed;
                    L10n.SetLanguage(store.Language);
                    ApplyLanguage();
                    ThemeStyle.Select(store.Theme);
                    ApplyTheme();
                    LoadLists(selectedList == null ? null : selectedList.Id, selectedMetaList);
                    if (selectedTask != null)
                    {
                        SelectTask(store.Lists.SelectMany(list => list.Items).FirstOrDefault(item => item.Id == selectedTask.Id));
                    }
                    return;
                }
            }
        }

        private bool UpdateWindowsStartup(bool enabled, bool showError)
        {
            string error;
            if (WindowsStartup.TrySetEnabled(enabled, out error))
            {
                return true;
            }

            Trace.TraceWarning("Could not update Taskinator startup registration: {0}", error);
            if (showError)
            {
                MessageBox.Show(this, L10n.Get(TextId.StartupChangeFailed) + "\r\n\r\n" + error,
                    L10n.Get(TextId.Settings), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        private void RenameListButton_Click(object sender, EventArgs e)
        {
            TodoList list = CurrentList;
            if (list == null)
            {
                return;
            }

            using (TextPromptForm dialog = new TextPromptForm(L10n.Get(TextId.RenameList), L10n.Get(TextId.ListName), list.Name, list.IconKey))
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
                L10n.Format(TextId.ConfirmDeleteList, list.Name),
                "Taskinator",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            int index = store.Lists.IndexOf(list);
            store.Lists.Remove(list);
            expandedCompletedLists.Remove(list.Id);
            SaveData();
            string nextListId = null;
            if (store.Lists.Count > 0)
            {
                nextListId = store.Lists[Math.Min(index, store.Lists.Count - 1)].Id;
            }
            LoadLists(nextListId);
        }

        private void MoveList(TodoList list, int offset)
        {
            TodoItem[] selected = list == CurrentList ? tasksListBox.SelectedTasks() : new TodoItem[0];
            int index = store.Lists.IndexOf(list);
            if (TryMove(store.Lists, index, store.Lists, index + offset))
            {
                LoadLists(list.Id);
                RestoreTaskSelection(selected);
            }
        }

        private List<TodoItem> GetSteppedTaskOrder(TodoItem[] tasks, int offset)
        {
            TodoList list = CurrentList;
            if (tasks.Length == 0 || list == null)
            {
                return null;
            }

            HashSet<TodoItem> selected = new HashSet<TodoItem>(tasks);
            List<TodoItem> order = new List<TodoItem>(list.Items);
            int previousOpen = -1;
            int previousComplete = -1;
            bool moved = false;
            // Walk from the leading edge so each selected block advances by one row in its section.
            for (int i = offset < 0 ? 0 : order.Count - 1; i >= 0 && i < order.Count; i -= offset)
            {
                TodoItem task = order[i];
                int previous = task.IsComplete ? previousComplete : previousOpen;
                if (previous >= 0 && selected.Contains(task) && !selected.Contains(order[previous]))
                {
                    order[i] = order[previous];
                    order[previous] = task;
                    moved = true;
                }
                if (task.IsComplete)
                {
                    previousComplete = i;
                }
                else
                {
                    previousOpen = i;
                }
            }
            return moved ? order : null;
        }

        private void StepSelectedTasks(int offset)
        {
            TodoItem[] selected = tasksListBox.SelectedTasks();
            SaveTaskOrder(GetSteppedTaskOrder(selected, offset), selected);
        }

        private void SaveTaskOrder(List<TodoItem> order, TodoItem[] selected)
        {
            if (order == null)
            {
                return;
            }

            TodoList list = CurrentList;
            int top = tasksListBox.TopIndex;
            List<TodoItem> previous = list.Items;
            list.Items = order;
            if (TrySaveData(store, delegate { list.Items = previous; }))
            {
                LoadTasks();
                RestoreTaskSelection(selected, top);
            }
        }

        private void MoveTasksToList(TodoItem[] tasks, TodoList destination)
        {
            if (!store.Lists.Contains(destination) || !ChangeTaskLists(tasks, destination))
            {
                return;
            }

            if (CurrentMetaList != null)
            {
                int top = tasksListBox.TopIndex;
                LoadTasks();
                RestoreTaskSelection(tasks, top);
                return;
            }

            if (tasks.Any(task => task.IsComplete))
            {
                expandedCompletedLists.Add(destination.Id);
            }
            listsListBox.SelectedItem = destination;
            RestoreTaskSelection(tasks);
        }

        private bool ChangeTaskLists(TodoItem[] tasks, TodoList destination = null)
        {
            if (tasks.Length == 0)
            {
                return false;
            }

            HashSet<TodoItem> selected = new HashSet<TodoItem>(tasks);
            Dictionary<TodoList, List<TodoItem>> previousItems = new Dictionary<TodoList, List<TodoItem>>();
            List<TodoItem> moved = new List<TodoItem>();
            TodoList currentList = CurrentList;
            IEnumerable<TodoList> sources = currentList == null ? (IEnumerable<TodoList>)store.Lists : new[] { currentList };
            foreach (TodoList list in sources)
            {
                if (list == destination)
                {
                    continue;
                }

                List<TodoItem> remaining = list.Items.FindAll(item => !selected.Contains(item));
                if (remaining.Count < list.Items.Count)
                {
                    previousItems.Add(list, list.Items);
                    if (destination != null)
                    {
                        moved.AddRange(list.Items.Where(selected.Contains));
                    }
                    list.Items = remaining;
                }
            }
            if (previousItems.Count == 0)
            {
                return false;
            }
            if (destination != null)
            {
                previousItems.Add(destination, destination.Items);
                destination.Items = new List<TodoItem>(destination.Items);
                destination.Items.AddRange(moved);
            }
            return TrySaveData(store, delegate
            {
                foreach (KeyValuePair<TodoList, List<TodoItem>> previous in previousItems)
                {
                    previous.Key.Items = previous.Value;
                }
            });
        }

        private bool TryMove<T>(List<T> source, int index, List<T> destination, int newIndex)
        {
            int destinationCount = source == destination ? source.Count - 1 : destination.Count;
            if (index < 0 || index >= source.Count || newIndex < 0 || newIndex > destinationCount ||
                (source == destination && index == newIndex))
            {
                return false;
            }

            T item = source[index];
            source.RemoveAt(index);
            destination.Insert(newIndex, item);
            return TrySaveData(store, delegate
            {
                destination.RemoveAt(newIndex);
                source.Insert(index, item);
            });
        }

        private void AddTaskButton_Click(object sender, EventArgs e)
        {
            if (store.Lists.Count == 0)
            {
                MessageBox.Show(this, L10n.Get(TextId.ListRequired), "Taskinator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TodoList list = CurrentList ?? ChooseTaskDestination();
            if (list == null)
            {
                return;
            }

            using (TaskEditorForm dialog = new TaskEditorForm(null, store.Tags))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                TodoItem item = new TodoItem();
                item.Title = dialog.TaskTitle;
                item.Description = dialog.Description;
                item.Subtasks = dialog.Subtasks;
                item.TagIds = dialog.TagIds;
                item.TitleColorIndex = dialog.TitleColorIndex;
                item.IsComplete = dialog.IsComplete;
                ReminderSchedule.Update(item, dialog.ReminderUtc, dialog.ReminderAdvanceMinutes);
                list.Items.Add(item);
                if (!TrySaveData(store, delegate { list.Items.Remove(item); }))
                {
                    return;
                }
                LoadTasks();
                if (CurrentMetaList != null && !tasksListBox.Items.Contains(item))
                {
                    listsListBox.SelectedItem = list;
                }
                SelectTask(item);
            }
        }

        private void EditTaskButton_Click(object sender, EventArgs e)
        {
            TaskListBox.SubtaskRow child = tasksListBox.SelectedSubtask;
            if (child != null)
            {
                EditSubtask(child);
                return;
            }

            TodoItem item = CurrentItem;
            if (item == null)
            {
                return;
            }

            TodoList list = CurrentList ?? store.Lists.Find(source => source.Items.Contains(item));
            if (list == null)
            {
                return;
            }

            using (TaskEditorForm dialog = new TaskEditorForm(item, store.Tags))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                TodoItem updated = item.WithTags(dialog.TagIds);
                updated.Title = dialog.TaskTitle;
                updated.Description = dialog.Description;
                updated.Subtasks = dialog.Subtasks;
                updated.TitleColorIndex = dialog.TitleColorIndex;
                updated.IsComplete = dialog.IsComplete;
                ReminderSchedule.Update(updated, dialog.ReminderUtc, dialog.ReminderAdvanceMinutes);
                int index = list.Items.IndexOf(item);
                list.Items[index] = updated;
                if (!TrySaveData(store, delegate { list.Items[index] = item; }))
                {
                    return;
                }
                LoadTasks();
                SelectTask(updated);
            }
        }

        private void DeleteTaskButton_Click(object sender, EventArgs e)
        {
            TaskListBox.SubtaskRow child = tasksListBox.SelectedSubtask;
            if (child != null)
            {
                DeleteSubtask(child);
                return;
            }

            TodoItem[] selected = tasksListBox.SelectedTasks();
            if (selected.Length == 0)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                selected.Length == 1
                    ? L10n.Format(TextId.ConfirmDeleteTask, selected[0].Title)
                    : L10n.Format(TextId.ConfirmDeleteSelectedTasks, selected.Length),
                "Taskinator",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            if (ChangeTaskLists(selected))
            {
                LoadTasks();
            }
        }

        private void EditSubtask(TaskListBox.SubtaskRow child)
        {
            using (TextPromptForm dialog = new TextPromptForm(L10n.Get(TextId.EditSubtask), child.Subtask))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                List<TodoSubtask> subtasks = child.Parent.Subtasks;
                int index = subtasks.IndexOf(child.Subtask);
                subtasks[index] = new TodoSubtask
                {
                    Title = dialog.Value,
                    Description = dialog.Description,
                    IsComplete = child.Subtask.IsComplete
                };
                if (TrySaveData(store, delegate { subtasks[index] = child.Subtask; }))
                {
                    ReloadSubtaskSelection(child.Parent, index);
                }
            }
        }

        private void DeleteSubtask(TaskListBox.SubtaskRow child)
        {
            DialogResult answer = MessageBox.Show(this, L10n.Format(TextId.ConfirmDeleteSubtask, child.Subtask.Title),
                "Taskinator", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            List<TodoSubtask> subtasks = child.Parent.Subtasks;
            int index = subtasks.IndexOf(child.Subtask);
            subtasks.RemoveAt(index);
            if (TrySaveData(store, delegate { subtasks.Insert(index, child.Subtask); }))
            {
                if (subtasks.Count == 0)
                {
                    tasksWithExpandedSubtasks.Remove(child.Parent.Id);
                }
                ReloadSubtaskSelection(child.Parent, Math.Min(index, subtasks.Count - 1));
            }
        }

        private void ReloadSubtaskSelection(TodoItem parent, int subtaskIndex)
        {
            int top = tasksListBox.TopIndex;
            LoadTasks();
            int row = tasksListBox.Items.IndexOf(parent);
            if (row >= 0)
            {
                row += 1 + subtaskIndex;
                tasksListBox.TopIndex = Math.Min(top, row);
                tasksListBox.SelectOnly(row);
            }
        }

        private void MarkSelectionComplete(bool complete)
        {
            TaskListBox.SubtaskRow child = tasksListBox.SelectedSubtask;
            if (child != null)
            {
                SetSubtaskComplete(child, complete);
            }
            else
            {
                SetTasksComplete(tasksListBox.SelectedTasks(), complete);
            }
        }

        private void SetSubtaskComplete(TaskListBox.SubtaskRow child, bool complete)
        {
            bool previous = child.Subtask.IsComplete;
            if (previous == complete)
            {
                return;
            }

            child.Subtask.IsComplete = complete;
            if (TrySaveData(store, delegate { child.Subtask.IsComplete = previous; }))
            {
                tasksListBox.Invalidate();
                if (complete && !store.MuteNonReminderSounds)
                {
                    SoundPacks.Play(store.SoundFolder, SoundPacks.TaskCompleteFile);
                }
            }
        }

        private void DeleteCompletedTasksButton_Click(object sender, EventArgs e)
        {
            TodoList list = CurrentList;
            if (list == null || !list.Items.Exists(item => item.IsComplete))
            {
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                L10n.Format(TextId.ConfirmDeleteCompletedTasks, list.Name),
                "Taskinator",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            TodoItem[] selected = tasksListBox.SelectedTasks();
            int top = tasksListBox.TopIndex;
            List<TodoItem> previousItems = list.Items;
            list.Items = previousItems.FindAll(item => !item.IsComplete);
            if (TrySaveData(store, delegate { list.Items = previousItems; }))
            {
                LoadTasks();
                RestoreTaskSelection(selected, top);
            }
        }

        private void TasksListBox_CompletionRequested(TodoItem clicked)
        {
            TodoItem[] selected = tasksListBox.SelectedTasks();
            if (clicked != null && !selected.Contains(clicked))
            {
                selected = new[] { clicked };
            }
            SetTasksComplete(selected, clicked == null ? selected.Any(item => !item.IsComplete) : !clicked.IsComplete);
        }

        private void SetTasksComplete(TodoItem[] selected, bool complete)
        {
            TodoItem[] changed = selected.Where(item => item.IsComplete != complete).ToArray();
            if (changed.Length == 0)
            {
                return;
            }

            int previousIndex = tasksListBox.Items.IndexOf(changed[0]);
            int top = tasksListBox.TopIndex;
            foreach (TodoItem task in changed)
            {
                task.IsComplete = complete;
            }
            if (!TrySaveData(store, delegate
            {
                foreach (TodoItem task in changed)
                {
                    task.IsComplete = !complete;
                }
            }))
            {
                return;
            }

            LoadTasks();
            RestoreTaskSelection(selected, top);
            if (complete)
            {
                if (!store.MuteNonReminderSounds)
                {
                    SoundPacks.Play(store.SoundFolder, SoundPacks.TaskCompleteFile);
                }
                if (selected.Length == 1)
                {
                    int nextRow = Math.Min(previousIndex, tasksListBox.Items.Count - 1);
                    if (nextRow >= 0)
                    {
                        TaskListBox.SubtaskRow child = tasksListBox.Items[nextRow] as TaskListBox.SubtaskRow;
                        if (child != null)
                        {
                            nextRow = tasksListBox.Items.IndexOf(child.Parent);
                        }
                    }
                    tasksListBox.SelectOnly(nextRow);
                }
            }
        }

        private void ReminderTimer_Tick(object sender, EventArgs e)
        {
            RefreshDatedMetaList();
            if (lastReminder.IsRunning && lastReminder.ElapsedMilliseconds < ReminderDisplayMilliseconds)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            TodoList dueList = null;
            TodoItem dueTask = null;
            ReminderKind dueKind = ReminderKind.None;
            foreach (TodoList list in store.Lists)
            {
                foreach (TodoItem task in list.Items)
                {
                    ReminderKind kind = ReminderSchedule.GetDue(task, now);
                    if (kind > dueKind || (kind != ReminderKind.None && kind == dueKind && task.ReminderUtc < dueTask.ReminderUtc))
                    {
                        dueList = list;
                        dueTask = task;
                        dueKind = kind;
                    }
                }
            }

            if (dueTask != null)
            {
                ShowReminder(dueList, dueTask, dueKind);
            }
        }

        private void ShowReminder(TodoList list, TodoItem task, ReminderKind kind)
        {
            lastReminder.Restart();
            reminderTimer.Stop();
            try
            {
                bool advance = kind == ReminderKind.Advance;
                if (advance)
                {
                    task.AdvanceReminderDelivered = true;
                }
                else
                {
                    task.MainReminderDelivered = true;
                }

                SoundPacks.Play(store.SoundFolder, advance ? SoundPacks.ReminderFile : SoundPacks.AlarmFile);
                string title = L10n.Get(advance ? TextId.AdvanceReminder : TextId.Reminder);
                string message = task.Title + "\r\n" + list.Name + "\r\n" + ReminderSchedule.FormatLocalTime(task.ReminderUtc.Value);
                if (message.Length > 255)
                {
                    message = message.Substring(0, 252) + "...";
                }
                trayIcon.ShowBalloonTip(ReminderDisplayMilliseconds, title, message, ToolTipIcon.Info);
                SaveData();
            }
            finally
            {
                if (!exitRequested)
                {
                    reminderTimer.Start();
                }
            }
        }

        private void TasksListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTaskControls();
        }

        private void SelectTask(TodoItem item, TodoSubtask subtask = null)
        {
            if (item == null)
            {
                return;
            }
            int subtaskIndex = subtask == null ? -1 : item.Subtasks.IndexOf(subtask);
            bool reload = subtaskIndex >= 0 && tasksWithExpandedSubtasks.Add(item.Id);
            TodoList list = CurrentList;
            if (list != null && item.IsComplete && expandedCompletedLists.Add(list.Id))
            {
                reload = true;
            }
            if (reload)
            {
                LoadTasks();
            }
            int row = tasksListBox.Items.IndexOf(item);
            if (row >= 0)
            {
                row += subtaskIndex + 1;
            }
            tasksListBox.SelectOnly(row);
            if (subtaskIndex >= 0 && row >= 0)
            {
                Rectangle bounds = tasksListBox.GetItemRectangle(row);
                if (bounds.Top < 0 || bounds.Bottom > tasksListBox.ClientSize.Height)
                {
                    tasksListBox.TopIndex = row;
                }
            }
        }

        private void RestoreTaskSelection(TodoItem[] tasks, int top = 0)
        {
            HashSet<TodoItem> selected = new HashSet<TodoItem>(tasks);
            tasksListBox.SetTaskSelection(selected);
            if (tasksListBox.Items.Count == 0)
            {
                return;
            }

            tasksListBox.TopIndex = Math.Min(top, tasksListBox.Items.Count - 1);
            int first = tasksListBox.SelectedIndex;
            if (first >= 0 && !tasksListBox.SelectedIndices.Cast<int>().Any(index =>
                tasksListBox.ClientRectangle.IntersectsWith(tasksListBox.GetItemRectangle(index))))
            {
                tasksListBox.TopIndex = first;
            }
        }

        private void SaveData()
        {
            TrySaveData(store);
        }

        private bool TrySaveData(TodoStore value, Action onFailure = null)
        {
            try
            {
                storage.Save(value);
                return true;
            }
            catch (Exception exception)
            {
                if (onFailure != null)
                {
                    onFailure();
                }
                MessageBox.Show(
                    this,
                    L10n.Get(TextId.SaveFailed) + "\r\n\r\n" + exception.Message,
                    "Taskinator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            exitRequested = true;
            reminderTimer.Stop();
            trayIcon.Visible = false;
            SaveData();
        }

        protected override void Dispose(bool disposing)
        {
            bool disposeResources = disposing && !resourcesDisposed;
            if (disposeResources)
            {
                resourcesDisposed = true;
                reminderTimer.Dispose();
                trayIcon.Dispose();
                trayMenu.Dispose();
                if (trayOpenImage != null)
                {
                    trayOpenImage.Dispose();
                }
                actionToolTip.Dispose();
                helpMenu.Dispose();
                listMoveMenu.Dispose();
                taskMoveMenu.Dispose();
            }
            base.Dispose(disposing);
            if (disposeResources)
            {
                themeToolbarRenderer.Dispose();
                if (applicationIcon != null)
                {
                    applicationIcon.Dispose();
                }
            }
        }

        private sealed class TaskActionsPanel : FlowLayoutPanel
        {
            protected override void OnPaintBackground(PaintEventArgs e)
            {
                ThemeStyle theme = ThemeStyle.Current;
                if (theme == null)
                {
                    base.OnPaintBackground(e);
                }
                else
                {
                    ThemeStyle.PaintHeader(e.Graphics, ClientRectangle, theme);
                }
            }
        }
    }
}
