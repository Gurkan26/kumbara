namespace Kumbara.Risk.Domain.Interfaces;

public interface IVelocityService
{
    /// <summary>
    /// Increments and checks whether the cumulative transfer amount for the given account within the last 24 hours
    /// remains within the daily limit.
    /// </summary>
    /// <returns>True if within the daily limit (approved), false if exceeded (rejected).</returns>
    Task<bool> IncrementAndCheckDailyLimitAsync(
        Guid accountId,
        decimal amount,
        decimal dailyLimit = 100000m,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments and checks whether the number of transactions for the given account within the velocity window
    /// (e.g. 5 minutes) remains within the allowed frequency limit.
    /// </summary>
    /// <returns>True if within allowed frequency (approved), false if exceeded (rejected).</returns>
    Task<bool> IncrementAndCheckFrequencyAsync(
        Guid accountId,
        int maxAllowedCount = 5,
        TimeSpan? window = null,
        CancellationToken cancellationToken = default);
}
