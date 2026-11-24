using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskinator
{
    internal sealed class TaskReminderEditor : Panel
    {
        private readonly DateTime? originalUtc;
        private readonly CheckBox reminderCheckBox;
        private readonly DateTimePicker datePicker;
        private readonly DateTimePicker timePicker;
        private readonly CheckBox advanceCheckBox;
        private readonly NumericUpDown advanceAmount;
        private readonly ComboBox advanceUnit;

        public TaskReminderEditor(TodoItem item)
        {
            AccessibleName = L10n.Get(TextId.Reminder);
            AccessibleRole = AccessibleRole.Grouping;
            Height = 67;
            Controls.Add(new Label { AutoSize = true, Text = AccessibleName });
            originalUtc = item == null ? null : item.ReminderUtc;
            int minutes = originalUtc.HasValue ? item.ReminderAdvanceMinutes : 0;
            DateTime local = originalUtc.HasValue ? originalUtc.Value.ToLocalTime() : DateTime.Now.AddHours(1);

            reminderCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(8, 19),
                Text = L10n.Get(TextId.DateAndTime),
                Checked = originalUtc.HasValue
            };
            Controls.Add(reminderCheckBox);

            datePicker = new DateTimePicker
            {
                AccessibleName = L10n.Get(TextId.ReminderDate),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = L10n.Culture.DateTimeFormat.ShortDatePattern,
                Location = new Point(148, 16),
                Width = 92,
                Value = local
            };
            Controls.Add(datePicker);

            timePicker = new DateTimePicker
            {
                AccessibleName = L10n.Get(TextId.ReminderTime),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Location = new Point(246, 16),
                Width = 72,
                Value = local
            };
            Controls.Add(timePicker);

            advanceCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(8, 43),
                Text = L10n.Get(TextId.RemindEarly),
                Checked = minutes > 0
            };
            Controls.Add(advanceCheckBox);

            int unit = ReminderSchedule.GetUnitIndex(minutes);
            advanceAmount = new NumericUpDown
            {
                AccessibleName = L10n.Get(TextId.ReminderInterval),
                Location = new Point(148, 40),
                Width = 92,
                Minimum = 1,
                Maximum = Int32.MaxValue / ReminderSchedule.MinutesPerUnit[unit],
                Value = minutes > 0 ? minutes / ReminderSchedule.MinutesPerUnit[unit] : 15
            };
            Controls.Add(advanceAmount);

            advanceUnit = new ComboBox
            {
                AccessibleName = L10n.Get(TextId.ReminderUnit),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(246, 40),
                Width = 72
            };
            advanceUnit.Items.AddRange(ReminderSchedule.GetUnitNames());
            advanceUnit.SelectedIndex = unit;
            advanceUnit.SelectedIndexChanged += AdvanceUnit_Changed;
            Controls.Add(advanceUnit);

            reminderCheckBox.CheckedChanged += UpdateControls;
            advanceCheckBox.CheckedChanged += UpdateControls;
            UpdateControls(this, EventArgs.Empty);
        }

        public string ReadSchedule(out DateTime? utc, out int advanceMinutes)
        {
            utc = null;
            advanceMinutes = 0;
            if (!reminderCheckBox.Checked)
            {
                return null;
            }

            DateTime local = datePicker.Value.Date.AddMinutes(timePicker.Value.Hour * 60 + timePicker.Value.Minute);
            if (originalUtc.HasValue && local == AtMinute(originalUtc.Value.ToLocalTime()))
            {
                utc = originalUtc;
            }
            else
            {
                local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
                if (TimeZoneInfo.Local.IsInvalidTime(local))
                {
                    return L10n.Get(TextId.InvalidReminderTime);
                }

                utc = TimeZoneInfo.ConvertTimeToUtc(local);
                if (utc.Value <= DateTime.UtcNow)
                {
                    return L10n.Get(TextId.FutureReminderTime);
                }
            }

            if (advanceCheckBox.Checked)
            {
                advanceMinutes = (int)advanceAmount.Value * ReminderSchedule.MinutesPerUnit[advanceUnit.SelectedIndex];
            }
            return null;
        }

        private static DateTime AtMinute(DateTime time)
        {
            return time.Date.AddMinutes(time.Hour * 60 + time.Minute);
        }

        private void AdvanceUnit_Changed(object sender, EventArgs e)
        {
            advanceAmount.Maximum = Int32.MaxValue / ReminderSchedule.MinutesPerUnit[advanceUnit.SelectedIndex];
        }

        private void UpdateControls(object sender, EventArgs e)
        {
            bool enabled = reminderCheckBox.Checked;
            datePicker.Enabled = timePicker.Enabled = advanceCheckBox.Enabled = enabled;
            advanceAmount.Enabled = advanceUnit.Enabled = enabled && advanceCheckBox.Checked;
        }
    }
}
