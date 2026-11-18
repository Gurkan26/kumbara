namespace Kumbara.Ledger.Domain.Exceptions;

public class InsufficientFundsException : Exception
{
    public Guid AccountId { get; }
    public decimal RequestedAmount { get; }
    public decimal AvailableBalance { get; }

    public InsufficientFundsException(Guid accountId, decimal requestedAmount, decimal availableBalance)
        : base($"Insufficient funds for account {accountId}. Requested: {requestedAmount}, Available: {availableBalance}")
    {
        AccountId = accountId;
        RequestedAmount = requestedAmount;
        AvailableBalance = availableBalance;
    }

    public InsufficientFundsException(string message) : base(message)
    {
    }
}
