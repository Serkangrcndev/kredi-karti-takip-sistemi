using System.Text.Json;

namespace kredi_karti_takip;

internal sealed class CardReminders(string historyPath)
{
    internal sealed record Reminder(DateOnly Date, string Key, string Text, Guid CardId, DateOnly? PaymentPeriod = null);
    private sealed record History(DateOnly Date, string[] Shown);
    private History? history;

    public IEnumerable<Reminder> GetDue(Ledger ledger, DateOnly today)
    {
        EnsureHistory(today);
        var acknowledged = history?.Date == today ? history.Shown.ToHashSet() : new HashSet<string>();
        foreach (var card in ledger.Cards)
        {
            if (card.Deleted) continue;
            var statement = $"{card.Id}:statement";
            var payment = $"{card.Id}:payment";
            if (IsToday(card.StatementDay, today) && !acknowledged.Contains(statement))
                yield return new Reminder(today, statement, $"{card.Name} · Hesap kesim günü", card.Id);
            var period = PaymentSchedule.MonthOf(today);
            if (PaymentSchedule.DueDate(card, period) == today && !acknowledged.Contains(payment) &&
                !PaymentSchedule.IsPaid(ledger, card.Id, period))
                yield return new Reminder(today, payment, $"{card.Name} · Son ödeme günü", card.Id, period);

            var upcoming = today.AddDays(3);
            var nextPeriod = PaymentSchedule.MonthOf(upcoming);
            var advance = $"{card.Id}:payment-advance";
            if (PaymentSchedule.DueDate(card, nextPeriod) == upcoming && !acknowledged.Contains(advance) &&
                !PaymentSchedule.IsPaid(ledger, card.Id, nextPeriod))
                yield return new Reminder(today, advance,
                    $"{card.Name} · Ödemeye 3 gün kaldı ({upcoming:dd.MM.yyyy})", card.Id, nextPeriod);
        }
    }

    public void Acknowledge(IEnumerable<Reminder> reminders)
    {
        foreach (var group in reminders.GroupBy(reminder => reminder.Date).OrderBy(group => group.Key))
        {
            EnsureHistory(group.Key);
            if (history?.Date > group.Key) continue;
            var acknowledged = history?.Date == group.Key ? history.Shown.ToHashSet() : new HashSet<string>();
            acknowledged.UnionWith(group.Select(reminder => reminder.Key));
            history = new History(group.Key, [.. acknowledged]);
        }

        if (history is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
            File.WriteAllText(historyPath, JsonSerializer.Serialize(history));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Program.LogError(ex);
        }
    }

    private void EnsureHistory(DateOnly today)
    {
        if (history is not null) return;
        try
        {
            if (File.Exists(historyPath))
                history = JsonSerializer.Deserialize<History>(File.ReadAllBytes(historyPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Program.LogError(ex);
        }
        if (history?.Shown is null) history = new History(today, []);
    }

    private static bool IsToday(int? day, DateOnly today) =>
        day is >= 1 and <= 31 && today.Day == Math.Min(day.Value, DateTime.DaysInMonth(today.Year, today.Month));
}
