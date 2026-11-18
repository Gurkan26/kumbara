namespace Kumbara.Risk.Domain.Rules;

using Kumbara.Risk.Domain.Models;

public interface IRiskRule
{
    Task<RiskCheckResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default);
}
