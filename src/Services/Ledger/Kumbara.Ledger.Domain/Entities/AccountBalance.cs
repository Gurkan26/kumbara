using Kumbara.Ledger.Domain.Exceptions;

namespace Kumbara.Ledger.Domain.Entities;

/// <summary>
/// Snapshot balance entity with optimistic concurrency token (RowVersion).
/// </summary>
public class AccountBalance
{
    public Guid AccountId { get; private set; }
    public string Currency { get; private set; }
    public decimal AvailableBalance { get; private set; }
    public decimal BlockedBalance { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public decimal TotalBalance => AvailableBalance + BlockedBalance;

    // EF Core constructor
    private AccountBalance()
    {
        Currency = string.Empty;
    }

    public AccountBalance(Guid accountId, string currency, decimal initialAvailable = 0m, decimal initialBlocked = 0m)
    {
        AccountId = accountId;
        Currency = currency.Trim().ToUpperInvariant();
        if (initialAvailable < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialAvailable), "Initial available balance cannot be negative.");
        }
        if (initialBlocked < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialBlocked), "Initial blocked balance cannot be negative.");
        }

        AvailableBalance = decimal.Round(initialAvailable, 4, MidpointRounding.AwayFromZero);
        BlockedBalance = decimal.Round(initialBlocked, 4, MidpointRounding.AwayFromZero);
    }

    public void Credit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be positive.");
        }

        var roundedAmount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        AvailableBalance += roundedAmount;
    }

    public void Debit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be positive.");
        }

        var roundedAmount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (AvailableBalance < roundedAmount)
        {
            throw new InsufficientFundsException(AccountId, roundedAmount, AvailableBalance);
        }

        AvailableBalance -= roundedAmount;
    }

    public void Hold(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Hold amount must be positive.");
        }

        var roundedAmount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (AvailableBalance < roundedAmount)
        {
            throw new InsufficientFundsException(AccountId, roundedAmount, AvailableBalance);
        }

        AvailableBalance -= roundedAmount;
        BlockedBalance += roundedAmount;
    }

    public void ReleaseHold(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Release amount must be positive.");
        }

        var roundedAmount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (BlockedBalance < roundedAmount)
        {
            throw new InvalidOperationException($"Cannot release {roundedAmount} because blocked balance is only {BlockedBalance}.");
        }

        BlockedBalance -= roundedAmount;
        AvailableBalance += roundedAmount;
    }

    public void CommitHold(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Commit amount must be positive.");
        }

        var roundedAmount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (BlockedBalance < roundedAmount)
        {
            throw new InvalidOperationException($"Cannot commit {roundedAmount} because blocked balance is only {BlockedBalance}.");
        }

        BlockedBalance -= roundedAmount;
    }
}
