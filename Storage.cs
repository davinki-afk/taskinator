using System;
using System.IO;
using System.Xml.Serialization;

namespace Taskinator
{
    public class Storage
    {
        private readonly string fileName;

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
            if (!File.Exists(fileName))
            {
                return new TodoStore();
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(TodoStore));
                using (FileStream stream = File.OpenRead(fileName))
                {
                    TodoStore store = (TodoStore)serializer.Deserialize(stream);
                    if (store.Lists == null)
                    {
                        store.Lists = new System.Collections.Generic.List<TodoList>();
                    }
                    return store;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return new TodoStore();
            }
        }

        public void Save(TodoStore store)
        {
            string folder = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string temporaryFile = fileName + ".tmp";
            XmlSerializer serializer = new XmlSerializer(typeof(TodoStore));
            using (FileStream stream = File.Create(temporaryFile))
            {
                serializer.Serialize(stream, store);
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            File.Move(temporaryFile, fileName);
        }
    }
}
