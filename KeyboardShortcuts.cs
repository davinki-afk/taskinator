using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Taskinator
{
    internal static class KeyboardShortcuts
    {
        private static readonly KeyValuePair<TextId, Keys>[] defaults =
        {
            Binding(TextId.CreateList, Keys.Control | Keys.Shift | Keys.N),
            Binding(TextId.RenameList, Keys.Control | Keys.Shift | Keys.E),
            Binding(TextId.DeleteList, Keys.Control | Keys.Shift | Keys.D),
            Binding(TextId.CreateTask, Keys.Control | Keys.N),
            Binding(TextId.EditTask, Keys.Control | Keys.E),
            Binding(TextId.DeleteTask, Keys.Control | Keys.D),
            Binding(TextId.Settings, Keys.Control | Keys.Alt | Keys.S),
            Binding(TextId.Search, Keys.Control | Keys.F),
            Binding(TextId.About, Keys.Control | Keys.Alt | Keys.A)
        };
        private static readonly KeysConverter converter = new KeysConverter();

        internal static IEnumerable<TextId> Actions
        {
            get { return defaults.Select(binding => binding.Key); }
        }

        internal static Dictionary<TextId, Keys> Load(TodoStore store)
        {
            Dictionary<TextId, Keys> bindings = CreateDefaults();
            foreach (ShortcutBinding custom in store.ShortcutOverrides)
            {
                TextId action;
                Keys keys = (Keys)custom.KeyData;
                if (Enum.TryParse(custom.Action, out action) && bindings.ContainsKey(action) &&
                    (keys == Keys.None || IsSupported(keys)))
                {
                    bindings[action] = keys;
                }
            }

            var assigned = bindings.Values.Where(keys => keys != Keys.None);
            return assigned.Count() == assigned.Distinct().Count() ? bindings : CreateDefaults();
        }

        internal static Dictionary<TextId, Keys> CreateDefaults()
        {
            return defaults.ToDictionary(binding => binding.Key, binding => binding.Value);
        }

        internal static List<ShortcutBinding> GetOverrides(Dictionary<TextId, Keys> bindings)
        {
            return defaults.Where(binding => binding.Value != bindings[binding.Key])
                .Select(binding => new ShortcutBinding
                {
                    Action = binding.Key.ToString(),
                    KeyData = (int)bindings[binding.Key]
                }).ToList();
        }

        internal static bool IsSupported(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            return (modifiers & (Keys.Control | Keys.Alt)) != Keys.None &&
                (modifiers & ~(Keys.Control | Keys.Alt | Keys.Shift)) == Keys.None &&
                (key != Keys.F4 || (modifiers & Keys.Alt) == Keys.None) &&
                ((key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9) ||
                 (key >= Keys.F1 && key <= Keys.F12));
        }

        internal static string Format(Keys keys)
        {
            return converter.ConvertToInvariantString(keys);
        }

        internal static string WithShortcut(string caption, Keys keys)
        {
            return keys == Keys.None ? caption : caption + " (" + Format(keys) + ")";
        }

        private static KeyValuePair<TextId, Keys> Binding(TextId action, Keys keys)
        {
            return new KeyValuePair<TextId, Keys>(action, keys);
        }
    }
}
