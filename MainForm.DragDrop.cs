using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Taskinator
{
    public partial class MainForm
    {
        private const string RowDragFormat = "Taskinator.Row";
        private const int DragScrollMargin = 12;

        private enum DropPlacement { Before, After, Into }

        private Rectangle dragThreshold;
        private object pressedRow;
        private object draggedRow;
        private TodoList draggedTaskList;
        private TodoItem[] draggedTasks;
        private ListBox hoveredListBox;
        private int markerRow = ListBox.NoMatches;
        private DropPlacement markerPlacement;

        private void EnableRowDragging(ListBox listBox)
        {
            listBox.AllowDrop = true;
            listBox.MouseDown += RowDrag_MouseDown;
            listBox.MouseMove += RowDrag_MouseMove;
            listBox.MouseUp += delegate { pressedRow = null; };
            listBox.DragEnter += RowDrag_Over;
            listBox.DragOver += RowDrag_Over;
            listBox.DragLeave += delegate { SetDropMarker(null, ListBox.NoMatches, DropPlacement.Before); };
            listBox.DragDrop += RowDrag_Drop;
        }

        private void RowDrag_MouseDown(object sender, MouseEventArgs e)
        {
            pressedRow = null;
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ListBox listBox = (ListBox)sender;
            int row = RowAtPoint(listBox, e.Location);
            if (row < 0 ||
                (listBox == listsListBox && !(listBox.Items[row] is TodoList)) ||
                (listBox == tasksListBox && (ModifierKeys != Keys.None || tasksListBox.TaskAt(row) == null ||
                    tasksListBox.IsCheckBoxHit(listBox.GetItemRectangle(row), e.Location) ||
                    tasksListBox.IsSubtaskToggleHit(row, listBox.GetItemRectangle(row), e.Location))))
            {
                return;
            }

            pressedRow = listBox.Items[row];
            Size size = SystemInformation.DragSize;
            dragThreshold = new Rectangle(e.X - size.Width / 2, e.Y - size.Height / 2, size.Width, size.Height);
        }

        private void RowDrag_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                pressedRow = null;
            }
            if (pressedRow == null || dragThreshold.Contains(e.Location) || draggedRow != null)
            {
                return;
            }

            ListBox listBox = (ListBox)sender;
            if (!listBox.Items.Contains(pressedRow))
            {
                return;
            }

            draggedRow = pressedRow;
            draggedTaskList = listBox == tasksListBox ? CurrentList : null;
            pressedRow = null;
            if (listBox == tasksListBox)
            {
                draggedTasks = tasksListBox.SelectedTasks();
                if (!draggedTasks.Contains((TodoItem)draggedRow))
                {
                    draggedTasks = new[] { (TodoItem)draggedRow };
                }
                listBox.Capture = false;
            }
            try
            {
                using (Timer scrollTimer = new Timer { Interval = 120 })
                {
                    scrollTimer.Tick += RowDrag_Scroll;
                    scrollTimer.Start();
                    listBox.DoDragDrop(new DataObject(RowDragFormat, String.Empty), DragDropEffects.Move);
                }
            }
            finally
            {
                SetDropMarker(null, ListBox.NoMatches, DropPlacement.Before);
                draggedRow = null;
                draggedTaskList = null;
                draggedTasks = null;
            }
        }

        private static int RowAtPoint(ListBox listBox, Point point)
        {
            int row = listBox.IndexFromPoint(point);
            return row >= 0 && row < listBox.Items.Count && listBox.GetItemRectangle(row).Contains(point)
                ? row
                : ListBox.NoMatches;
        }

        private int GetDropRow(ListBox listBox, Point point, out DropPlacement placement)
        {
            placement = DropPlacement.Before;
            int row = RowAtPoint(listBox, point);
            if (draggedTasks != null && listBox == listsListBox)
            {
                placement = DropPlacement.Into;
                TodoList destination = row >= 0 ? listBox.Items[row] as TodoList : null;
                return destination != null && draggedTasks.Any(task => !destination.Items.Contains(task))
                    ? row : ListBox.NoMatches;
            }

            int oldRow = listBox.Items.IndexOf(draggedRow);
            if (oldRow < 0 || (listBox == tasksListBox && (draggedTaskList == null || draggedTaskList != CurrentList)))
            {
                return ListBox.NoMatches;
            }

            if (row < 0)
            {
                row = listBox.Items.Count - 1;
                if (point.Y < listBox.GetItemRectangle(row).Bottom)
                {
                    return ListBox.NoMatches;
                }
                placement = DropPlacement.After;
            }
            else
            {
                Rectangle bounds = listBox.GetItemRectangle(row);
                if (point.Y >= bounds.Top + bounds.Height / 2)
                {
                    placement = DropPlacement.After;
                }
            }

            if (listBox == tasksListBox)
            {
                TodoItem task = (TodoItem)draggedRow;
                if (!task.IsComplete && listBox.Items[row] is TaskListBox.CompletedTasksHeader)
                {
                    row--;
                    placement = DropPlacement.After;
                }
                TaskListBox.SubtaskRow child = listBox.Items[row] as TaskListBox.SubtaskRow;
                if (child != null)
                {
                    row = listBox.Items.IndexOf(child.Parent);
                    placement = DropPlacement.After;
                }
                TodoItem target = tasksListBox.TaskAt(row);
                if (target == null || target.IsComplete != task.IsComplete)
                {
                    return ListBox.NoMatches;
                }
                if (draggedTasks.Length > 1)
                {
                    return row;
                }
                if (row == oldRow || (placement == DropPlacement.Before
                    ? tasksListBox.LastTaskRow(oldRow) + 1 == row
                    : tasksListBox.LastTaskRow(row) + 1 == oldRow))
                {
                    return ListBox.NoMatches;
                }
                return row;
            }
            return listBox.Items[row] is TodoList && ReorderOffset(oldRow, row, placement) != 0
                ? row : ListBox.NoMatches;
        }

        private static int ReorderOffset(int oldRow, int targetRow, DropPlacement placement)
        {
            int insertion = targetRow + (placement == DropPlacement.After ? 1 : 0);
            int newRow = insertion > oldRow ? insertion - 1 : insertion;
            return newRow - oldRow;
        }

        private void RowDrag_Over(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (draggedRow == null || !e.Data.GetDataPresent(RowDragFormat))
            {
                return;
            }

            ListBox listBox = (ListBox)sender;
            DropPlacement placement;
            int row = GetDropRow(listBox, listBox.PointToClient(new Point(e.X, e.Y)), out placement);
            SetDropMarker(listBox, row, placement);
            if (row >= 0)
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void RowDrag_Drop(object sender, DragEventArgs e)
        {
            RowDrag_Over(sender, e);
            SetDropMarker(null, ListBox.NoMatches, DropPlacement.Before);
            if (e.Effect != DragDropEffects.Move)
            {
                return;
            }

            ListBox listBox = (ListBox)sender;
            DropPlacement placement;
            int row = GetDropRow(listBox, listBox.PointToClient(new Point(e.X, e.Y)), out placement);
            if (row < 0)
            {
                return;
            }
            if (placement == DropPlacement.Into)
            {
                MoveTasksToList(draggedTasks, (TodoList)listBox.Items[row]);
                return;
            }

            if (listBox == listsListBox)
            {
                int offset = ReorderOffset(listBox.Items.IndexOf(draggedRow), row, placement);
                MoveList((TodoList)draggedRow, offset);
            }
            else
            {
                DropTasks(tasksListBox.TaskAt(row), placement);
            }
        }

        private void DropTasks(TodoItem target, DropPlacement placement)
        {
            HashSet<TodoItem> selected = new HashSet<TodoItem>(draggedTasks);
            List<TodoItem> original = draggedTaskList.Items;
            int boundary = original.IndexOf(target) + (placement == DropPlacement.After ? 1 : 0);
            List<TodoItem> order = original.FindAll(item => !selected.Contains(item));
            int insertion = original.Take(boundary).Count(item => !selected.Contains(item));
            order.InsertRange(insertion, original.Where(selected.Contains));
            if (!order.SequenceEqual(original))
            {
                SaveTaskOrder(order, draggedTasks);
            }
        }

        private void RowDrag_Scroll(object sender, EventArgs e)
        {
            ListBox listBox = hoveredListBox;
            if (listBox == null || listBox.Items.Count == 0)
            {
                return;
            }

            Point point = listBox.PointToClient(Cursor.Position);
            if (!listBox.ClientRectangle.Contains(point))
            {
                return;
            }

            int top = listBox.TopIndex;
            if (point.Y < DragScrollMargin && top > 0)
            {
                listBox.TopIndex = top - 1;
            }
            else if (point.Y >= listBox.ClientSize.Height - DragScrollMargin && top + 1 < listBox.Items.Count &&
                listBox.GetItemRectangle(listBox.Items.Count - 1).Bottom > listBox.ClientSize.Height)
            {
                listBox.TopIndex = top + 1;
            }
            else
            {
                return;
            }

            DropPlacement placement;
            int row = GetDropRow(listBox, point, out placement);
            SetDropMarker(listBox, row, placement);
        }

        private void SetDropMarker(ListBox listBox, int row, DropPlacement placement)
        {
            if (row >= 0 && listBox == tasksListBox && placement == DropPlacement.After)
            {
                row = tasksListBox.LastTaskRow(row);
            }
            if (hoveredListBox != listBox || markerRow != row || markerPlacement != placement)
            {
                if (markerRow >= 0)
                {
                    hoveredListBox.DrawItem -= RowDrag_DrawMarker;
                    if (markerRow < hoveredListBox.Items.Count)
                    {
                        hoveredListBox.Invalidate(hoveredListBox.GetItemRectangle(markerRow));
                    }
                }
                hoveredListBox = listBox;
                markerRow = row;
                markerPlacement = placement;
                if (row >= 0)
                {
                    listBox.DrawItem += RowDrag_DrawMarker;
                    listBox.Invalidate(listBox.GetItemRectangle(row));
                }
            }
        }

        private void RowDrag_DrawMarker(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || sender != hoveredListBox || e.Index != markerRow)
            {
                return;
            }

            Rectangle bounds = e.Bounds;
            if (markerPlacement == DropPlacement.Into)
            {
                ControlPaint.DrawBorder(e.Graphics, bounds, SystemColors.Highlight, ButtonBorderStyle.Solid);
            }
            else
            {
                int y = markerPlacement == DropPlacement.Before ? bounds.Top : bounds.Bottom - 2;
                e.Graphics.FillRectangle(SystemBrushes.Highlight, bounds.Left, y, bounds.Width, 2);
            }
        }
    }
}
