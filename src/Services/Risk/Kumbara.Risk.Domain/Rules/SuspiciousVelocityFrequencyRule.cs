namespace Kumbara.Risk.Domain.Rules;

using Kumbara.Risk.Domain.Interfaces;
using Kumbara.Risk.Domain.Models;

public class SuspiciousVelocityFrequencyRule : IRiskRule
{
    private readonly IVelocityService _velocityService;
    private readonly int _maxTransfers;
    private readonly TimeSpan _window;

    public SuspiciousVelocityFrequencyRule(
        IVelocityService velocityService,
        int maxTransfers = 5,
        TimeSpan? window = null)
    {
        _velocityService = velocityService;
        _maxTransfers = maxTransfers;
        _window = window ?? TimeSpan.FromMinutes(5);
    }

    public async Task<RiskCheckResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var withinLimit = await _velocityService.IncrementAndCheckFrequencyAsync(
            context.SourceAccountId,
            _maxTransfers,
            _window,
            cancellationToken);

        if (!withinLimit)
        {
            return RiskCheckResult.Rejected(
                $"Suspicious transaction velocity frequency: account {context.SourceAccountId} exceeded {_maxTransfers} transfers within {_window.TotalMinutes} minutes.");
        }

        return RiskCheckResult.Approved();
    }
}
