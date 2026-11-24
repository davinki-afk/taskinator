using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Taskinator
{
    internal sealed class SettingsForm : Form
    {
        private readonly ComboBox languageComboBox;
        private readonly ComboBox themeComboBox;
        private readonly ComboBox taskCountComboBox;
        private readonly ComboBox soundFolderComboBox;
        private readonly CheckBox muteNonReminderSoundsCheckBox;
        private readonly CheckBox startupCheckBox;
        private readonly CheckBox showWindowOnStartupCheckBox;
        private static string CsvFilter
        {
            get { return L10n.Get(TextId.CsvFiles) + "|*.csv"; }
        }

        private static string XmlFilter
        {
            get { return L10n.Get(TextId.XmlFiles) + "|*.xml"; }
        }

        private readonly ListBox tagsListBox;
        private readonly Button editButton;
        private readonly Button deleteButton;
        private readonly Label transferStatus;
        private Dictionary<TextId, Keys> shortcuts;
        private List<TodoList> lists;

        public SettingsForm(TodoStore store)
        {
            ThemeStyle.StyleDialog(this);
            lists = store.Lists;
            shortcuts = KeyboardShortcuts.Load(store);
            Text = L10n.Get(TextId.Settings);
            Font = new Font("Tahoma", 8.25F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 342);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            ListBox sections = new ListBox();
            sections.AccessibleName = L10n.Get(TextId.SettingsSections);
            sections.BorderStyle = BorderStyle.None;
            sections.DrawMode = DrawMode.OwnerDrawFixed;
            sections.IntegralHeight = false;
            sections.ItemHeight = 22;
            sections.Location = new Point(12, 12);
            sections.Size = new Size(138, 284);
            sections.Items.Add(L10n.Get(TextId.General));
            sections.Items.Add(L10n.Get(TextId.Language));
            sections.Items.Add(L10n.Get(TextId.Appearance));
            sections.Items.Add(L10n.Get(TextId.Tags));
            sections.Items.Add(L10n.Get(TextId.ImportExport));
            sections.Items.Add(L10n.Get(TextId.KeyboardShortcuts));
            sections.Items.Add(L10n.Get(TextId.Sounds));
            Image[] sectionIcons =
            {
                ListIconCatalog.GetImage("041.png"),
                ListIconCatalog.GetImage("094.png"),
                ListIconCatalog.GetImage("090.png"),
                ListIconCatalog.GetImage("013.png"),
                ListIconCatalog.GetImage("096.png"),
                ListIconCatalog.GetImage("keyboard.png"),
                ListIconCatalog.GetImage("speaker.png")
            };
            sections.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                e.DrawBackground();
                if (e.Index < 0 || e.Index >= sections.Items.Count)
                {
                    return;
                }

                Rectangle iconBounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + (e.Bounds.Height - 16) / 2, 16, 16);
                e.Graphics.DrawImage(sectionIcons[e.Index], iconBounds);
                Rectangle textBounds = Rectangle.FromLTRB(iconBounds.Right + 4, e.Bounds.Top, e.Bounds.Right - 4, e.Bounds.Bottom);
                TextRenderer.DrawText(e.Graphics, sections.GetItemText(sections.Items[e.Index]), e.Font, textBounds, e.ForeColor,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
            Controls.Add(new ThemeFrame(sections));

            Panel languagePanel = new Panel { Location = new Point(162, 12), Size = new Size(306, 284) };
            Label languageLabel = new Label
            {
                Text = L10n.Get(TextId.InterfaceLanguage),
                Location = new Point(0, 5),
                AutoSize = true
            };
            languagePanel.Controls.Add(languageLabel);
            languageComboBox = new ComboBox
            {
                AccessibleName = languageLabel.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 25),
                Width = 200
            };
            languageComboBox.Items.AddRange(L10n.LanguageNames);
            languageComboBox.SelectedIndex = L10n.GetLanguageIndex(store.Language);
            languagePanel.Controls.Add(languageComboBox);
            Controls.Add(languagePanel);

            Panel generalPanel = new Panel { Bounds = languagePanel.Bounds };
            startupCheckBox = new CheckBox
            {
                Text = L10n.Get(TextId.StartWithWindows),
                AutoSize = true,
                Location = new Point(0, 5),
                Checked = store.StartWithWindows
            };
            generalPanel.Controls.Add(startupCheckBox);
            showWindowOnStartupCheckBox = new CheckBox
            {
                Text = L10n.Get(TextId.ShowWindowOnStartup),
                AutoSize = true,
                Location = new Point(18, 30),
                Checked = store.ShowWindowOnStartup,
                Enabled = startupCheckBox.Checked
            };
            startupCheckBox.CheckedChanged += delegate { showWindowOnStartupCheckBox.Enabled = startupCheckBox.Checked; };
            generalPanel.Controls.Add(showWindowOnStartupCheckBox);
            Controls.Add(generalPanel);

            Panel themePanel = new Panel { Location = languagePanel.Location, Size = languagePanel.Size };
            Label themeLabel = new Label
            {
                Text = L10n.Get(TextId.InterfaceTheme),
                Location = new Point(0, 5),
                AutoSize = true
            };
            themeComboBox = new ComboBox
            {
                AccessibleName = themeLabel.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 25),
                Width = 260
            };
            themeComboBox.Items.AddRange(ThemeStyle.GetNames());
            themeComboBox.SelectedIndex = ThemeStyle.GetIndex(store.Theme);
            themePanel.Controls.Add(themeLabel);
            themePanel.Controls.Add(themeComboBox);

            Label taskCountLabel = new Label
            {
                Text = L10n.Get(TextId.TaskCount),
                Location = new Point(0, 60),
                AutoSize = true
            };
            taskCountComboBox = new ComboBox
            {
                AccessibleName = taskCountLabel.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 80),
                Width = 260
            };
            taskCountComboBox.Items.Add(L10n.Get(TextId.CountAllTasks));
            taskCountComboBox.Items.Add(L10n.Get(TextId.CountIncompleteTasks));
            taskCountComboBox.SelectedIndex = store.CountOnlyIncompleteTasks ? 1 : 0;
            themePanel.Controls.Add(taskCountLabel);
            themePanel.Controls.Add(taskCountComboBox);
            Controls.Add(themePanel);

            Panel tagsPanel = new Panel { Location = languagePanel.Location, Size = languagePanel.Size };
            tagsListBox = new ListBox();
            tagsListBox.AccessibleName = L10n.Get(TextId.TaskTags);
            tagsListBox.BorderStyle = BorderStyle.None;
            tagsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            tagsListBox.IntegralHeight = false;
            tagsListBox.ItemHeight = 22;
            tagsListBox.Location = Point.Empty;
            tagsListBox.Size = new Size(306, 214);
            tagsListBox.DrawItem += TagsListBox_DrawItem;
            tagsListBox.DoubleClick += EditButton_Click;
            foreach (TodoTag tag in store.Tags)
            {
                tagsListBox.Items.Add(new TodoTag { Id = tag.Id, Name = tag.Name, ColorIndex = tag.ColorIndex });
            }
            tagsPanel.Controls.Add(new ThemeFrame(tagsListBox));

            tagsPanel.Controls.Add(CreateButton(L10n.Get(TextId.Add), new Point(0, 223), 96, AddButton_Click));
            editButton = CreateButton(L10n.Get(TextId.Edit), new Point(105, 223), 96, EditButton_Click);
            deleteButton = CreateButton(L10n.Get(TextId.Delete), new Point(210, 223), 96, DeleteButton_Click);
            tagsPanel.Controls.Add(editButton);
            tagsPanel.Controls.Add(deleteButton);
            tagsListBox.SelectedIndexChanged += TagsListBox_SelectedIndexChanged;
            TagsListBox_SelectedIndexChanged(this, EventArgs.Empty);
            Controls.Add(tagsPanel);

            Panel transferPanel = new Panel { Location = tagsPanel.Location, Size = tagsPanel.Size };
            transferPanel.Controls.Add(new Label
            {
                Text = L10n.Get(TextId.CsvInstructions),
                Location = new Point(0, 5),
                Size = new Size(306, 45)
            });
            transferPanel.Controls.Add(CreateButton(L10n.Get(TextId.ImportCsv), new Point(0, 54), 148,
                delegate { ImportFile(CsvFilter, TodoCsv.Read, TextId.CsvImportFailed); }));
            transferPanel.Controls.Add(CreateButton(L10n.Get(TextId.ExportCsv), new Point(158, 54), 148,
                delegate { ExportFile(CsvFilter, "csv", TodoCsv.Export, TextId.CsvSaved, TextId.CsvExportFailed); }));
            transferPanel.Controls.Add(CreateButton(L10n.Get(TextId.ImportXml), new Point(0, 85), 148,
                delegate { ImportFile(XmlFilter, Storage.ImportXml, TextId.XmlImportFailed); }));
            transferPanel.Controls.Add(CreateButton(L10n.Get(TextId.ExportXml), new Point(158, 85), 148,
                delegate { ExportFile(XmlFilter, "xml", Storage.ExportXml, TextId.XmlSaved, TextId.XmlExportFailed); }));
            transferStatus = new Label { Location = new Point(0, 122), Size = new Size(306, 48) };
            transferPanel.Controls.Add(transferStatus);
            Controls.Add(transferPanel);

            Panel shortcutsPanel = CreateShortcutsPanel(languagePanel.Bounds);
            Controls.Add(shortcutsPanel);

            Panel soundsPanel = new Panel { Bounds = languagePanel.Bounds };
            Label soundFolderLabel = new Label
            {
                Text = L10n.Get(TextId.SoundFolder),
                Location = new Point(0, 5),
                AutoSize = true
            };
            soundsPanel.Controls.Add(soundFolderLabel);
            soundFolderComboBox = new ComboBox
            {
                AccessibleName = soundFolderLabel.Text,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 25),
                Width = soundsPanel.Width
            };
            soundsPanel.Controls.Add(soundFolderComboBox);
            soundsPanel.Controls.Add(CreateButton(L10n.Get(TextId.ImportSoundFolder), new Point(0, 54), 148,
                ImportSoundFolder_Click));
            Button deleteSoundFolderButton = CreateButton(L10n.Get(TextId.DeleteSoundFolder), new Point(158, 54), 148,
                DeleteSoundFolder_Click);
            soundsPanel.Controls.Add(deleteSoundFolderButton);
            soundFolderComboBox.SelectedIndexChanged += delegate
            {
                deleteSoundFolderButton.Enabled = soundFolderComboBox.SelectedIndex > 0;
            };
            RefreshSoundFolders(store.SoundFolder);
            soundsPanel.Controls.Add(CreateButton(L10n.Get(TextId.RefreshSoundFolders), new Point(0, 85), 148,
                delegate { RefreshSoundFolders(soundFolderComboBox.SelectedItem as string); }));
            muteNonReminderSoundsCheckBox = new CheckBox
            {
                Text = L10n.Get(TextId.MuteNonReminderSounds),
                Location = new Point(0, 119),
                AutoSize = true,
                Checked = store.MuteNonReminderSounds
            };
            soundsPanel.Controls.Add(muteNonReminderSoundsCheckBox);
            soundsPanel.Controls.Add(new Label
            {
                Text = L10n.Get(TextId.SoundFolderInstructions),
                Location = new Point(0, 150),
                Size = new Size(soundsPanel.Width, 125)
            });
            Controls.Add(soundsPanel);

            Panel[] panels = { generalPanel, languagePanel, themePanel, tagsPanel, transferPanel, shortcutsPanel, soundsPanel };
            sections.SelectedIndexChanged += delegate
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    panels[i].Visible = sections.SelectedIndex == i;
                }
            };
            sections.SelectedIndex = 0;

            Button okButton = new Button { Text = L10n.Get(TextId.OK), Location = new Point(307, 305), Size = new Size(78, 25) };
            okButton.DialogResult = DialogResult.OK;
            Controls.Add(okButton);
            Button cancelButton = new Button { Text = L10n.Get(TextId.Cancel), Location = new Point(390, 305), Size = new Size(78, 25) };
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public TodoStore GetStore()
        {
            return new TodoStore
            {
                Language = L10n.GetLanguageCode(languageComboBox.SelectedIndex),
                Theme = ThemeStyle.GetKey(themeComboBox.SelectedIndex),
                CountOnlyIncompleteTasks = taskCountComboBox.SelectedIndex == 1,
                StartWithWindows = startupCheckBox.Checked,
                ShowWindowOnStartup = showWindowOnStartupCheckBox.Checked,
                SoundFolder = (string)soundFolderComboBox.SelectedItem,
                MuteNonReminderSounds = muteNonReminderSoundsCheckBox.Checked,
                ShortcutOverrides = KeyboardShortcuts.GetOverrides(shortcuts),
                // Keep the existing tasks so reminder deliveries are visible while settings are open.
                Lists = lists.ConvertAll(list => new TodoList
                {
                    Id = list.Id,
                    Name = list.Name,
                    IconKey = list.IconKey,
                    Items = new List<TodoItem>(list.Items)
                }),
                Tags = tagsListBox.Items.Cast<TodoTag>().ToList()
            };
        }

        private void RefreshSoundFolders(string selectedFolder)
        {
            List<string> folders = SoundPacks.GetFolders();
            int selectedIndex = folders.FindIndex(folder => String.Equals(folder, selectedFolder, StringComparison.OrdinalIgnoreCase));
            soundFolderComboBox.BeginUpdate();
            soundFolderComboBox.Items.Clear();
            soundFolderComboBox.Items.AddRange(folders.ToArray());
            soundFolderComboBox.SelectedIndex = Math.Max(0, selectedIndex);
            soundFolderComboBox.EndUpdate();
        }

        private void ImportSoundFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = L10n.Get(TextId.ChooseSoundFolder),
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    RefreshSoundFolders(SoundPacks.ImportFolder(dialog.SelectedPath));
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, L10n.Get(TextId.SoundImportFailed) + "\r\n\r\n" + error.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteSoundFolder_Click(object sender, EventArgs e)
        {
            string folder = soundFolderComboBox.SelectedItem as string;
            if (soundFolderComboBox.SelectedIndex <= 0 ||
                MessageBox.Show(this, L10n.Format(TextId.ConfirmDeleteSoundFolder, folder),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                SoundPacks.DeleteFolder(folder);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, L10n.Get(TextId.SoundDeleteFailed) + "\r\n\r\n" + error.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                RefreshSoundFolders(folder);
            }
        }

        private Panel CreateShortcutsPanel(Rectangle bounds)
        {
            Panel panel = new Panel { Bounds = bounds };
            string hint = L10n.Get(TextId.ShortcutInstructions);
            Label status = new Label
            {
                Text = hint,
                Location = new Point(0, 3),
                Size = new Size(bounds.Width, 32)
            };
            panel.Controls.Add(status);

            Dictionary<TextId, ShortcutBox> fields = new Dictionary<TextId, ShortcutBox>();
            int y = 36;
            foreach (TextId action in KeyboardShortcuts.Actions)
            {
                string caption = L10n.Get(action);
                panel.Controls.Add(new Label
                {
                    Text = caption,
                    Bounds = new Rectangle(0, y, 170, 20),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                });
                ShortcutBox field = new ShortcutBox(keys =>
                {
                    foreach (KeyValuePair<TextId, Keys> binding in shortcuts)
                    {
                        if (keys != Keys.None && binding.Key != action && binding.Value == keys)
                        {
                            status.Text = L10n.Format(TextId.ShortcutAlreadyAssigned, L10n.Get(binding.Key));
                            return false;
                        }
                    }

                    shortcuts[action] = keys;
                    status.Text = hint;
                    return true;
                })
                {
                    AccessibleName = caption,
                    AccessibleDescription = hint,
                    Bounds = new Rectangle(174, y, 132, 20)
                };
                field.ShowShortcut(shortcuts[action]);
                fields.Add(action, field);
                panel.Controls.Add(field);
                y += 24;
            }

            panel.Controls.Add(CreateButton(L10n.Get(TextId.ResetShortcuts), new Point(130, 255), 176, delegate
            {
                shortcuts = KeyboardShortcuts.CreateDefaults();
                foreach (KeyValuePair<TextId, ShortcutBox> field in fields)
                {
                    field.Value.ShowShortcut(shortcuts[field.Key]);
                }
                status.Text = hint;
            }));
            return panel;
        }

        private sealed class ShortcutBox : TextBox
        {
            private readonly Func<Keys, bool> assign;

            internal ShortcutBox(Func<Keys, bool> assign)
            {
                this.assign = assign;
                ReadOnly = true;
                ShortcutsEnabled = false;
            }

            internal void ShowShortcut(Keys keys)
            {
                Text = keys == Keys.None ? "—" : KeyboardShortcuts.Format(keys);
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                Keys keys = keyData == Keys.Back || keyData == Keys.Delete ? Keys.None : keyData;
                if (keys != Keys.None && !KeyboardShortcuts.IsSupported(keys))
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }

                if (assign(keys))
                {
                    ShowShortcut(keys);
                }
                return true;
            }
        }

        private static Button CreateButton(string text, Point location, int width, EventHandler click)
        {
            Button button = new Button { Text = text, Location = location, Size = new Size(width, 25) };
            button.Click += click;
            return button;
        }

        private void ImportFile(string filter, Func<string, TodoStore> read, TextId failed)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = filter, RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    StageImport(read(dialog.FileName));
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, L10n.Get(failed) + "\r\n\r\n" + error.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void StageImport(TodoStore imported)
        {
            TodoStore staged = GetStore();
            int listCount = staged.Lists.Count;
            int tagCount = staged.Tags.Count;
            int taskCount = staged.Lists.Sum(list => list.Items.Count);
            staged.AddMissing(imported);
            lists = staged.Lists;
            tagsListBox.BeginUpdate();
            tagsListBox.Items.Clear();
            tagsListBox.Items.AddRange(staged.Tags.ToArray());
            tagsListBox.EndUpdate();
            TagsListBox_SelectedIndexChanged(this, EventArgs.Empty);
            transferStatus.Text = L10n.Format(TextId.ImportCounts,
                staged.Lists.Count - listCount, staged.Lists.Sum(list => list.Items.Count) - taskCount, staged.Tags.Count - tagCount);
        }

        private void ExportFile(string filter, string extension, Action<string, TodoStore> export, TextId saved, TextId failed)
        {
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = "Taskinator." + extension,
                DefaultExt = extension,
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    TodoStore snapshot = GetStore();
                    snapshot.RemoveMissingTags();
                    export(dialog.FileName, snapshot);
                    transferStatus.Text = L10n.Get(saved);
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, L10n.Get(failed) + "\r\n\r\n" + error.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TagsListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < tagsListBox.Items.Count)
            {
                Rectangle bounds = e.Bounds;
                bounds.Inflate(-5, 0);
                TagPalette.DrawTag(e.Graphics, (TodoTag)tagsListBox.Items[e.Index], e.Font, bounds, e.ForeColor, e.BackColor);
                e.DrawFocusRectangle();
            }
        }

        private void TagsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            editButton.Enabled = deleteButton.Enabled = tagsListBox.SelectedIndex >= 0;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (TagEditorForm dialog = new TagEditorForm(null))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    TodoTag tag = new TodoTag { Name = dialog.TagName, ColorIndex = dialog.ColorIndex };
                    tagsListBox.SelectedIndex = tagsListBox.Items.Add(tag);
                }
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            TodoTag tag = tagsListBox.SelectedItem as TodoTag;
            if (tag == null)
            {
                return;
            }

            using (TagEditorForm dialog = new TagEditorForm(tag))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    int index = tagsListBox.SelectedIndex;
                    tag.Name = dialog.TagName;
                    tag.ColorIndex = dialog.ColorIndex;
                    tagsListBox.Items[index] = tag;
                    tagsListBox.SelectedIndex = index;
                }
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            TodoTag tag = tagsListBox.SelectedItem as TodoTag;
            if (tag == null)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(this, L10n.Format(TextId.ConfirmDeleteTag, tag.Name),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
            {
                int index = tagsListBox.SelectedIndex;
                tagsListBox.Items.RemoveAt(index);
                tagsListBox.SelectedIndex = Math.Min(index, tagsListBox.Items.Count - 1);
            }
        }
    }
}
