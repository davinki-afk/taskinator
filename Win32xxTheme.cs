using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Taskinator
{
    internal static class Win32xxTheme
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DrawGradient(IntPtr dc, int left, int top, int right, int bottom, int start, int end);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EndDrawing();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern bool FreeLibrary(IntPtr module);

        private static IntPtr module;
        private static DrawGradient draw;
        private static EndDrawing endDrawing;
        private static bool loadAttempted;
        private static readonly Version MinimumNativeVersion = new Version(6, 1);

        internal static bool TryPaint(Graphics graphics, Rectangle bounds, Color start, Color end)
        {
            using (Matrix transform = graphics.Transform)
            {
                // Transparent WinForms children paint their parent's background with a GDI+ translation.
                if (!transform.IsIdentity)
                {
                    return false;
                }
            }

            if (!loadAttempted)
            {
                LoadPainter();
            }
            if (draw == null)
            {
                return false;
            }

            IntPtr dc = graphics.GetHdc();
            try
            {
                return draw(dc, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom,
                    ColorTranslator.ToWin32(start), ColorTranslator.ToWin32(end)) != 0;
            }
            finally
            {
                graphics.ReleaseHdc(dc);
            }
        }

        private static void LoadPainter()
        {
            loadAttempted = true;
            // The optional native build can import APIs that are absent on XP and Vista.
            if (Environment.OSVersion.Version < MinimumNativeVersion)
            {
                return;
            }

            string fileName = IntPtr.Size == 8 ? "Win32xxTheme.x64.dll" : "Win32xxTheme.x86.dll";
            module = LoadLibraryW(Path.Combine(Application.StartupPath, fileName));
            if (module == IntPtr.Zero)
            {
                return;
            }

            IntPtr drawAddress = GetProcAddress(module, "DrawThemeGradient");
            IntPtr endAddress = GetProcAddress(module, "EndThemeDrawing");
            if (drawAddress == IntPtr.Zero || endAddress == IntPtr.Zero)
            {
                FreeLibrary(module);
                module = IntPtr.Zero;
                return;
            }

            draw = (DrawGradient)Marshal.GetDelegateForFunctionPointer(drawAddress, typeof(DrawGradient));
            endDrawing = (EndDrawing)Marshal.GetDelegateForFunctionPointer(endAddress, typeof(EndDrawing));
        }

        internal static void Shutdown()
        {
            if (module != IntPtr.Zero)
            {
                endDrawing();
                draw = null;
                endDrawing = null;
                FreeLibrary(module);
                module = IntPtr.Zero;
            }
        }
    }
}
