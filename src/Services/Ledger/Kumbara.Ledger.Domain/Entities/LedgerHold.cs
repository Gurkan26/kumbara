using Kumbara.Ledger.Domain.Enums;

namespace Kumbara.Ledger.Domain.Entities;

public class LedgerHold
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public HoldStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    // EF Core constructor
    private LedgerHold()
    {
        Currency = string.Empty;
    }

    public LedgerHold(Guid transactionId, Guid accountId, decimal amount, string currency)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Hold amount must be positive.");
        }

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
        Status = HoldStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public void Commit()
    {
        if (Status != HoldStatus.Active)
        {
            throw new InvalidOperationException($"Cannot commit hold with status {Status}.");
        }

        Status = HoldStatus.Committed;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        if (Status != HoldStatus.Active)
        {
            throw new InvalidOperationException($"Cannot release hold with status {Status}.");
        }

        Status = HoldStatus.Released;
        ResolvedAt = DateTime.UtcNow;
    }
}
