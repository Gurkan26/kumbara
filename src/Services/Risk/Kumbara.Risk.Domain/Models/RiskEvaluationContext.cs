namespace Kumbara.Risk.Domain.Models;

public record RiskEvaluationContext(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency);
