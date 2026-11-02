using System;
using System.Windows.Forms;

namespace Taskinator
{
    internal static class FormPreloader
    {
        private static bool preloaded;

        public static void Preload()
        {
            if (preloaded)
            {
                return;
            }

            preloaded = true;

            // Load the complete icon catalog while the application is starting.
            ListIconCatalog.GetOptions();

            PreloadForm(new TextPromptForm(
                "Новый список", "Название списка:", String.Empty, ListIconCatalog.DefaultIconKey));
            PreloadForm(new IconPickerForm(ListIconCatalog.DefaultIconKey));
            PreloadForm(new TaskEditorForm(null));
            PreloadForm(new TaskEditorForm(new TodoItem()));
        }

        private static void PreloadForm(Form form)
        {
            using (form)
            {
                // Force WinForms to create the control tree and native handles now,
                // instead of doing that during the first real ShowDialog call.
                form.CreateControl();
                form.PerformLayout();
            }
        }
    }
}
