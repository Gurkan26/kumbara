using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Kumbara.Ledger.Application.Invariants;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Enums;

namespace Kumbara.Ledger.Infrastructure.Invariants;

public class LedgerInvariantChecker : ILedgerInvariantChecker
{
    private readonly ILedgerDbContext _dbContext;
    private readonly ILogger<LedgerInvariantChecker> _logger;

    public LedgerInvariantChecker(ILedgerDbContext dbContext, ILogger<LedgerInvariantChecker>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? NullLogger<LedgerInvariantChecker>.Instance;
    }

    public async Task<InvariantCheckResult> CheckDoubleEntryZeroSumAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying Double-Entry Zero-Sum invariant across ledger entries...");

        // Aggregate total debits and credits
        var debitSum = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Type == EntryType.Debit)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var creditSum = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Type == EntryType.Credit)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var discrepancy = Math.Abs(debitSum - creditSum);

        if (discrepancy > 0.0000m)
        {
            var msg = $"Double-entry invariant VIOLATION! Total Debits: {debitSum:F4}, Total Credits: {creditSum:F4}, Discrepancy: {discrepancy:F4}.";
            _logger.LogError(msg);
            return InvariantCheckResult.Failure(
                "DoubleEntryZeroSum",
                msg,
                new { DebitSum = debitSum, CreditSum = creditSum, Discrepancy = discrepancy });
        }

        var successMsg = $"Double-entry zero-sum invariant verified: Total Debits ({debitSum:F4}) == Total Credits ({creditSum:F4}). Net difference is strictly zero.";
        _logger.LogInformation(successMsg);
        return InvariantCheckResult.Success(
            "DoubleEntryZeroSum",
            successMsg,
            new { DebitSum = debitSum, CreditSum = creditSum });
    }

    public async Task<InvariantCheckResult> CheckNoNegativeBalancesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying No-Negative-Balances invariant across all accounts...");

        var negativeAccounts = await _dbContext.AccountBalances
            .AsNoTracking()
            .Where(b => b.AvailableBalance < 0m || b.BlockedBalance < 0m)
            .Select(b => new
            {
                b.AccountId,
                b.Currency,
                b.AvailableBalance,
                b.BlockedBalance
            })
            .ToListAsync(cancellationToken);

        if (negativeAccounts.Count > 0)
        {
            var msg = $"Negative balance invariant VIOLATION! Detected {negativeAccounts.Count} account(s) with negative balance: " +
                      string.Join("; ", negativeAccounts.Select(a => $"[Account: {a.AccountId}, Avail: {a.AvailableBalance:F4}, Blocked: {a.BlockedBalance:F4} {a.Currency}]"));
            _logger.LogError(msg);
            return InvariantCheckResult.Failure(
                "NoNegativeBalances",
                msg,
                negativeAccounts);
        }

        var successMsg = "No negative balances invariant verified: All accounts maintain non-negative available and blocked balances.";
        _logger.LogInformation(successMsg);
        return InvariantCheckResult.Success("NoNegativeBalances", successMsg);
    }

    public async Task<InvariantCheckResult> CheckTotalSystemFundsConservedAsync(decimal expectedTotalFunds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying Total-System-Funds-Conserved invariant. Expected total: {ExpectedTotal:F4}...", expectedTotalFunds);

        var totalAvailable = await _dbContext.AccountBalances
            .AsNoTracking()
            .SumAsync(b => (decimal?)b.AvailableBalance, cancellationToken) ?? 0m;

        var totalBlocked = await _dbContext.AccountBalances
            .AsNoTracking()
            .SumAsync(b => (decimal?)b.BlockedBalance, cancellationToken) ?? 0m;

        var actualTotal = totalAvailable + totalBlocked;
        var discrepancy = Math.Abs(expectedTotalFunds - actualTotal);

        if (discrepancy > 0.0000m)
        {
            var msg = $"Total funds conservation VIOLATION! Expected total: {expectedTotalFunds:F4}, Actual total: {actualTotal:F4} (Available: {totalAvailable:F4}, Blocked: {totalBlocked:F4}), Discrepancy: {discrepancy:F4}.";
            _logger.LogError(msg);
            return InvariantCheckResult.Failure(
                "TotalSystemFundsConserved",
                msg,
                new { Expected = expectedTotalFunds, Actual = actualTotal, Discrepancy = discrepancy });
        }

        var successMsg = $"Total funds conservation invariant verified: Expected ({expectedTotalFunds:F4}) == Actual ({actualTotal:F4}). System total funds are strictly conserved.";
        _logger.LogInformation(successMsg);
        return InvariantCheckResult.Success(
            "TotalSystemFundsConserved",
            successMsg,
            new { Expected = expectedTotalFunds, Actual = actualTotal });
    }
}
