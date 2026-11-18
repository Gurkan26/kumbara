namespace Kumbara.Risk.Application.Commands;

using MediatR;
using Kumbara.Risk.Domain.Models;

public record EvaluateRiskCommand(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency) : IRequest<RiskCheckResult>;
