using System;
using System.Collections.Generic;

namespace Taskinator
{
    [Serializable]
    public class TodoStore
    {
        public TodoStore()
        {
            Lists = new List<TodoList>();
        }

        public List<TodoList> Lists { get; set; }
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
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsComplete { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
