namespace Kumbara.Wallet.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public record WalletCreatedDomainEvent(
    Guid WalletId,
    Guid UserId,
    string Currency,
    DateTime OccurredAt
) : IDomainEvent
{
    public WalletCreatedDomainEvent(Guid walletId, Guid userId, string currency)
        : this(walletId, userId, currency, DateTime.UtcNow)
    {
    }
}

public record WalletFrozenDomainEvent(
    Guid WalletId,
    string Reason,
    DateTime OccurredAt
) : IDomainEvent
{
    public WalletFrozenDomainEvent(Guid walletId, string reason)
        : this(walletId, reason, DateTime.UtcNow)
    {
    }
}

public record WalletUnfrozenDomainEvent(
    Guid WalletId,
    DateTime OccurredAt
) : IDomainEvent
{
    public WalletUnfrozenDomainEvent(Guid walletId)
        : this(walletId, DateTime.UtcNow)
    {
    }
}
