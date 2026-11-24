using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace Taskinator
{
    [Serializable]
    public class TodoStore
    {
        public TodoStore()
        {
            Lists = new List<TodoList>();
            Tags = new List<TodoTag>();
            ShortcutOverrides = new List<ShortcutBinding>();
            Language = "ru";
            Theme = ThemeStyle.DefaultKey;
            CountOnlyIncompleteTasks = true;
            StartWithWindows = true;
            ShowWindowOnStartup = true;
            SoundFolder = SoundPacks.DefaultFolder;
        }

        public List<TodoList> Lists { get; set; }
        public List<TodoTag> Tags { get; set; }
        [DefaultValue("ru")]
        public string Language { get; set; }
        [DefaultValue(ThemeStyle.DefaultKey)]
        public string Theme { get; set; }
        [DefaultValue(true)]
        public bool CountOnlyIncompleteTasks { get; set; }
        [DefaultValue(true)]
        public bool StartWithWindows { get; set; }
        [DefaultValue(true)]
        public bool ShowWindowOnStartup { get; set; }
        [DefaultValue(SoundPacks.DefaultFolder)]
        public string SoundFolder { get; set; }
        [DefaultValue(false)]
        public bool MuteNonReminderSounds { get; set; }
        public List<ShortcutBinding> ShortcutOverrides { get; set; }

        public bool ShouldSerializeShortcutOverrides()
        {
            return ShortcutOverrides.Count != 0;
        }

        internal void AddMissing(TodoStore source)
        {
            HashSet<string> tagIds = new HashSet<string>(Tags.Select(tag => tag.Id));
            foreach (TodoTag tag in source.Tags)
            {
                if (tagIds.Add(tag.Id))
                {
                    Tags.Add(tag);
                }
            }

            Dictionary<string, TodoList> lists = Lists.ToDictionary(list => list.Id);
            HashSet<string> taskIds = new HashSet<string>(Lists.SelectMany(list => list.Items).Select(task => task.Id));
            foreach (TodoList incoming in source.Lists)
            {
                TodoList list;
                if (!lists.TryGetValue(incoming.Id, out list))
                {
                    list = new TodoList { Id = incoming.Id, Name = incoming.Name, IconKey = incoming.IconKey };
                    lists.Add(list.Id, list);
                    Lists.Add(list);
                }
                foreach (TodoItem task in incoming.Items)
                {
                    if (taskIds.Add(task.Id))
                    {
                        list.Items.Add(task);
                    }
                }
            }
        }

        internal void RemoveMissingTags()
        {
            HashSet<string> activeTags = new HashSet<string>();
            foreach (TodoTag tag in Tags)
            {
                activeTags.Add(tag.Id);
            }
            foreach (TodoList list in Lists)
            {
                for (int i = 0; i < list.Items.Count; i++)
                {
                    TodoItem task = list.Items[i];
                    List<string> retained = task.TagIds.FindAll(activeTags.Contains);
                    if (retained.Count != task.TagIds.Count)
                    {
                        list.Items[i] = task.WithTags(retained);
                    }
                }
            }
        }
    }

    [Serializable]
    public class ShortcutBinding
    {
        [XmlAttribute]
        public string Action { get; set; }
        [XmlAttribute]
        public int KeyData { get; set; }
    }

    [Serializable]
    public class TodoTag
    {
        public TodoTag()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public int ColorIndex { get; set; }

        public override string ToString()
        {
            return Name ?? String.Empty;
        }
    }

    [Serializable]
    public class TodoList
    {
        public TodoList()
        {
            Id = Guid.NewGuid().ToString("N");
            Items = new List<TodoItem>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string IconKey { get; set; }
        public List<TodoItem> Items { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    [Serializable]
    public class TodoItem
    {
        public TodoItem()
        {
            Id = Guid.NewGuid().ToString("N");
            CreatedAt = DateTime.Now;
            TagIds = new List<string>();
            Subtasks = new List<TodoSubtask>();
            TitleColorIndex = -1;
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<TodoSubtask> Subtasks { get; set; }
        // Read by Storage when opening files written by the single-tag version.
        public string TagId { get; set; }
        public List<string> TagIds { get; set; }
        [DefaultValue(-1)]
        public int TitleColorIndex { get; set; }
        public bool IsComplete { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReminderUtc { get; set; }
        [DefaultValue(0)]
        public int ReminderAdvanceMinutes { get; set; }
        [DefaultValue(false)]
        public bool AdvanceReminderDelivered { get; set; }
        [DefaultValue(false)]
        public bool MainReminderDelivered { get; set; }

        public bool ShouldSerializeReminderUtc()
        {
            return ReminderUtc.HasValue;
        }

        public bool ShouldSerializeSubtasks()
        {
            return Subtasks.Count != 0;
        }

        internal TodoItem WithTags(List<string> tagIds)
        {
            TodoItem copy = (TodoItem)MemberwiseClone();
            copy.TagIds = tagIds;
            return copy;
        }
    }

    [Serializable]
    public class TodoSubtask
    {
        public string Title { get; set; }
        // Keep the element name used by existing XML files.
        [XmlElement("Comment")]
        public string Description { get; set; }

        [DefaultValue(false)]
        public bool IsComplete { get; set; }
    }
}
