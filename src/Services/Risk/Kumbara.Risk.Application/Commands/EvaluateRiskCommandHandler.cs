namespace Kumbara.Risk.Application.Commands;

using MediatR;
using Microsoft.Extensions.Logging;
using Kumbara.Risk.Domain.Models;
using Kumbara.Risk.Domain.Rules;

public class EvaluateRiskCommandHandler : IRequestHandler<EvaluateRiskCommand, RiskCheckResult>
{
    private readonly IEnumerable<IRiskRule> _rules;
    private readonly ILogger<EvaluateRiskCommandHandler> _logger;

    public EvaluateRiskCommandHandler(
        IEnumerable<IRiskRule> rules,
        ILogger<EvaluateRiskCommandHandler> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<RiskCheckResult> Handle(EvaluateRiskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Evaluating risk for transfer {TransferId}: Source={Source}, Dest={Dest}, Amount={Amount} {Currency}",
            request.TransferId, request.SourceAccountId, request.DestinationAccountId, request.Amount, request.Currency);

        var context = new RiskEvaluationContext(
            request.TransferId,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Currency);

        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(context, cancellationToken);
            if (!result.IsApproved)
            {
                _logger.LogWarning(
                    "Transfer {TransferId} failed risk evaluation on rule {Rule}: {Reason}",
                    request.TransferId, rule.GetType().Name, result.RejectionReason);

                return result;
            }
        }

        _logger.LogInformation("Transfer {TransferId} passed all risk checks successfully.", request.TransferId);
        return RiskCheckResult.Approved();
    }
}
