using System;

namespace Taskinator
{
    internal enum ReminderKind
    {
        None,
        Advance,
        Main
    }

    internal static class ReminderSchedule
    {
        public static readonly int[] MinutesPerUnit = { 1, 60, 1440 };
        private static readonly TextId[] Units = { TextId.Minutes, TextId.Hours, TextId.Days };

        public static string[] GetUnitNames()
        {
            return Array.ConvertAll(Units, L10n.Get);
        }

        public static void Update(TodoItem task, DateTime? utc, int advanceMinutes)
        {
            bool timeChanged = task.ReminderUtc != utc;
            if (timeChanged)
            {
                task.MainReminderDelivered = false;
            }
            if (timeChanged || task.ReminderAdvanceMinutes != advanceMinutes)
            {
                task.AdvanceReminderDelivered = false;
            }

            task.ReminderUtc = utc;
            task.ReminderAdvanceMinutes = advanceMinutes;
        }

        public static ReminderKind GetDue(TodoItem task, DateTime nowUtc)
        {
            if (task.IsComplete || !task.ReminderUtc.HasValue || task.MainReminderDelivered)
            {
                return ReminderKind.None;
            }

            TimeSpan remaining = task.ReminderUtc.Value - nowUtc;
            if (remaining <= TimeSpan.Zero)
            {
                return ReminderKind.Main;
            }
            if (!task.AdvanceReminderDelivered && task.ReminderAdvanceMinutes > 0 &&
                remaining.TotalMinutes <= task.ReminderAdvanceMinutes)
            {
                return ReminderKind.Advance;
            }
            return ReminderKind.None;
        }

        public static int GetUnitIndex(int minutes)
        {
            for (int i = MinutesPerUnit.Length - 1; i > 0; i--)
            {
                if (minutes > 0 && minutes % MinutesPerUnit[i] == 0)
                {
                    return i;
                }
            }
            return 0;
        }

        public static string FormatLocalTime(DateTime utc)
        {
            DateTime local = utc.ToLocalTime();
            return local.ToString("d", L10n.Culture) + local.ToString(" HH:mm", L10n.Culture);
        }

        public static string FormatAdvance(int minutes)
        {
            int unit = GetUnitIndex(minutes);
            return (minutes / MinutesPerUnit[unit]).ToString(L10n.Culture) + " " + L10n.Get(Units[unit]);
        }
    }
}
