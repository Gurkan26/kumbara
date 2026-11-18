namespace Kumbara.Transfer.Application.DTOs;

public record TransferDto(
    Guid TransferId,
    string State,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? CompletedAt);
