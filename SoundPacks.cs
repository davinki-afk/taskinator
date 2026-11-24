using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Taskinator
{
    internal static class SoundPacks
    {
        internal const string DefaultFolder = "default";
        internal const string TaskCompleteFile = "taskcomplete.wav";
        internal const string ReminderFile = "reminder.wav";
        internal const string AlarmFile = "alarm.wav";
        private const string ImportedFolder = ".imported";
        private const string TemporaryPrefix = ".taskinator-";
        private const uint SoundAsync = 0x0001;
        private const uint SoundNoDefault = 0x0002;
        private const uint SoundFileName = 0x00020000;
        private static readonly string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds");
        private static readonly string ImportedRoot = Path.Combine(Root, ImportedFolder);

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern bool PlaySound(string sound, IntPtr module, uint flags);

        internal static List<string> GetFolders()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddFolders(ImportedRoot, names);
            AddFolders(Root, names);
            List<string> folders = new List<string>(names);
            folders.Sort(StringComparer.CurrentCultureIgnoreCase);
            folders.Insert(0, DefaultFolder);
            return folders;
        }

        private static void AddFolders(string root, HashSet<string> folders)
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    return;
                }

                foreach (string directory in Directory.GetDirectories(root))
                {
                    string folder = Path.GetFileName(directory);
                    if (IsCustomFolder(folder))
                    {
                        folders.Add(folder);
                    }
                }
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is UnauthorizedAccessException))
                {
                    throw;
                }
                Trace.TraceWarning("Could not list sound folders: {0}", error.Message);
            }
        }

        internal static string ImportFolder(string source)
        {
            source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string folder = Path.GetFileName(source);
            if (!IsCustomFolder(folder) ||
                ImportedRoot.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(L10n.Get(TextId.InvalidSoundFolder));
            }

            if (GetFolders().Exists(name => String.Equals(name, folder, StringComparison.OrdinalIgnoreCase)))
            {
                throw new IOException(L10n.Format(TextId.SoundFolderExists, folder));
            }
            if (!File.Exists(Path.Combine(source, TaskCompleteFile)) &&
                !File.Exists(Path.Combine(source, ReminderFile)) &&
                !File.Exists(Path.Combine(source, AlarmFile)))
            {
                throw new IOException(L10n.Get(TextId.SoundFilesMissing));
            }

            Directory.CreateDirectory(ImportedRoot);
            string temporary = TemporaryDirectory(ImportedRoot);
            try
            {
                // Publish the pack only after the entire folder has been copied.
                CopyFolder(source, temporary);
                Directory.Move(temporary, Path.Combine(ImportedRoot, folder));
                return folder;
            }
            catch
            {
                try
                {
                    if (Directory.Exists(temporary))
                    {
                        DeleteTree(temporary);
                    }
                }
                catch (Exception error)
                {
                    if (!(error is IOException || error is UnauthorizedAccessException))
                    {
                        throw;
                    }
                    Trace.TraceWarning("Could not clean up sound import: {0}", error.Message);
                }
                throw;
            }
        }

        internal static void DeleteFolder(string folder)
        {
            if (!IsCustomFolder(folder))
            {
                throw new IOException(L10n.Get(TextId.InvalidSoundFolder));
            }

            string directory = GetCustomPath(folder);
            string temporary = TemporaryDirectory(Path.GetDirectoryName(directory));
            // Stop offering the pack before deleting its individual files.
            Directory.Move(directory, temporary);
            DeleteTree(temporary);
        }

        private static bool IsCustomFolder(string folder)
        {
            return !String.IsNullOrEmpty(folder) &&
                folder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                folder.TrimEnd(' ', '.').Length == folder.Length &&
                !String.Equals(folder, DefaultFolder, StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(folder, ImportedFolder, StringComparison.OrdinalIgnoreCase) &&
                !folder.StartsWith(TemporaryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCustomPath(string folder)
        {
            string imported = Path.Combine(ImportedRoot, folder);
            return Directory.Exists(imported) ? imported : Path.Combine(Root, folder);
        }

        private static string TemporaryDirectory(string parent)
        {
            return Path.Combine(parent, TemporaryPrefix + Guid.NewGuid().ToString("N"));
        }

        private static void CopyFolder(string source, string destination)
        {
            CheckCopyEntry(source);
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                CheckCopyEntry(file);
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyFolder(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private static void CheckCopyEntry(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(L10n.Get(TextId.SoundFolderContainsLinks));
            }
        }

        private static void DeleteTree(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    FileAttributes attributes = File.GetAttributes(file);
                    if ((attributes & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(file);
                }
                foreach (string child in Directory.GetDirectories(directory))
                {
                    DeleteTree(child);
                }
            }
            Directory.Delete(directory);
        }

        internal static void Play(string folder, string fileName)
        {
            if (IsCustomFolder(folder) && TryPlay(GetCustomPath(folder), fileName))
            {
                return;
            }

            TryPlay(Path.Combine(Root, DefaultFolder), fileName);
        }

        private static bool TryPlay(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            return File.Exists(path) && PlaySound(path, IntPtr.Zero, SoundFileName | SoundAsync | SoundNoDefault);
        }
    }
}
