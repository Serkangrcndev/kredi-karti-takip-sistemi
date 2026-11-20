using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using kredi_karti_takip;

var builder = WebApplication.CreateBuilder(args);
var key = builder.Configuration["LEDGER_KEY"];
if (string.IsNullOrWhiteSpace(key) || key.Length < 24)
    throw new InvalidOperationException("LEDGER_KEY en az 24 karakter olmalıdır.");

var dataPath = Path.GetFullPath(builder.Configuration["LEDGER_DATA"] ?? "data/ledger.json");
Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
using var writerLock = File.Open(dataPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var state = File.Exists(dataPath)
    ? JsonSerializer.Deserialize<Ledger>(File.ReadAllBytes(dataPath)) ?? throw new InvalidDataException(dataPath)
    : new Ledger(0, [], []);
var gate = new object();
var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + key));
var app = builder.Build();

app.Use(async (context, next) =>
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString()));
    if (!CryptographicOperations.FixedTimeEquals(hash, keyHash))
    {
        context.Response.StatusCode = 401;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.MapGet("/api/ledger", () =>
{
    lock (gate) return state;
});

app.MapPost("/api/cards", (NewCard card) =>
{
    if (card.Id == Guid.Empty || !Valid(card.Name, 60) || !Valid(card.Bank, 60, optional: true))
        return Error("Kart adı 1–60 karakter olmalı. Banka adı en çok 60 karakter olabilir.");

    lock (gate)
    {
        if (state.Cards.Any(existing => existing.Id == card.Id)) return Results.Ok(state);
        if (state.Cards.Any(existing => string.Equals(existing.Name, card.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Error("Bu kart adı zaten kullanılıyor.");

        return Commit(state with { Cards = [.. state.Cards, new Card(card.Id, card.Name.Trim(), card.Bank.Trim())] });
    }
});

app.MapPost("/api/expenses", (NewExpense expense) =>
{
    if (expense.Id == Guid.Empty || expense.Cents is <= 0 or > 99_999_999_999L ||
        expense.Date.Year < 1900 || !Valid(expense.Note, 240) || !Valid(expense.Author, 60))
        return Error("Tutar, tarih, açıklama veya kullanıcı adı geçersiz.");

    lock (gate)
    {
        if (state.Expenses.Any(existing => existing.Id == expense.Id)) return Results.Ok(state);
        if (!state.Cards.Any(card => card.Id == expense.CardId)) return Error("Kart bulunamadı.");

        return Commit(state with
        {
            Expenses = [.. state.Expenses, new Expense(expense.Id, expense.CardId, expense.Cents,
                expense.Date, expense.Note.Trim(), expense.Author!.Trim())]
        });
    }
});

app.Run();

IResult Commit(Ledger next)
{
    next = next with { Version = state.Version + 1 };
    string temporary = dataPath + ".tmp";
    using (var stream = File.Create(temporary))
    {
        JsonSerializer.Serialize(stream, next);
        stream.Flush(flushToDisk: true);
    }
    File.Move(temporary, dataPath, overwrite: true);
    state = next;
    return Results.Ok(state);
}

static bool Valid(string? value, int max, bool optional = false) =>
    value is not null && value.Length <= max && (optional || !string.IsNullOrWhiteSpace(value));

static IResult Error(string message) => Results.BadRequest(new { message });
