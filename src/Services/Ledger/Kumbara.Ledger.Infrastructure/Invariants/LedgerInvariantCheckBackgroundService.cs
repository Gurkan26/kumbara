using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kumbara.Ledger.Application.Invariants;

namespace Kumbara.Ledger.Infrastructure.Invariants;

public class LedgerInvariantCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LedgerInvariantCheckBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public LedgerInvariantCheckBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LedgerInvariantCheckBackgroundService> logger,
        TimeSpan? checkInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ledger Invariant Check Background Service started with check interval: {Interval}.", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunInvariantChecksAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unexpected error occurred during periodic ledger invariant check.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Ledger Invariant Check Background Service stopped.");
    }

    public async Task<bool> RunInvariantChecksAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var checker = scope.ServiceProvider.GetService<ILedgerInvariantChecker>();

        if (checker == null)
        {
            _logger.LogWarning("ILedgerInvariantChecker is not registered in service provider. Skipping check.");
            return false;
        }

        var zeroSumResult = await checker.CheckDoubleEntryZeroSumAsync(cancellationToken);
        var noNegResult = await checker.CheckNoNegativeBalancesAsync(cancellationToken);

        bool allPassed = zeroSumResult.IsValid && noNegResult.IsValid;

        if (allPassed)
        {
            _logger.LogInformation("Periodic ledger invariant checks PASSED successfully.");
        }
        else
        {
            _logger.LogCritical("CRITICAL: One or more ledger invariants FAILED! DoubleEntryZeroSum: {ZeroSum}, NoNegativeBalances: {NoNegative}",
                zeroSumResult.IsValid ? "PASS" : "FAIL",
                noNegResult.IsValid ? "PASS" : "FAIL");
        }

        return allPassed;
    }
}
