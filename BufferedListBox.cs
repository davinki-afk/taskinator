using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Taskinator
{
    internal class BufferedListBox : ListBox
    {
        private const int PaintMessage = 0x000F;
        private const int EraseBackgroundMessage = 0x0014;
        private const int VerticalScrollMessage = 0x0115;
        private const int MouseWheelMessage = 0x020A;
        private const int PrintMessage = 0x0317;
        private const int PrintClientMessage = 0x0318;
        private const int PrintClient = 0x0004;
        private const int PrintCheckVisible = 0x0001;
        private const int GetCaretIndexMessage = 0x019F;
        private const int CopyPixels = 0x00CC0020; // SRCCOPY
        private const int BufferGrowth = 128;
        private readonly BufferedGraphicsContext paintBuffer = new BufferedGraphicsContext();

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            internal IntPtr Dc;
            internal int Erase;
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
            internal int Restore;
            internal int IncUpdate;
            private PaintReserved reserved;
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct PaintReserved { }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool EndPaint(IntPtr window, ref PaintStruct paint);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height,
            IntPtr source, int sourceX, int sourceY, int operation);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == EraseBackgroundMessage)
            {
                // The buffered frame includes the background. Erasing here would expose a blank frame.
                m.Result = new IntPtr(1);
                return;
            }

            if (m.Msg == VerticalScrollMessage || m.Msg == MouseWheelMessage)
            {
                base.WndProc(ref m);
                // Native scrolling can copy pixels into the area below the last task.
                Invalidate();
                return;
            }

            if (m.Msg == PrintMessage)
            {
                int flags = m.LParam.ToInt32();
                if ((flags & PrintCheckVisible) == 0 || Visible)
                {
                    m.LParam = new IntPtr(flags & ~PrintClient);
                    base.WndProc(ref m);
                    if ((flags & PrintClient) != 0 && m.WParam != IntPtr.Zero)
                    {
                        PaintClientToDc(m.WParam);
                    }
                }
                return;
            }

            if (m.Msg == PrintClientMessage || (m.Msg == PaintMessage && m.WParam != IntPtr.Zero))
            {
                if (m.WParam != IntPtr.Zero)
                {
                    PaintClientToDc(m.WParam);
                }
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg != PaintMessage || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                base.WndProc(ref m);
                return;
            }

            PaintStruct paint;
            IntPtr dc = BeginPaint(m.HWnd, out paint);
            if (dc == IntPtr.Zero)
            {
                base.WndProc(ref m);
                return;
            }

            try
            {
                Rectangle bounds = ClientRectangle;
                Rectangle dirty = Rectangle.Intersect(bounds, Rectangle.FromLTRB(paint.Left, paint.Top, paint.Right, paint.Bottom));
                if (dirty.Width > 0 && dirty.Height > 0)
                {
                    PaintBuffered(dc, bounds, dirty);
                }
            }
            finally
            {
                EndPaint(m.HWnd, ref paint);
            }
            m.Result = IntPtr.Zero;
        }

        protected sealed override void OnDrawItem(DrawItemEventArgs e)
        {
            // Queue native focus, selection and scroll drawing in the next buffered frame.
            Rectangle row = new Rectangle(0, e.Bounds.Top, ClientSize.Width, e.Bounds.Height);
            Rectangle dirty = Rectangle.Intersect(ClientRectangle, row);
            if (dirty.Width > 0 && dirty.Height > 0)
            {
                Invalidate(dirty);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate();
        }

        protected virtual void PaintRow(DrawItemEventArgs e)
        {
            base.OnDrawItem(e);
        }

        private void PaintClientToDc(IntPtr dc)
        {
            using (Graphics graphics = Graphics.FromHdc(dc))
            {
                PaintClient(graphics, ClientRectangle);
            }
        }

        private void PaintClient(Graphics graphics, Rectangle dirty)
        {
            using (SolidBrush background = new SolidBrush(BackColor))
            {
                graphics.FillRectangle(background, dirty);
            }

            int width = ClientSize.Width;
            int focusedIndex = Focused && ShowFocusCues
                ? SendMessage(Handle, GetCaretIndexMessage, IntPtr.Zero, IntPtr.Zero).ToInt32() : NoMatches;
            for (int i = TopIndex; i >= 0 && i < Items.Count; i++)
            {
                // Use the native positions and heights, and paint intersecting rows across the client width.
                Rectangle row = GetItemRectangle(i);
                if (row.Top >= dirty.Bottom)
                {
                    break;
                }
                if (row.Bottom <= dirty.Top || row.Height <= 0)
                {
                    continue;
                }
                row.X = 0;
                row.Width = width;
                DrawItemState state = GetSelected(i) ? DrawItemState.Selected : DrawItemState.None;
                if (i == focusedIndex)
                {
                    state |= DrawItemState.Focus;
                }
                PaintRow(new DrawItemEventArgs(graphics, Font, row, i, state, ForeColor, BackColor));
            }
        }

        private void PaintBuffered(IntPtr dc, Rectangle bounds, Rectangle dirty)
        {
            Size maximum = paintBuffer.MaximumBuffer;
            if (bounds.Width > maximum.Width || bounds.Height > maximum.Height)
            {
                maximum = new Size(
                    bounds.Width > maximum.Width ? bounds.Width + BufferGrowth : maximum.Width,
                    bounds.Height > maximum.Height ? bounds.Height + BufferGrowth : maximum.Height);
                paintBuffer.MaximumBuffer = maximum;
            }

            // Reuse a slightly larger bitmap across successive window sizes.
            using (BufferedGraphics buffer = paintBuffer.Allocate(dc, new Rectangle(Point.Empty, maximum)))
            {
                PaintClient(buffer.Graphics, dirty);
                IntPtr bufferDc = buffer.Graphics.GetHdc();
                try
                {
                    BitBlt(dc, dirty.X, dirty.Y, dirty.Width, dirty.Height, bufferDc, dirty.X, dirty.Y, CopyPixels);
                }
                finally
                {
                    buffer.Graphics.ReleaseHdc(bufferDc);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                paintBuffer.Dispose();
            }
        }
    }
}
