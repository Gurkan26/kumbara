namespace Kumbara.Ledger.Application.DTOs;

public record AccountBalanceDto(
    Guid AccountId,
    string Currency,
    decimal AvailableBalance,
    decimal BlockedBalance,
    decimal TotalBalance
);
