namespace Kumbara.Risk.Application.DTOs;

public record EvaluateRiskRequest(
    Guid? TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency);

public record EvaluateRiskResponse(
    Guid TransferId,
    bool IsApproved,
    string? RejectionReason);
