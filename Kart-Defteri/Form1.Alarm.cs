using System.Media;

namespace kredi_karti_takip;

public partial class Form1
{
    private static readonly Icon BrandIcon = LoadBrandIcon();
    private readonly List<CardReminders.Reminder> pendingReminders = [];
    private Form? alarmWindow;
    private ListBox? alarmList;
    private SoundPlayer? alarmSound;
    private System.Windows.Forms.Timer? fallbackAlarm;

    private static Icon LoadBrandIcon()
    {
        using var stream = typeof(Form1).Assembly.GetManifestResourceStream("KartDefteri.Icon")!;
        using var icon = new Icon(stream, 48, 48);
        return (Icon)icon.Clone();
    }

    private static PictureBox CardLogo() => new()
    {
        Size = new Size(50, 50), SizeMode = PictureBoxSizeMode.CenterImage, Image = BrandIcon.ToBitmap()
    };

    private void ShowDueReminders()
    {
        try
        {
            var active = ActiveCards.Select(card => card.Id).ToHashSet();
            int removed = pendingReminders.RemoveAll(item => !active.Contains(item.CardId) ||
                (item.PaymentPeriod is DateOnly period && PaymentSchedule.IsPaid(ledger, item.CardId, period)));
            if (removed > 0 && alarmList is not null)
            {
                alarmList.Items.Clear();
                foreach (var item in pendingReminders) alarmList.Items.Add(item.Text);
            }
            var due = reminders.GetDue(ledger, DateOnly.FromDateTime(DateTime.Today));
            foreach (var reminder in due)
            {
                if (pendingReminders.Any(item => item.Date == reminder.Date && item.Key == reminder.Key)) continue;
                pendingReminders.Add(reminder);
                alarmList?.Items.Add(reminder.Text);
            }

            if (pendingReminders.Count == 0 && alarmWindow is not null) CloseReminderAlarm();
            else if (pendingReminders.Count > 0 && alarmWindow is null) OpenReminderAlarm();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Program.LogError(ex);
        }
    }

    private void OpenReminderAlarm()
    {
        var window = new Form
        {
            Text = "Kart Defteri · Kart alarmı", Icon = BrandIcon, TopMost = true,
            BackColor = Canvas, StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, ControlBox = false,
            ClientSize = new Size(465, 300), Font = new Font("Segoe UI", 10)
        };
        Place(window, CardLogo(), 23, 20);
        Place(window, LabelFor("Kart alarmı", 20, Ink, true), 82, 20);
        Place(window, LabelFor("Bugünün kart hatırlatmaları", 10, Muted), 84, 54);
        var list = new ListBox
        {
            Size = new Size(415, 132), IntegralHeight = false,
            BackColor = Color.White, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle
        };
        foreach (var reminder in pendingReminders) list.Items.Add(reminder.Text);
        Place(window, list, 25, 90);

        var acknowledge = MakeButton("Hatırlattım", true, 140);
        Place(window, acknowledge, 300, 241);
        acknowledge.Click += (_, _) =>
        {
            reminders.Acknowledge(pendingReminders);
            pendingReminders.Clear();
            CloseReminderAlarm();
        };
        window.FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing && alarmWindow == window)
                args.Cancel = true;
        };

        alarmWindow = window;
        alarmList = list;
        window.Show();
        window.Activate();
        StartAlarmSound();
    }

    private void StartAlarmSound()
    {
        try
        {
            alarmSound = new SoundPlayer(typeof(Form1).Assembly.GetManifestResourceStream("KartDefteri.Alarm"));
            alarmSound.Load();
            alarmSound.PlayLooping();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Program.LogError(ex);
            fallbackAlarm = new System.Windows.Forms.Timer { Interval = 2500 };
            fallbackAlarm.Tick += (_, _) => SystemSounds.Exclamation.Play();
            SystemSounds.Exclamation.Play();
            fallbackAlarm.Start();
        }
    }

    private void CloseReminderAlarm()
    {
        fallbackAlarm?.Dispose();
        fallbackAlarm = null;
        if (alarmSound is not null)
        {
            alarmSound.Stop();
            alarmSound.Stream?.Dispose();
            alarmSound.Dispose();
            alarmSound = null;
        }

        var window = alarmWindow;
        alarmWindow = null;
        alarmList = null;
        window?.Dispose();
    }
}
