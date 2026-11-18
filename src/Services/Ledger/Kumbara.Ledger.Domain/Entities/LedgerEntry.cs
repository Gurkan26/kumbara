using Kumbara.Ledger.Domain.Enums;

namespace Kumbara.Ledger.Domain.Entities;

/// <summary>
/// Append-only ledger entry. Once created, records cannot be updated or deleted.
/// Corrections must be issued via reversal entries.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public EntryType Type { get; private set; }
    public string Currency { get; private set; }
    public string Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core constructor
    private LedgerEntry()
    {
        Currency = string.Empty;
        Description = string.Empty;
    }

    public LedgerEntry(
        Guid transactionId,
        Guid accountId,
        decimal amount,
        EntryType type,
        string currency,
        string description)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Ledger entry amount must be positive.");
        }

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        Type = type;
        Currency = currency.Trim().ToUpperInvariant();
        Description = description ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public static LedgerEntry CreateDebit(
        Guid transactionId,
        Guid accountId,
        decimal amount,
        string currency,
        string description)
    {
        return new LedgerEntry(transactionId, accountId, amount, EntryType.Debit, currency, description);
    }

    public static LedgerEntry CreateCredit(
        Guid transactionId,
        Guid accountId,
        decimal amount,
        string currency,
        string description)
    {
        return new LedgerEntry(transactionId, accountId, amount, EntryType.Credit, currency, description);
    }
}
