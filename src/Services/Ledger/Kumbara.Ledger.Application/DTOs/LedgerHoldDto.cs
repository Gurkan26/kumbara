namespace Kumbara.Ledger.Application.DTOs;

public record LedgerHoldDto(
    Guid Id,
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);
