using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;

namespace Taskinator
{
    internal enum TextId
    {
        WindowTitle,
        Settings,
        Search,
        About,
        AboutTitle,
        AboutCredits,
        Help,
        HelpTopics,
        HelpFileMissing,
        SearchTasks,
        SearchQuery,
        HideCompletedTasks,
        SearchResults,
        DateFrom,
        DateTo,
        InvalidSearchDateRange,
        NoSearchResults,
        OpenTask,
        OpenSubtask,
        Close,
        SettingsSections,
        General,
        StartWithWindows,
        ShowWindowOnStartup,
        StartupChangeFailed,
        KeyboardShortcuts,
        ShortcutInstructions,
        ShortcutAlreadyAssigned,
        ResetShortcuts,
        Language,
        InterfaceLanguage,
        Appearance,
        InterfaceTheme,
        ThemeSystem,
        ThemeOfficeBlue,
        ThemeOfficeSilver,
        ThemeOfficeOlive,
        TaskCount,
        CountAllTasks,
        CountIncompleteTasks,
        Sounds,
        SoundFolder,
        RefreshSoundFolders,
        SoundFolderInstructions,
        ImportSoundFolder,
        ChooseSoundFolder,
        DeleteSoundFolder,
        ConfirmDeleteSoundFolder,
        MuteNonReminderSounds,
        InvalidSoundFolder,
        SoundFilesMissing,
        SoundFolderExists,
        SoundFolderContainsLinks,
        SoundImportFailed,
        SoundDeleteFailed,
        OK,
        Cancel,
        NewList,
        NewTask,
        MyLists,
        MetaAllTasks,
        MetaCompletedTasks,
        MetaToday,
        MetaThisWeek,
        ChooseTaskList,
        CreateList,
        RenameList,
        DeleteList,
        SelectOrCreateList,
        CreateTask,
        EditTask,
        DeleteTask,
        DeleteSelectedTasks,
        DeleteCompletedTasks,
        MarkTasksComplete,
        MarkTasksIncomplete,
        SelectAllTasks,
        MoveUp,
        MoveDown,
        MoveToList,
        Tasks,
        OpenTaskinator,
        Exit,
        ListName,
        ConfirmDeleteList,
        ConfirmDeleteTask,
        ConfirmDeleteSelectedTasks,
        ConfirmDeleteCompletedTasks,
        ListRequired,
        LoadFailed,
        SaveFailed,
        Icon,
        ChooseIcon,
        IconPicker,
        ChooseListIcon,
        IconRequired,
        NameRequired,
        Task,
        TitleColor,
        Default,
        Description,
        Subtasks,
        SubtaskTitle,
        EditSubtask,
        DeleteSubtask,
        ConfirmDeleteSubtask,
        SubtaskProgress,
        Tags,
        TaskTags,
        NoTags,
        Completed,
        CompletedTasks,
        TaskTitleRequired,
        NewTag,
        EditTag,
        Name,
        Color,
        TagNameRequired,
        Add,
        Edit,
        Delete,
        ConfirmDeleteTag,
        Reminder,
        AdvanceReminder,
        ReminderAt,
        ReminderAtWithAdvance,
        DateAndTime,
        ReminderDate,
        ReminderTime,
        RemindEarly,
        ReminderInterval,
        ReminderUnit,
        InvalidReminderTime,
        FutureReminderTime,
        Minutes,
        Hours,
        Days,
        ImportExport,
        CsvFiles,
        CsvInstructions,
        ImportCsv,
        ExportCsv,
        CsvImportFailed,
        CsvExportFailed,
        ImportCounts,
        CsvSaved,
        XmlFiles,
        ImportXml,
        ExportXml,
        XmlImportFailed,
        XmlExportFailed,
        XmlSaved,
        InvalidXmlRecords,
        InvalidCsvFormat,
        InvalidCsvColumnCount,
        CsvListMissing,
        CsvTagLinkMissing,
        CsvSubtaskParentMissing,
        DuplicateCsvId,
        InvalidCsvField,
        InvalidCsvQuotes,
        UnclosedCsvQuote,
        Black,
        Maroon,
        Green,
        Olive,
        Navy,
        Purple,
        Teal,
        Silver,
        Gray,
        Red,
        Lime,
        Yellow,
        Blue,
        Magenta,
        Aqua,
        White
    }

    internal static class L10n
    {
        private static readonly string[] LanguageCodes = { "ru", "en", "uk", "be", "kk" };
        private static readonly Catalog[] Catalogs = Array.ConvertAll(LanguageCodes, LoadCatalog);
        private static readonly Catalog Russian = Catalogs[0];
        private static readonly Catalog English = Catalogs[1];
        internal static readonly string[] LanguageNames = Array.ConvertAll(Catalogs, catalog => catalog.Name);
        private static int languageIndex;

        internal static string LanguageCode
        {
            get { return LanguageCodes[languageIndex]; }
        }

        internal static CultureInfo Culture
        {
            get { return Catalogs[languageIndex].Culture ?? Russian.Culture ?? English.Culture ?? CultureInfo.InvariantCulture; }
        }

        internal static int GetLanguageIndex(string code)
        {
            return Math.Max(0, Array.IndexOf(LanguageCodes, code));
        }

        internal static string DetectLanguage()
        {
            string code = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            return Array.IndexOf(LanguageCodes, code) >= 0 ? code : "en";
        }

        internal static string GetLanguageCode(int index)
        {
            return LanguageCodes[index];
        }

        internal static void SetLanguage(string code)
        {
            languageIndex = GetLanguageIndex(code);
        }

        internal static string Get(TextId key)
        {
            string text;
            if (Catalogs[languageIndex].Texts.TryGetValue(key, out text) ||
                Russian.Texts.TryGetValue(key, out text) ||
                English.Texts.TryGetValue(key, out text))
            {
                return text;
            }

            return key.ToString();
        }

        internal static string Format(TextId key, params object[] values)
        {
            string text = Get(key);
            try
            {
                return String.Format(Culture, text, values);
            }
            catch (FormatException error)
            {
                Trace.TraceWarning("Invalid translation format for {0}: {1}", key, error.Message);
                return text;
            }
        }

        private static Catalog LoadCatalog(string code)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang", code + ".xml");
            try
            {
                XmlDocument document = new XmlDocument { XmlResolver = null };
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(path, settings))
                {
                    document.Load(reader);
                }

                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "language")
                {
                    throw new XmlException("Expected a language element.");
                }

                string name = root.GetAttribute("name");
                string culture = root.GetAttribute("culture");
                if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(culture))
                {
                    throw new XmlException("Expected a language name and culture.");
                }

                CultureInfo catalogCulture = null;
                try
                {
                    catalogCulture = CultureInfo.GetCultureInfo(culture);
                }
                catch (ArgumentException error)
                {
                    Trace.TraceWarning("Could not use culture {0}: {1}", culture, error.Message);
                }

                Catalog catalog = new Catalog(name, catalogCulture);
                foreach (XmlElement entry in root.SelectNodes("text"))
                {
                    TextId key;
                    if (Enum.TryParse(entry.GetAttribute("key"), out key) && !String.IsNullOrWhiteSpace(entry.InnerText))
                    {
                        catalog.Texts[key] = entry.InnerText;
                    }
                }
                return catalog;
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is UnauthorizedAccessException ||
                    error is XmlException))
                {
                    throw;
                }

                Trace.TraceWarning("Could not load language file {0}: {1}", path, error.Message);
                return new Catalog(code, null);
            }
        }

        private sealed class Catalog
        {
            internal readonly string Name;
            internal readonly CultureInfo Culture;
            internal readonly Dictionary<TextId, string> Texts = new Dictionary<TextId, string>();

            internal Catalog(string name, CultureInfo culture)
            {
                Name = name;
                Culture = culture;
            }
        }
    }
}
