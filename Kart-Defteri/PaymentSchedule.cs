namespace kredi_karti_takip;

internal static class PaymentSchedule
{
    public static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly? DueDate(Card card, DateOnly period) => card.PaymentDay is int day && day is >= 1 and <= 31
        ? new DateOnly(period.Year, period.Month, Math.Min(day, DateTime.DaysInMonth(period.Year, period.Month)))
        : null;

    public static bool IsPaid(Ledger ledger, Guid cardId, DateOnly period) =>
        ledger.PaymentRecords.Any(payment => payment.CardId == cardId && payment.Period == MonthOf(period));
}
