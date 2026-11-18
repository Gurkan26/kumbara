namespace Kumbara.Wallet.Domain.Entities;

using Kumbara.Wallet.Domain.Enums;
using Kumbara.Wallet.Domain.Events;

public class Wallet
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public WalletStatus Status { get; private set; }
    public decimal DailyLimit { get; private set; }
    public decimal MonthlyLimit { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected Wallet()
    {
    }

    public static Wallet Create(
        Guid userId,
        string currency,
        decimal dailyLimit = 50000m,
        decimal monthlyLimit = 200000m)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        currency = currency.Trim().ToUpperInvariant();
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        if (dailyLimit <= 0)
            throw new ArgumentException("Daily limit must be greater than zero.", nameof(dailyLimit));

        if (monthlyLimit < dailyLimit)
            throw new ArgumentException("Monthly limit cannot be less than daily limit.", nameof(monthlyLimit));

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Currency = currency,
            Status = WalletStatus.Active,
            DailyLimit = dailyLimit,
            MonthlyLimit = monthlyLimit,
            CreatedAt = DateTime.UtcNow
        };

        wallet.AddDomainEvent(new WalletCreatedDomainEvent(wallet.Id, wallet.UserId, wallet.Currency));
        return wallet;
    }

    public void Freeze(string reason)
    {
        if (Status == WalletStatus.Frozen)
            throw new InvalidOperationException("Wallet is already frozen.");

        if (Status == WalletStatus.Closed)
            throw new InvalidOperationException("Cannot freeze a closed wallet.");

        if (Status != WalletStatus.Active && Status != WalletStatus.Suspended)
            throw new InvalidOperationException($"Cannot freeze wallet in status {Status}.");

        Status = WalletStatus.Frozen;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new WalletFrozenDomainEvent(Id, reason));
    }

    public void Unfreeze()
    {
        if (Status != WalletStatus.Frozen)
            throw new InvalidOperationException("Only frozen wallets can be unfrozen.");

        Status = WalletStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new WalletUnfrozenDomainEvent(Id));
    }

    public void Suspend(string reason)
    {
        if (Status == WalletStatus.Closed)
            throw new InvalidOperationException("Cannot suspend a closed wallet.");

        if (Status == WalletStatus.Frozen)
            throw new InvalidOperationException("Cannot suspend a frozen wallet.");

        Status = WalletStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close()
    {
        if (Status == WalletStatus.Closed)
            throw new InvalidOperationException("Wallet is already closed.");

        Status = WalletStatus.Closed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLimits(decimal dailyLimit, decimal monthlyLimit)
    {
        if (Status == WalletStatus.Closed)
            throw new InvalidOperationException("Cannot update limits on a closed wallet.");

        if (dailyLimit <= 0)
            throw new ArgumentException("Daily limit must be greater than zero.", nameof(dailyLimit));

        if (monthlyLimit < dailyLimit)
            throw new ArgumentException("Monthly limit cannot be less than daily limit.", nameof(monthlyLimit));

        DailyLimit = dailyLimit;
        MonthlyLimit = monthlyLimit;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
