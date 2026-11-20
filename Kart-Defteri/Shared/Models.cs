using System.Text.Json.Serialization;

namespace kredi_karti_takip;

public sealed record Card(Guid Id, string Name, string Bank, int? StatementDay = null, int? PaymentDay = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Deleted = false);
public sealed record Expense(Guid Id, Guid CardId, long Cents, DateOnly Date, string Note,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Author = null);
public sealed record Ledger(long Version, Card[] Cards, Expense[] Expenses, CardPayment[]? Payments = null)
{
    [JsonIgnore]
    public CardPayment[] PaymentRecords => Payments ?? [];
}
public sealed record CardPayment(Guid CardId, DateOnly Period, DateOnly PaidOn);
public sealed record SetCardPayment(Guid CardId, DateOnly Period, DateOnly? PaidOn);
public sealed record UpdateExpense(Expense Original, Guid CardId, long Cents, DateOnly Date, string Note);
public sealed record RestoreLedger(string BackupPath);
public sealed record NewCard(Guid Id, string Name, string Bank, int? StatementDay = null, int? PaymentDay = null);
public sealed record UpdateCard(Card Original, string Name, string Bank, int? StatementDay, int? PaymentDay);
public sealed record DeleteCard(Guid Id);
public sealed record NewExpense(Guid Id, Guid CardId, long Cents, DateOnly Date, string Note, string? Author = null);
