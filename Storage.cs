using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Taskinator
{
    public class Storage
    {
        private readonly string fileName;
        private bool preserveUnreadableFile;

        public Storage()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Taskinator");
            fileName = Path.Combine(folder, "lists.xml");
        }

        public TodoStore Load(out string error)
        {
            error = null;
            preserveUnreadableFile = false;
            try
            {
                using (XmlReader reader = OpenXml(fileName))
                {
                    return ReadStore(reader);
                }
            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is DirectoryNotFoundException)
                {
                    return new TodoStore { Language = L10n.DetectLanguage() };
                }

                preserveUnreadableFile = true;
                error = exception.Message;
                return new TodoStore();
            }
        }

        internal static TodoStore ImportXml(string path)
        {
            XmlDocument document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using (XmlReader reader = OpenXml(path))
            {
                document.Load(reader);
            }

            // The constructors generate IDs for new entries. Require IDs in the file
            // so importing the same records again cannot create copies.
            foreach (XmlNode record in document.SelectNodes("/TodoStore/Lists/TodoList | " +
                "/TodoStore/Lists/TodoList/Items/TodoItem | /TodoStore/Tags/TodoTag"))
            {
                XmlNode id = record.SelectSingleNode("Id");
                if (id == null || String.IsNullOrWhiteSpace(id.InnerText))
                {
                    throw InvalidXmlRecords();
                }
            }

            using (XmlNodeReader reader = new XmlNodeReader(document))
            {
                TodoStore store = ReadStore(reader);
                TodoItem[] tasks = store.Lists.SelectMany(list => list.Items).ToArray();
                if (!UniqueIds(store.Lists.Select(list => list.Id)) ||
                    !UniqueIds(store.Tags.Select(tag => tag.Id)) ||
                    !UniqueIds(tasks.Select(task => task.Id)) ||
                    store.Lists.Any(list => !ListIconCatalog.IsValidKey(list.IconKey)) ||
                    tasks.Any(task => task.ReminderAdvanceMinutes < 0))
                {
                    throw InvalidXmlRecords();
                }
                return store;
            }
        }

        private static bool UniqueIds(IEnumerable<string> ids)
        {
            HashSet<string> seen = new HashSet<string>();
            return ids.All(id => !String.IsNullOrWhiteSpace(id) && seen.Add(id));
        }

        private static InvalidDataException InvalidXmlRecords()
        {
            return new InvalidDataException(L10n.Get(TextId.InvalidXmlRecords));
        }

        private static XmlReader OpenXml(string path)
        {
            return XmlReader.Create(File.OpenRead(path), new XmlReaderSettings
            {
                CloseInput = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
        }

        private static TodoStore ReadStore(XmlReader reader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TodoStore));
            TodoStore store = (TodoStore)serializer.Deserialize(reader);
            if (store == null)
            {
                throw new InvalidDataException();
            }
            if (store.Lists == null)
            {
                store.Lists = new List<TodoList>();
            }
            if (store.Tags == null)
            {
                store.Tags = new List<TodoTag>();
            }
            foreach (TodoList list in store.Lists)
            {
                foreach (TodoItem task in list.Items)
                {
                    if (task.Subtasks.Exists(subtask => String.IsNullOrWhiteSpace(subtask.Title)))
                    {
                        throw new InvalidDataException(L10n.Get(TextId.NameRequired));
                    }
                    if (task.TagIds == null)
                    {
                        task.TagIds = new List<string>();
                    }
                    if (!String.IsNullOrEmpty(task.TagId) && !task.TagIds.Contains(task.TagId))
                    {
                        task.TagIds.Add(task.TagId);
                    }
                    task.TagId = null;
                }
            }
            return store;
        }

        public void Save(TodoStore store)
        {
            string folder = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string backup = preserveUnreadableFile ? fileName + "." + Guid.NewGuid().ToString("N") + ".bak" : null;
            WriteXml(fileName, store, backup);
            preserveUnreadableFile = false;
        }

        internal static void ExportXml(string path, TodoStore store)
        {
            WriteXml(path, store, null);
        }

        private static void WriteXml(string path, TodoStore store, string backup)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TodoStore));
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, NewLineHandling = NewLineHandling.Entitize };
            WriteFile(path, stream =>
            {
                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                {
                    serializer.Serialize(writer, store);
                }
            }, backup);
        }

        internal static void WriteFile(string path, Action<Stream> write, string backup = null)
        {
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                }
                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, backup);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
