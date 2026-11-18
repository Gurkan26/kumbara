namespace Kumbara.Ledger.Domain.Exceptions;

public class AccountFrozenException : Exception
{
    public Guid AccountId { get; }

    public AccountFrozenException(Guid accountId)
        : base($"Account {accountId} is frozen and cannot perform transactions.")
    {
        AccountId = accountId;
    }

    public AccountFrozenException(string message) : base(message)
    {
    }
}
