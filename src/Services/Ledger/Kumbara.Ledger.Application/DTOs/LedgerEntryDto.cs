namespace Kumbara.Ledger.Application.DTOs;

public record LedgerEntryDto(
    Guid Id,
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string Type,
    string Currency,
    string Description,
    DateTime CreatedAt
);
