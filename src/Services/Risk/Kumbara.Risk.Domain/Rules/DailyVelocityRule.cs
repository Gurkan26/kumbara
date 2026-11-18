namespace Kumbara.Risk.Domain.Rules;

using Kumbara.Risk.Domain.Interfaces;
using Kumbara.Risk.Domain.Models;

public class DailyVelocityRule : IRiskRule
{
    private readonly IVelocityService _velocityService;
    private readonly decimal _dailyLimit;

    public DailyVelocityRule(IVelocityService velocityService, decimal dailyLimit = 100000m)
    {
        _velocityService = velocityService;
        _dailyLimit = dailyLimit;
    }

    public async Task<RiskCheckResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var withinLimit = await _velocityService.IncrementAndCheckDailyLimitAsync(
            context.SourceAccountId,
            context.Amount,
            _dailyLimit,
            cancellationToken);

        if (!withinLimit)
        {
            return RiskCheckResult.Rejected(
                $"Daily velocity limit of {_dailyLimit} {context.Currency} exceeded in the last 24 hours for account {context.SourceAccountId}.");
        }

        return RiskCheckResult.Approved();
    }
}
