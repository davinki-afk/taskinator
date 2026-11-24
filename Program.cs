using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Taskinator
{
    internal static class Program
    {
        private const string InstanceMutexName = @"Local\Taskinator.{065353B4-79A4-45A4-B6B9-2D11F59B2503}";
        internal static readonly int ShowWindowMessage = RegisterWindowMessage("Taskinator.ShowWindow.{065353B4-79A4-45A4-B6B9-2D11F59B2503}");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [STAThread]
        private static void Main(string[] args)
        {
            bool automaticStartup = args.Length == 1 &&
                String.Equals(args[0], WindowsStartup.LaunchArgument, StringComparison.OrdinalIgnoreCase);
            bool created;
            using (Mutex instance = new Mutex(true, InstanceMutexName, out created))
            {
                if (!created)
                {
                    if (!automaticStartup && ShowWindowMessage != 0)
                    {
                        PostMessage(new IntPtr(0xffff), ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
                    }
                    return;
                }

                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (MainForm mainForm = new MainForm())
                    {
                        mainForm.Shown += MainForm_Shown;
                        mainForm.Run(automaticStartup);
                    }
                }
                finally
                {
                    Win32xxTheme.Shutdown();
                    instance.ReleaseMutex();
                }
            }
        }

        private static void MainForm_Shown(object sender, EventArgs e)
        {
            FormPreloader.Preload();
        }
    }
}
