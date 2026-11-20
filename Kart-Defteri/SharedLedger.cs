using System.Text.Json;

namespace kredi_karti_takip;

internal static class SharedLedger
{
    internal sealed class Rejected(string message) : Exception(message);

    public static Ledger Open(string path)
    {
        using var writer = Lock(path + ".lock");
        return Read(path);
    }

    public static Ledger Read(string path)
    {
        try { return ReadSnapshot(path); }
        catch (FileNotFoundException) { return new Ledger(0, [], []); }
    }

    private static Ledger ReadSnapshot(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var state = JsonSerializer.Deserialize<Ledger>(stream);
        if (state?.Cards is null || state.Expenses is null ||
            state.Cards.Any(card => card is null || string.IsNullOrWhiteSpace(card.Name)) ||
            state.Expenses.Any(expense => expense is null) ||
            state.PaymentRecords.Any(payment => payment is null || payment.Period.Day != 1) ||
            state.Cards.Select(card => card.Id).Distinct().Count() != state.Cards.Length)
            throw new InvalidDataException("Defter dosyası eksik veya geçersiz.");
        return state;
    }

    public static string BackupFolder(string path) => Path.Combine(Path.GetDirectoryName(path)!, "Yedekler");

    public static Ledger Save(string path, object entry)
    {
        using var writer = Lock(path + ".lock");
        if (entry is RestoreLedger restore) return Restore(path, restore.BackupPath);
        var state = Read(path);
        var next = entry switch
        {
            NewCard card => AddCard(state, card),
            UpdateCard edit => EditCard(state, edit),
            DeleteCard deletion => RemoveCard(state, deletion),
            NewExpense expense => AddExpense(state, expense),
            UpdateExpense editExpense => EditExpense(state, editExpense),
            SetCardPayment payment => ChangePayment(state, payment),
            _ => throw new ArgumentException("Kayıt türü geçersiz.", nameof(entry))
        };
        if (ReferenceEquals(next, state)) return state;

        next = next with { Version = state.Version + 1 };
        var backup = Path.Combine(BackupFolder(path), $"defter-{DateTime.Today:yyyy-MM-dd}.json");
        if (!File.Exists(backup))
        {
            Directory.CreateDirectory(BackupFolder(path));
            WriteSnapshot(backup, File.Exists(path) ? state : next);
        }
        WriteSnapshot(path, next);
        return next;
    }

    private static Ledger Restore(string path, string backupPath)
    {
        var backup = ReadSnapshot(backupPath);
        Directory.CreateDirectory(BackupFolder(path));
        long version = DateTime.UtcNow.Ticks;
        if (File.Exists(path))
        {
            string checkpoint = Path.Combine(BackupFolder(path),
                $"geri-yukleme-oncesi-{DateTime.Now:yyyy-MM-dd-HHmmss}-{Guid.NewGuid():N}.json");
            try
            {
                var current = ReadSnapshot(path);
                version = Math.Max(version, current.Version + 1);
                WriteSnapshot(checkpoint, current);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                File.Copy(path, Path.ChangeExtension(checkpoint, ".invalid"));
            }
        }
        var restored = backup with { Version = version };
        WriteSnapshot(path, restored);
        return restored;
    }

    private static void WriteSnapshot(string path, Ledger next)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, next);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static FileStream Lock(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) when (attempt < 20 && (ex.HResult & 0xffff) is 32 or 33)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static Ledger AddCard(Ledger state, NewCard card)
    {
        if (state.Cards.Any(existing => existing.Id == card.Id)) return state;
        ValidateCard(state, card.Id, card.Name, card.StatementDay, card.PaymentDay);
        return state with
        {
            Cards = [.. state.Cards, new Card(card.Id, card.Name.Trim(), card.Bank.Trim(), card.StatementDay, card.PaymentDay)]
        };
    }

    private static Ledger EditCard(Ledger state, UpdateCard edit)
    {
        int index = Array.FindIndex(state.Cards, card => card.Id == edit.Original.Id && !card.Deleted);
        if (index < 0) throw new Rejected("Kart artık aktif değil.");
        var current = state.Cards[index];
        ValidateCard(state, current.Id, edit.Name, edit.StatementDay, edit.PaymentDay);
        var updated = current with
        {
            Name = edit.Name.Trim(),
            Bank = edit.Bank.Trim(),
            StatementDay = edit.StatementDay,
            PaymentDay = edit.PaymentDay
        };
        if (updated == current) return state;
        if (current != edit.Original)
            throw new Rejected("Kart başka bir bilgisayarda değiştirildi. Pencereyi kapatıp kartı yeniden açın.");
        return ReplaceCard(state, index, updated);
    }

    private static Ledger RemoveCard(Ledger state, DeleteCard deletion)
    {
        int index = Array.FindIndex(state.Cards, card => card.Id == deletion.Id && !card.Deleted);
        if (index < 0) return state;
        return ReplaceCard(state, index, state.Cards[index] with { Deleted = true });
    }

    private static Ledger ReplaceCard(Ledger state, int index, Card card)
    {
        var cards = (Card[])state.Cards.Clone();
        cards[index] = card;
        return state with { Cards = cards };
    }

    private static void ValidateCard(Ledger state, Guid id, string name, int? statementDay, int? paymentDay)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new Rejected("Kart adı girin.");
        if (state.Cards.Any(card => !card.Deleted && card.Id != id &&
            string.Equals(card.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new Rejected("Bu kart adı zaten kullanılıyor.");
        if (statementDay is < 1 or > 31 || paymentDay is < 1 or > 31)
            throw new Rejected("Kart günleri 1 ile 31 arasında olmalı.");
    }

    private static Ledger AddExpense(Ledger state, NewExpense expense)
    {
        if (state.Expenses.Any(existing => existing.Id == expense.Id)) return state;
        ValidateExpense(state, expense.CardId, expense.Cents, expense.Note);
        return state with
        {
            Expenses = [.. state.Expenses, new Expense(expense.Id, expense.CardId, expense.Cents,
                expense.Date, expense.Note.Trim())]
        };
    }

    private static Ledger EditExpense(Ledger state, UpdateExpense edit)
    {
        int index = Array.FindIndex(state.Expenses, expense => expense.Id == edit.Original.Id);
        if (index < 0) throw new Rejected("Harcama artık defterde bulunmuyor.");
        var current = state.Expenses[index];
        ValidateExpense(state, edit.CardId, edit.Cents, edit.Note,
            allowInactive: edit.CardId == current.CardId);
        var replacement = current with { CardId = edit.CardId, Cents = edit.Cents, Date = edit.Date, Note = edit.Note.Trim() };
        if (current == replacement) return state;
        if (current != edit.Original)
            throw new Rejected("Harcama başka bir bilgisayarda değiştirildi. Pencereyi kapatıp kaydı yeniden açın.");
        var expenses = (Expense[])state.Expenses.Clone();
        expenses[index] = replacement;
        return state with { Expenses = expenses };
    }

    private static void ValidateExpense(Ledger state, Guid cardId, long cents, string note, bool allowInactive = false)
    {
        if (cents <= 0) throw new Rejected("Sıfırdan büyük bir tutar girin.");
        if (string.IsNullOrWhiteSpace(note)) throw new Rejected("Harcama açıklaması girin.");
        if (!allowInactive && !state.Cards.Any(card => card.Id == cardId && !card.Deleted))
            throw new Rejected("Kart artık aktif değil. Listeden başka bir kart seçin.");
    }

    private static Ledger ChangePayment(Ledger state, SetCardPayment payment)
    {
        var period = PaymentSchedule.MonthOf(payment.Period);
        var existing = state.PaymentRecords.FirstOrDefault(item => item.CardId == payment.CardId && item.Period == period);
        if ((existing is not null) == payment.PaidOn.HasValue) return state;
        var card = state.Cards.FirstOrDefault(card => card.Id == payment.CardId && !card.Deleted);
        if (card is null || (payment.PaidOn is not null && PaymentSchedule.DueDate(card, period) is null))
            throw new Rejected("Ödeme takibi için aktif karta bir son ödeme günü girin.");
        var payments = state.PaymentRecords.Where(item => item.CardId != payment.CardId || item.Period != period);
        return state with
        {
            Payments = payment.PaidOn is DateOnly paidOn
                ? [.. payments, new CardPayment(payment.CardId, period, paidOn)]
                : [.. payments]
        };
    }
}
