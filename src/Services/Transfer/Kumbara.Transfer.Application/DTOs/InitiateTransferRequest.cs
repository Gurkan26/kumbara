namespace Kumbara.Transfer.Application.DTOs;

public record InitiateTransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency);
