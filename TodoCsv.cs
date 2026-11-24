using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Taskinator
{
    internal static class TodoCsv
    {
        private static readonly string[] Columns =
        {
            "kind", "id", "list_id", "tag_id", "name", "description", "icon_key", "color_index",
            "completed", "created_at", "reminder_utc", "advance_minutes", "advance_delivered", "main_delivered"
        };
        private const int Kind = 0;
        private const int Id = 1;
        private const int ListId = 2;
        private const int TagId = 3;
        private const int Name = 4;
        private const int Description = 5;
        private const int IconKey = 6;
        private const int ColorIndex = 7;
        private const int Completed = 8;
        private const int CreatedAt = 9;
        private const int ReminderUtc = 10;
        private const int AdvanceMinutes = 11;
        private const int AdvanceDelivered = 12;
        private const int MainDelivered = 13;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly char[] QuotedCharacters = { ',', '"', '\r', '\n' };

        public static void Export(string path, TodoStore store)
        {
            Storage.WriteFile(path, delegate(Stream stream)
            {
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(true, true)))
                {
                    writer.NewLine = "\r\n";
                    WriteRow(writer, Columns);
                    HashSet<string> knownTags = new HashSet<string>();
                    foreach (TodoTag tag in store.Tags)
                    {
                        knownTags.Add(tag.Id);
                        string[] row = MakeRow("tag", tag.Id);
                        row[Name] = tag.Name;
                        row[ColorIndex] = tag.ColorIndex.ToString(Invariant);
                        WriteRow(writer, row);
                    }

                    foreach (TodoList list in store.Lists)
                    {
                        string[] row = MakeRow("list", list.Id);
                        row[Name] = list.Name;
                        row[IconKey] = list.IconKey;
                        WriteRow(writer, row);
                        foreach (TodoItem task in list.Items)
                        {
                            WriteTask(writer, list.Id, task);
                            // Linked rows use the owning task's ID, as task_tag rows do.
                            foreach (TodoSubtask subtask in task.Subtasks)
                            {
                                string[] step = MakeRow("subtask", task.Id);
                                step[Name] = subtask.Title;
                                step[Description] = subtask.Description;
                                step[Completed] = subtask.IsComplete.ToString();
                                WriteRow(writer, step);
                            }
                            foreach (string tagId in task.TagIds.Where(knownTags.Contains))
                            {
                                string[] link = MakeRow("task_tag", task.Id);
                                link[TagId] = tagId;
                                WriteRow(writer, link);
                            }
                        }
                    }
                }
            });
        }

        public static TodoStore Read(string path)
        {
            List<string[]> rows;
            using (StreamReader reader = new StreamReader(path, new UTF8Encoding(false, true), true))
            {
                rows = ReadRows(reader).Where(row => row.Length != 1 || !String.IsNullOrWhiteSpace(row[0])).ToList();
            }
            if (rows.Count == 0 || !Columns.SequenceEqual(rows[0]))
            {
                throw new InvalidDataException(L10n.Get(TextId.InvalidCsvFormat));
            }

            TodoStore result = new TodoStore();
            Dictionary<string, TodoList> lists = new Dictionary<string, TodoList>();
            Dictionary<string, TodoTag> tags = new Dictionary<string, TodoTag>();
            Dictionary<string, TodoItem> tasks = new Dictionary<string, TodoItem>();
            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                if (row.Length != Columns.Length)
                {
                    throw new InvalidDataException(L10n.Format(TextId.InvalidCsvColumnCount, i + 1));
                }

                switch (row[Kind])
                {
                    case "tag":
                        TodoTag tag = new TodoTag { Id = ReadId(row, Id), Name = row[Name], ColorIndex = ReadInt(row, ColorIndex) };
                        AddUnique(tags, tag.Id, tag);
                        result.Tags.Add(tag);
                        break;
                    case "list":
                        TodoList list = new TodoList { Id = ReadId(row, Id), Name = row[Name], IconKey = ReadIconKey(row[IconKey]) };
                        AddUnique(lists, list.Id, list);
                        result.Lists.Add(list);
                        break;
                    case "task":
                    case "task_tag":
                    case "subtask":
                        break;
                    default:
                        throw InvalidField(Kind);
                }
            }

            foreach (string[] row in rows.Where(row => row[Kind] == "task"))
            {
                TodoList list;
                if (!lists.TryGetValue(row[ListId], out list))
                {
                    throw new InvalidDataException(L10n.Get(TextId.CsvListMissing));
                }
                TodoItem task = ReadTask(row);
                AddUnique(tasks, task.Id, task);
                list.Items.Add(task);
            }
            foreach (string[] row in rows.Where(row => row[Kind] == "task_tag"))
            {
                TodoItem task;
                string tagId = row[TagId];
                if (!tasks.TryGetValue(row[Id], out task) || !tags.ContainsKey(tagId))
                {
                    throw new InvalidDataException(L10n.Get(TextId.CsvTagLinkMissing));
                }
                if (!task.TagIds.Contains(tagId))
                {
                    task.TagIds.Add(tagId);
                }
            }
            foreach (string[] row in rows.Where(row => row[Kind] == "subtask"))
            {
                TodoItem task;
                if (!tasks.TryGetValue(row[Id], out task))
                {
                    throw new InvalidDataException(L10n.Get(TextId.CsvSubtaskParentMissing));
                }
                if (String.IsNullOrWhiteSpace(row[Name]))
                {
                    throw InvalidField(Name);
                }
                task.Subtasks.Add(new TodoSubtask
                {
                    Title = row[Name],
                    Description = row[Description].Length == 0 ? null : row[Description],
                    IsComplete = ReadBool(row, Completed)
                });
            }
            return result;
        }

        private static void WriteTask(TextWriter writer, string listId, TodoItem task)
        {
            string[] row = MakeRow("task", task.Id);
            row[ListId] = listId;
            row[Name] = task.Title;
            row[Description] = task.Description;
            row[ColorIndex] = task.TitleColorIndex.ToString(Invariant);
            row[Completed] = task.IsComplete.ToString();
            row[CreatedAt] = task.CreatedAt.ToString("o", Invariant);
            row[ReminderUtc] = task.ReminderUtc.HasValue ? task.ReminderUtc.Value.ToString("o", Invariant) : null;
            row[AdvanceMinutes] = task.ReminderAdvanceMinutes.ToString(Invariant);
            row[AdvanceDelivered] = task.AdvanceReminderDelivered.ToString();
            row[MainDelivered] = task.MainReminderDelivered.ToString();
            WriteRow(writer, row);
        }

        private static TodoItem ReadTask(string[] row)
        {
            int minutes = ReadInt(row, AdvanceMinutes);
            if (minutes < 0)
            {
                throw InvalidField(AdvanceMinutes);
            }
            return new TodoItem
            {
                Id = ReadId(row, Id),
                Title = row[Name],
                Description = row[Description],
                TitleColorIndex = ReadInt(row, ColorIndex),
                IsComplete = ReadBool(row, Completed),
                CreatedAt = ReadDate(row, CreatedAt),
                ReminderUtc = row[ReminderUtc].Length == 0 ? (DateTime?)null : ReadDate(row, ReminderUtc).ToUniversalTime(),
                ReminderAdvanceMinutes = minutes,
                AdvanceReminderDelivered = ReadBool(row, AdvanceDelivered),
                MainReminderDelivered = ReadBool(row, MainDelivered)
            };
        }

        private static string ReadId(string[] row, int column)
        {
            if (String.IsNullOrWhiteSpace(row[column]))
            {
                throw InvalidField(column);
            }
            return row[column];
        }

        private static string ReadIconKey(string value)
        {
            if (!ListIconCatalog.IsValidKey(value))
            {
                throw InvalidField(IconKey);
            }
            return value;
        }

        private static int ReadInt(string[] row, int column)
        {
            int value;
            if (!Int32.TryParse(row[column], NumberStyles.Integer, Invariant, out value))
            {
                throw InvalidField(column);
            }
            return value;
        }

        private static bool ReadBool(string[] row, int column)
        {
            bool value;
            if (!Boolean.TryParse(row[column], out value))
            {
                throw InvalidField(column);
            }
            return value;
        }

        private static DateTime ReadDate(string[] row, int column)
        {
            DateTime value;
            if (!DateTime.TryParseExact(row[column], "o", Invariant, DateTimeStyles.RoundtripKind, out value))
            {
                throw InvalidField(column);
            }
            return value;
        }

        private static void AddUnique<T>(Dictionary<string, T> entries, string id, T value)
        {
            if (entries.ContainsKey(id))
            {
                throw new InvalidDataException(L10n.Get(TextId.DuplicateCsvId));
            }
            entries.Add(id, value);
        }

        private static InvalidDataException InvalidField(int column)
        {
            return new InvalidDataException(L10n.Format(TextId.InvalidCsvField, Columns[column]));
        }

        private static string[] MakeRow(string kind, string id)
        {
            string[] row = new string[Columns.Length];
            row[Kind] = kind;
            row[Id] = id;
            return row;
        }

        private static void WriteRow(TextWriter writer, string[] row)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }
                string value = row[i] ?? String.Empty;
                writer.Write(value.IndexOfAny(QuotedCharacters) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"");
            }
            writer.WriteLine();
        }

        private static IEnumerable<string[]> ReadRows(TextReader reader)
        {
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;
            bool closed = false;
            int next;
            while ((next = reader.Read()) >= 0)
            {
                char c = (char)next;
                if (quoted)
                {
                    if (c != '"' || reader.Peek() == '"')
                    {
                        if (c == '"') reader.Read();
                        field.Append(c);
                    }
                    else
                    {
                        quoted = false;
                        closed = true;
                    }
                }
                else if (c == ',' || c == '\r' || c == '\n')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                    closed = false;
                    if (c != ',')
                    {
                        if (c == '\r' && reader.Peek() == '\n') reader.Read();
                        yield return row.ToArray();
                        row.Clear();
                    }
                }
                else if (c == '"' && field.Length == 0 && !closed)
                {
                    quoted = true;
                }
                else if (closed || c == '"')
                {
                    throw new InvalidDataException(L10n.Get(TextId.InvalidCsvQuotes));
                }
                else
                {
                    field.Append(c);
                }
            }
            if (quoted)
            {
                throw new InvalidDataException(L10n.Get(TextId.UnclosedCsvQuote));
            }
            if (row.Count > 0 || field.Length > 0 || closed)
            {
                row.Add(field.ToString());
                yield return row.ToArray();
            }
        }
    }
}
