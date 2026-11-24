using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Taskinator
{
    internal sealed class TaskListBox : BufferedListBox
    {
        private const int LeftButtonDownMessage = 0x0201;
        private const int LeftButtonUpMessage = 0x0202;
        private const int TextInset = 28;
        private const int DisclosureWidth = 14;
        private const int SubtaskIndent = 20;
        private const int VerticalPadding = 6;
        private const int TagGap = 12;
        private const int AlternateRowBrightness = 95;
        private const TextFormatFlags RowTextFlags = TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private readonly Dictionary<string, TodoTag> tagsById = new Dictionary<string, TodoTag>();
        private readonly Dictionary<TodoTag, int> tagWidths = new Dictionary<TodoTag, int>();
        private TodoItem pendingClick;
        private bool updatingSelection;

        public TaskListBox()
        {
            AccessibleName = L10n.Get(TextId.Tasks);
            BorderStyle = BorderStyle.None;
            DisplayMember = "Title";
            DrawMode = DrawMode.OwnerDrawVariable;
            IntegralHeight = false;
        }

        public event Action<TodoItem> CompletionRequested;
        public event Action<SubtaskRow> SubtaskCompletionRequested;
        public event EventHandler CompletedSectionToggled;
        public event Action<TodoItem> SubtasksToggled;
        public event EventHandler TaskActivated;

        internal string SearchQuery { get; set; }
        internal bool HighlightReminder { get; set; }

        internal sealed class SearchResult
        {
            internal SearchResult(TodoList list, TodoItem task, TodoSubtask subtask = null)
            {
                List = list;
                Task = task;
                Subtask = subtask;
            }

            internal TodoList List { get; private set; }
            internal TodoItem Task { get; private set; }
            internal TodoSubtask Subtask { get; private set; }
            public string Title { get { return Subtask == null ? Task.Title : Subtask.Title; } }
        }

        internal sealed class CompletedTasksHeader
        {
            public string Title { get; private set; }
            internal bool Expanded { get; private set; }

            internal CompletedTasksHeader(int count, bool expanded)
            {
                Title = L10n.Format(TextId.CompletedTasks, count);
                Expanded = expanded;
            }
        }

        internal sealed class SubtaskRow
        {
            internal SubtaskRow(TodoItem parent, TodoSubtask subtask)
            {
                Parent = parent;
                Subtask = subtask;
            }

            internal TodoItem Parent { get; private set; }
            internal TodoSubtask Subtask { get; private set; }
            public string Title { get { return Subtask.Title; } }
        }

        internal int LastTaskRow(int index)
        {
            return HasExpandedSubtasks(index) ? index + TaskAt(index).Subtasks.Count : index;
        }

        private bool HasExpandedSubtasks(int index)
        {
            return index + 1 < Items.Count && Items[index + 1] is SubtaskRow;
        }

        internal TodoItem TaskAt(int index)
        {
            if (index < 0 || index >= Items.Count)
            {
                return null;
            }

            SearchResult result = Items[index] as SearchResult;
            return result == null ? Items[index] as TodoItem : result.Task;
        }

        internal TodoItem[] SelectedTasks()
        {
            return SelectedItems.OfType<TodoItem>().ToArray();
        }

        internal SubtaskRow SelectedSubtask
        {
            get { return SelectedItems.Count == 1 ? SelectedItem as SubtaskRow : null; }
        }

        internal void SelectOnly(int index)
        {
            ClearSelected();
            if (index >= 0)
            {
                SelectedIndex = index;
            }
        }

        internal void SelectAllTasks()
        {
            SetTaskSelection(null);
        }

        internal void SetTaskSelection(HashSet<TodoItem> tasks)
        {
            updatingSelection = true;
            BeginUpdate();
            try
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    TodoItem task = Items[i] as TodoItem;
                    bool select = task != null && (tasks == null || tasks.Contains(task));
                    if (GetSelected(i) != select)
                    {
                        SetSelected(i, select);
                    }
                }
            }
            finally
            {
                updatingSelection = false;
                EndUpdate();
            }
            OnSelectedIndexChanged(EventArgs.Empty);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            if (!updatingSelection)
            {
                base.OnSelectedIndexChanged(e);
            }
        }

        public void SetTags(IEnumerable<TodoTag> tags)
        {
            tagsById.Clear();
            tagWidths.Clear();
            foreach (TodoTag tag in tags)
            {
                if (!String.IsNullOrEmpty(tag.Id))
                {
                    tagsById[tag.Id] = tag;
                }
            }
        }

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            int lines = 1;
            TodoItem task = TaskAt(e.Index);
            object row = e.Index >= 0 && e.Index < Items.Count ? Items[e.Index] : null;
            SearchResult result = row as SearchResult;
            SubtaskRow child = row as SubtaskRow;
            TodoSubtask subtask = result != null ? result.Subtask : child == null ? null : child.Subtask;
            if (subtask != null)
            {
                lines += String.IsNullOrWhiteSpace(subtask.Description) ? 0 : 1;
                if (result != null)
                {
                    lines += task.ReminderUtc.HasValue ? 2 : 1;
                }
            }
            else if (task != null)
            {
                lines += HasDescription(task) ? 1 : 0;
                lines += task.Subtasks.Count > 0 ? 1 : 0;
                lines += GetTags(task).Any() ? 1 : 0;
                lines += task.ReminderUtc.HasValue ? 1 : 0;
                lines += result != null ? 1 : 0;
            }
            e.ItemHeight = Font.Height * lines + VerticalPadding * 2;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            tagWidths.Clear();
            base.OnFontChanged(e);
        }

        protected override void PaintRow(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
            {
                e.DrawBackground();
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) != 0;
            TodoItem task = TaskAt(e.Index);
            if (task == null && selected && SelectedIndices.Count > 1)
            {
                e = new DrawItemEventArgs(e.Graphics, e.Font, e.Bounds, e.Index,
                    e.State & ~DrawItemState.Selected, ForeColor, BackColor);
                selected = false;
            }
            bool highContrast = SystemInformation.HighContrast;
            ThemeStyle theme = ThemeStyle.Current;
            bool themedSelection = selected && theme != null;
            Color rowBackground = DrawRowBackground(e, selected, themedSelection, highContrast);
            Color titleColor = themedSelection ? theme.Ink : e.ForeColor;
            SearchResult result = Items[e.Index] as SearchResult;
            SubtaskRow child = Items[e.Index] as SubtaskRow;
            if (result != null && result.Subtask != null)
            {
                DrawSubtask(e, result.Subtask, titleColor, rowBackground, result);
            }
            else if (child != null)
            {
                DrawSubtask(e, child.Subtask, titleColor, rowBackground);
            }
            else if (task == null)
            {
                DrawCompletedHeader(e, (CompletedTasksHeader)Items[e.Index], titleColor);
            }
            else
            {
                DrawTask(e, task, result == null ? null : result.List, titleColor, rowBackground, selected || highContrast);
            }
            ThemeStyle.DrawRowFocus(e, themedSelection, titleColor);
            base.PaintRow(e);
        }

        private static void DrawCompletedHeader(DrawItemEventArgs e, CompletedTasksHeader header, Color color)
        {
            ThemeStyle theme = ThemeStyle.Current;
            using (Pen separator = new Pen(theme == null ? SystemColors.ControlDark : theme.Border))
            {
                e.Graphics.DrawLine(separator, e.Bounds.Left, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Top);
            }
            int x = e.Bounds.Left + TextInset / 2;
            int y = e.Bounds.Top + e.Bounds.Height / 2;
            DrawDisclosureArrow(e.Graphics, x, y, header.Expanded, color);
            TextRenderer.DrawText(e.Graphics, header.Title, e.Font, GetTextBounds(e), color, RowTextFlags);
        }

        private static void DrawDisclosureArrow(Graphics graphics, int x, int y, bool expanded, Color color)
        {
            Point[] arrow = expanded
                ? new[] { new Point(x - 4, y - 2), new Point(x + 4, y - 2), new Point(x, y + 3) }
                : new[] { new Point(x - 2, y - 4), new Point(x - 2, y + 4), new Point(x + 3, y) };
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPolygon(brush, arrow);
            }
        }

        private void DrawSubtask(DrawItemEventArgs e, TodoSubtask subtask, Color color, Color background,
            SearchResult result = null)
        {
            Rectangle bounds = GetTextBounds(e);
            bounds.X += SubtaskIndent;
            bounds.Width = Math.Max(0, bounds.Width - SubtaskIndent);
            CheckBoxState state = subtask.IsComplete ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
            Rectangle checkBox = GetSubtaskCheckBoxBounds(e.Graphics, e.Bounds, e.Font.Height);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBox.Location, state);
            using (Font completedFont = subtask.IsComplete ? new Font(e.Font, e.Font.Style | FontStyle.Strikeout) : null)
            {
                Font font = completedFont ?? e.Font;
                DrawTaskLine(e.Graphics, subtask.Title, SearchQuery, font, bounds, color, background);
                if (!String.IsNullOrWhiteSpace(subtask.Description))
                {
                    bounds.Offset(0, bounds.Height);
                    DrawTaskLine(e.Graphics, SingleLinePreview(subtask.Description), SearchQuery, font, bounds, color, background);
                }
            }
            if (result != null)
            {
                if (result.Task.ReminderUtc.HasValue)
                {
                    bounds.Offset(0, bounds.Height);
                    DrawReminder(e.Graphics, result.Task, e.Font, bounds, color, background);
                }
                bounds.Offset(0, bounds.Height);
                DrawListContext(e, result.List, result.List.Name + " › " + result.Task.Title, bounds, color);
            }
        }

        private void DrawTask(DrawItemEventArgs e, TodoItem task, TodoList list, Color titleColor, Color rowBackground, bool selectedOrHighContrast)
        {
            CheckBoxState state = task.IsComplete ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
            Rectangle checkBox = GetCheckBoxBounds(e.Graphics, e.Bounds);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBox.Location, state);

            // Choose a readable gray for both light rows and dark system selections.
            if (task.IsComplete && !SystemInformation.HighContrast)
            {
                titleColor = rowBackground.GetBrightness() < 0.5F ? Color.Gainsboro : Color.DimGray;
            }
            using (Font completedFont = task.IsComplete ? new Font(e.Font, e.Font.Style | FontStyle.Strikeout) : null)
            {
                Color descriptionColor = task.IsComplete || selectedOrHighContrast ? titleColor : Color.DimGray;
                DrawTaskText(e, task, list, completedFont ?? e.Font, titleColor, descriptionColor, rowBackground);
            }
        }

        private static Rectangle GetTextBounds(DrawItemEventArgs e)
        {
            return new Rectangle(
                e.Bounds.Left + TextInset,
                e.Bounds.Top + VerticalPadding,
                Math.Max(0, e.Bounds.Width - TextInset - 6),
                e.Font.Height);
        }

        private void DrawTaskText(DrawItemEventArgs e, TodoItem task, TodoList list, Font font, Color titleColor, Color descriptionColor, Color rowBackground)
        {
            Rectangle textBounds = GetTextBounds(e);
            int lineHeight = textBounds.Height;
            DrawTaskLine(e.Graphics, task.Title ?? String.Empty, SearchQuery, font, textBounds,
                titleColor, rowBackground, task.IsComplete ? -1 : task.TitleColorIndex);
            if (HasDescription(task))
            {
                textBounds.Offset(0, lineHeight);
                DrawTaskLine(e.Graphics, GetDescription(task), SearchQuery, font, textBounds, descriptionColor, rowBackground);
            }
            if (task.Subtasks.Count > 0)
            {
                textBounds.Offset(0, lineHeight);
                string progress = L10n.Format(TextId.SubtaskProgress,
                    task.Subtasks.Count(subtask => subtask.IsComplete), task.Subtasks.Count);
                Rectangle progressBounds = textBounds;
                if (list == null)
                {
                    DrawDisclosureArrow(e.Graphics, progressBounds.Left + 4, progressBounds.Top + lineHeight / 2,
                        HasExpandedSubtasks(e.Index), titleColor);
                    progressBounds.X += DisclosureWidth;
                    progressBounds.Width = Math.Max(0, progressBounds.Width - DisclosureWidth);
                }
                TextRenderer.DrawText(e.Graphics, progress, e.Font, progressBounds, descriptionColor, RowTextFlags);
            }
            TodoTag[] tags = GetTags(task).ToArray();
            if (tags.Length > 0)
            {
                textBounds.Offset(0, lineHeight);
                DrawTags(e, tags, font, textBounds, titleColor, rowBackground, !task.IsComplete);
            }
            if (task.ReminderUtc.HasValue)
            {
                textBounds.Offset(0, lineHeight);
                DrawReminder(e.Graphics, task, font, textBounds, titleColor, rowBackground);
            }
            if (list != null)
            {
                textBounds.Offset(0, lineHeight);
                DrawListContext(e, list, list.Name, textBounds, descriptionColor);
            }
        }

        private void DrawReminder(Graphics graphics, TodoItem task, Font font, Rectangle bounds, Color color, Color background)
        {
            string time = ReminderSchedule.FormatLocalTime(task.ReminderUtc.Value);
            string reminder = task.ReminderAdvanceMinutes > 0
                ? L10n.Format(TextId.ReminderAtWithAdvance, time, ReminderSchedule.FormatAdvance(task.ReminderAdvanceMinutes))
                : L10n.Format(TextId.ReminderAt, time);
            DrawTaskLine(graphics, reminder, HighlightReminder ? time : null, font, bounds, color, background);
        }

        private static void DrawListContext(DrawItemEventArgs e, TodoList list, string text, Rectangle bounds, Color color)
        {
            if (bounds.Width > 20)
            {
                int iconSize = Math.Min(16, bounds.Height);
                e.Graphics.DrawImage(ListIconCatalog.GetImage(list.IconKey),
                    new Rectangle(bounds.Left, bounds.Top, iconSize, iconSize));
                bounds = Rectangle.FromLTRB(bounds.Left + iconSize + 4, bounds.Top, bounds.Right, bounds.Bottom);
                TextRenderer.DrawText(e.Graphics, text ?? String.Empty, e.Font, bounds, color, RowTextFlags);
            }
        }

        internal static int FindSearchText(string text, string query, int startIndex = 0)
        {
            return String.IsNullOrEmpty(text) || String.IsNullOrEmpty(query) ? -1 :
                L10n.Culture.CompareInfo.IndexOf(text, query, startIndex, CompareOptions.IgnoreCase);
        }

        private static void DrawTaskLine(Graphics graphics, string text, string query, Font font, Rectangle bounds,
            Color textColor, Color background, int colorIndex = -1, bool isTag = false)
        {
            if (bounds.Width <= 0)
            {
                return;
            }

            TextFormatFlags flags = isTag ? RowTextFlags | TextFormatFlags.VerticalCenter : RowTextFlags;
            int match = FindSearchText(text, query);
            int textWidth = bounds.Width;
            if (match >= 0 && MeasureTextWidth(graphics, text, font) > bounds.Width)
            {
                int ellipsisWidth = MeasureTextWidth(graphics, "…", font);
                textWidth = Math.Max(0, bounds.Width - ellipsisWidth);
                int firstMatchEnd = Math.Min(match + query.Length, text.Length);
                if (match > 0 && MeasureTextWidth(graphics, text.Substring(0, firstMatchEnd), font) > textWidth)
                {
                    // Bring the first match into view when the normal ellipsis would hide it.
                    text = "…" + text.Substring(match);
                    match = 1;
                    if (MeasureTextWidth(graphics, text, font) <= bounds.Width)
                    {
                        textWidth = bounds.Width;
                    }
                }
            }

            TagPalette.DrawColoredText(graphics, text, font, bounds, colorIndex, textColor, background, flags, isTag);
            while (match >= 0)
            {
                int end = Math.Min(match + query.Length, text.Length);
                int left = MeasureTextWidth(graphics, text.Substring(0, match), font);
                if (left >= textWidth)
                {
                    break;
                }
                int right = Math.Min(textWidth, MeasureTextWidth(graphics, text.Substring(0, end), font));
                if (right > left)
                {
                    Rectangle matchBounds = new Rectangle(bounds.Left + left, bounds.Top, right - left, bounds.Height);
                    GraphicsState state = graphics.Save();
                    try
                    {
                        // Redraw the full line through the match bounds to keep its text layout.
                        graphics.SetClip(matchBounds, CombineMode.Intersect);
                        Color highlight = SystemInformation.HighContrast ? SystemColors.Info : Color.FromArgb(255, 223, 111);
                        Color highlightText = SystemInformation.HighContrast ? SystemColors.InfoText : Color.Black;
                        using (SolidBrush brush = new SolidBrush(highlight))
                        {
                            graphics.FillRectangle(brush, matchBounds);
                        }
                        TextRenderer.DrawText(graphics, text, font, bounds, highlightText, flags | TextFormatFlags.PreserveGraphicsClipping);
                    }
                    finally
                    {
                        graphics.Restore(state);
                    }
                }
                match = end < text.Length ? FindSearchText(text, query, end) : -1;
            }
        }

        private static int MeasureTextWidth(Graphics graphics, string text, Font font)
        {
            return text.Length == 0 ? 0 : TextRenderer.MeasureText(graphics, text, font,
                new Size(int.MaxValue, int.MaxValue), RowTextFlags & ~TextFormatFlags.EndEllipsis).Width;
        }

        private static Color DrawRowBackground(DrawItemEventArgs e, bool selected, bool themedSelection, bool highContrast)
        {
            if (themedSelection)
            {
                ThemeStyle.PaintSelection(e.Graphics, e.Bounds);
                return ThemeStyle.Selection;
            }

            Color background = e.BackColor;
            if (selected || highContrast || e.Index % 2 == 0)
            {
                e.DrawBackground();
                return background;
            }

            Color alternate = Color.FromArgb(
                background.R * AlternateRowBrightness / 100,
                background.G * AlternateRowBrightness / 100,
                background.B * AlternateRowBrightness / 100);
            using (SolidBrush brush = new SolidBrush(alternate))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            return alternate;
        }

        private void DrawTags(DrawItemEventArgs e, TodoTag[] tags, Font font, Rectangle bounds,
            Color textColor, Color background, bool useTagColor)
        {
            int[] widths = Array.ConvertAll(tags, tag => GetTagWidth(e, tag));
            if (tags.Length > 1 && widths.Sum() + TagGap * (tags.Length - 1) > bounds.Width)
            {
                if (!String.IsNullOrEmpty(SearchQuery))
                {
                    tags = tags.OrderByDescending(tag => FindSearchText(tag.Name, SearchQuery) >= 0).ToArray();
                    widths = Array.ConvertAll(tags, tag => GetTagWidth(e, tag));
                }
                int overflowWidth = Math.Min(bounds.Width, e.Font.Height + 2);
                Rectangle overflow = new Rectangle(bounds.Right - overflowWidth, bounds.Top, overflowWidth, bounds.Height);
                TextRenderer.DrawText(e.Graphics, "…", font, overflow, textColor, RowTextFlags);
                bounds.Width -= overflowWidth;
            }

            int right = bounds.Right;
            for (int i = 0; i < tags.Length && right - bounds.Left > TagPalette.TextInset; i++)
            {
                bounds.Width = Math.Min(widths[i], right - bounds.Left);
                TodoTag tag = tags[i];
                if (FindSearchText(tag.Name, SearchQuery) >= 0)
                {
                    Rectangle textBounds = TagPalette.DrawSwatch(e.Graphics, bounds, tag.ColorIndex);
                    DrawTaskLine(e.Graphics, tag.ToString(), SearchQuery, font, textBounds,
                        textColor, background, useTagColor ? TagPalette.ValidIndex(tag.ColorIndex) : -1, true);
                }
                else
                {
                    TagPalette.DrawTag(e.Graphics, tag, font, bounds, textColor, background, useTagColor);
                }
                bounds.X += bounds.Width + TagGap;
            }
        }

        private int GetTagWidth(DrawItemEventArgs e, TodoTag tag)
        {
            int width;
            if (!tagWidths.TryGetValue(tag, out width))
            {
                width = TagPalette.MeasureTag(e.Graphics, tag, e.Font);
                tagWidths.Add(tag, width);
            }
            return width;
        }

        private static bool HasDescription(TodoItem task)
        {
            return !String.IsNullOrWhiteSpace(task.Description);
        }

        internal static string GetDescription(TodoItem task)
        {
            return SingleLinePreview(task.Description);
        }

        internal static string SingleLinePreview(string text)
        {
            return (text ?? String.Empty).Trim().Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        }

        private IEnumerable<TodoTag> GetTags(TodoItem task)
        {
            foreach (string id in task.TagIds)
            {
                TodoTag tag;
                if (id != null && tagsById.TryGetValue(id, out tag))
                {
                    yield return tag;
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (SelectionMode == SelectionMode.MultiExtended &&
                (m.Msg == LeftButtonDownMessage || (m.Msg == LeftButtonUpMessage && pendingClick != null)))
            {
                int position = m.LParam.ToInt32();
                Point location = new Point(unchecked((short)position), unchecked((short)(position >> 16)));
                Rectangle bounds;
                int index = GetRowIndexAt(location, out bounds);
                TodoItem task = TaskAt(index);

                // Native list boxes collapse an extended selection on mouse down.
                // Keep it until mouse up so dragging or clicking a checkbox can use the whole selection.
                if (m.Msg == LeftButtonDownMessage && ModifierKeys == Keys.None && task != null &&
                    GetSelected(index) && SelectedIndices.Count > 1)
                {
                    Focus();
                    pendingClick = task;
                    Capture = true;
                    OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0));
                    return;
                }
                if (m.Msg == LeftButtonUpMessage)
                {
                    TodoItem pressed = pendingClick;
                    pendingClick = null;
                    Capture = false;
                    MouseEventArgs args = new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, 0);
                    OnMouseUp(args);
                    if (task == pressed)
                    {
                        if (IsSubtaskToggleHit(index, bounds, location))
                        {
                            ToggleSubtasks(task);
                        }
                        else if (IsCheckBoxHit(bounds, location))
                        {
                            ToggleRow(index);
                        }
                        else
                        {
                            SelectOnly(index);
                        }
                    }
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture)
            {
                pendingClick = null;
            }
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left ||
                (SelectionMode == SelectionMode.MultiExtended && ModifierKeys != Keys.None))
            {
                return;
            }

            Rectangle rowBounds;
            int index = GetRowIndexAt(e.Location, out rowBounds);
            if (index == NoMatches)
            {
                return;
            }
            if (IsSubtaskToggleHit(index, rowBounds, e.Location))
            {
                ToggleSubtasks(TaskAt(index));
            }
            else if (Items[index] is CompletedTasksHeader || (Items[index] is SubtaskRow
                ? IsSubtaskCheckBoxHit(rowBounds, e.Location)
                : TaskAt(index) != null && IsCheckBoxHit(rowBounds, e.Location)))
            {
                ToggleRow(index);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || (SelectionMode == SelectionMode.MultiExtended && ModifierKeys != Keys.None))
            {
                return;
            }

            Rectangle rowBounds;
            int index = GetRowIndexAt(e.Location, out rowBounds);
            if (index < 0)
            {
                return;
            }
            if (Items[index] is SubtaskRow)
            {
                if (!IsSubtaskCheckBoxHit(rowBounds, e.Location))
                {
                    SelectOnly(index);
                    ActivateSelectedRow();
                }
            }
            else if (TaskAt(index) != null && !IsCheckBoxHit(rowBounds, e.Location) &&
                !IsSubtaskToggleHit(index, rowBounds, e.Location))
            {
                ActivateSelectedRow();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int index = SelectedIndex;
            TodoItem task = index < 0 ? null : Items[index] as TodoItem;
            SubtaskRow child = SelectedSubtask;
            if (SelectionMode == SelectionMode.MultiExtended && e.KeyData == (Keys.Control | Keys.A))
            {
                e.SuppressKeyPress = true;
                SelectAllTasks();
            }
            else if (task != null && task.Subtasks.Count > 0 &&
                ((e.KeyData == Keys.Right && !HasExpandedSubtasks(index)) ||
                    (e.KeyData == Keys.Left && HasExpandedSubtasks(index))))
            {
                e.SuppressKeyPress = true;
                ToggleSubtasks(task);
            }
            else if (child != null && e.KeyData == Keys.Left)
            {
                e.SuppressKeyPress = true;
                SelectOnly(Items.IndexOf(child.Parent));
                ToggleSubtasks(child.Parent);
            }
            else if (child != null && e.KeyData == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ActivateSelectedRow();
            }
            else if (e.KeyData == Keys.Space && index != NoMatches)
            {
                e.SuppressKeyPress = true;
                Action<TodoItem> requested = CompletionRequested;
                if (requested != null && SelectedItems.OfType<TodoItem>().Any())
                {
                    requested(null);
                }
                else if (child != null || Items[index] is CompletedTasksHeader)
                {
                    ToggleRow(index);
                }
            }
            base.OnKeyDown(e);
        }

        private static Rectangle GetCheckBoxBounds(Graphics graphics, Rectangle rowBounds)
        {
            Size size = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);
            return new Rectangle(
                rowBounds.Left + (TextInset - size.Width) / 2,
                rowBounds.Top + (rowBounds.Height - size.Height) / 2,
                size.Width,
                size.Height);
        }

        private static Rectangle GetSubtaskCheckBoxBounds(Graphics graphics, Rectangle rowBounds, int fontHeight)
        {
            Size size = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);
            return new Rectangle(rowBounds.Left + TextInset + SubtaskIndent - size.Width - 5,
                rowBounds.Top + VerticalPadding + (fontHeight - size.Height) / 2, size.Width, size.Height);
        }

        private int GetRowIndexAt(Point location, out Rectangle rowBounds)
        {
            int index = IndexFromPoint(location);
            if (index < 0 || index >= Items.Count)
            {
                rowBounds = Rectangle.Empty;
                return NoMatches;
            }

            rowBounds = GetItemRectangle(index);
            return rowBounds.Contains(location) ? index : NoMatches;
        }

        internal bool IsCheckBoxHit(Rectangle rowBounds, Point location)
        {
            using (Graphics graphics = CreateGraphics())
            {
                Rectangle bounds = GetCheckBoxBounds(graphics, rowBounds);
                bounds.Inflate(3, 3);
                return bounds.Contains(location);
            }
        }

        private bool IsSubtaskCheckBoxHit(Rectangle rowBounds, Point location)
        {
            using (Graphics graphics = CreateGraphics())
            {
                Rectangle bounds = GetSubtaskCheckBoxBounds(graphics, rowBounds, Font.Height);
                bounds.Inflate(3, 3);
                return bounds.Contains(location);
            }
        }

        internal bool IsSubtaskToggleHit(int index, Rectangle bounds, Point location)
        {
            TodoItem task = Items[index] as TodoItem;
            if (task == null || task.Subtasks.Count == 0)
            {
                return false;
            }
            int top = bounds.Top + VerticalPadding + (HasDescription(task) ? Font.Height * 2 : Font.Height);
            return location.X >= bounds.Left + TextInset && location.Y >= top && location.Y < top + Font.Height;
        }

        private void ToggleSubtasks(TodoItem task)
        {
            Action<TodoItem> toggled = SubtasksToggled;
            if (toggled != null)
            {
                toggled(task);
            }
        }

        private void ActivateSelectedRow()
        {
            EventHandler activated = TaskActivated;
            if (activated != null)
            {
                activated(this, EventArgs.Empty);
            }
        }

        private void ToggleRow(int index)
        {
            if (Items[index] is SearchResult)
            {
                return;
            }
            SubtaskRow child = Items[index] as SubtaskRow;
            if (child != null)
            {
                Action<SubtaskRow> subtaskRequested = SubtaskCompletionRequested;
                if (subtaskRequested != null)
                {
                    subtaskRequested(child);
                }
                return;
            }

            TodoItem task = TaskAt(index);
            if (task == null)
            {
                SelectOnly(index);
                EventHandler toggled = CompletedSectionToggled;
                if (toggled != null)
                {
                    toggled(this, EventArgs.Empty);
                }
                return;
            }

            Action<TodoItem> requested = CompletionRequested;
            if (requested != null)
            {
                requested(task);
            }
        }
    }
}
