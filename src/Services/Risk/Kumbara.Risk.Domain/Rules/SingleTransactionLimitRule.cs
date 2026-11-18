namespace Kumbara.Risk.Domain.Rules;

using Kumbara.Risk.Domain.Models;

public class SingleTransactionLimitRule : IRiskRule
{
    private readonly decimal _maxSingleLimit;

    public SingleTransactionLimitRule(decimal maxSingleLimit = 50000m)
    {
        _maxSingleLimit = maxSingleLimit;
    }

    public Task<RiskCheckResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (context.Amount <= 0)
        {
            return Task.FromResult(RiskCheckResult.Rejected("Transaction amount must be strictly greater than zero."));
        }

        if (context.Amount > _maxSingleLimit)
        {
            return Task.FromResult(RiskCheckResult.Rejected(
                $"Transaction amount of {context.Amount} {context.Currency} exceeds the single transaction limit of {_maxSingleLimit} {context.Currency}."));
        }

        return Task.FromResult(RiskCheckResult.Approved());
    }
}
