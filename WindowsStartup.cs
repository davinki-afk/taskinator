using System;
using System.IO;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Taskinator
{
    internal static class WindowsStartup
    {
        internal const string LaunchArgument = "--autostart";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Taskinator";

        internal static bool TrySetEnabled(bool enabled, out string error)
        {
            try
            {
                using (RegistryKey key = enabled
                    ? Registry.CurrentUser.CreateSubKey(RunKey)
                    : Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (enabled)
                    {
                        string command = "\"" + Application.ExecutablePath + "\" " + LaunchArgument;
                        if (!String.Equals(key.GetValue(ValueName) as string, command, StringComparison.Ordinal))
                        {
                            key.SetValue(ValueName, command, RegistryValueKind.String);
                        }
                    }
                    else if (key != null)
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                if (!(exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException))
                {
                    throw;
                }

                error = exception.Message;
                return false;
            }
        }
    }
}
