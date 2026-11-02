using System;
using System.Windows.Forms;

namespace Taskinator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm mainForm = new MainForm();
            mainForm.Shown += MainForm_Shown;
            Application.Run(mainForm);
        }

        private static void MainForm_Shown(object sender, EventArgs e)
        {
            FormPreloader.Preload();
        }
    }
}
