namespace Kumbara.Ledger.Application.Invariants;

public interface ILedgerInvariantChecker
{
    /// <summary>
    /// Verifies that SUM(Debit) == SUM(Credit) across all ledger entries in the system.
    /// In a double-entry ledger, the net signed difference must strictly equal zero.
    /// </summary>
    Task<InvariantCheckResult> CheckDoubleEntryZeroSumAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that no account has negative AvailableBalance (< 0) or negative BlockedBalance (< 0).
    /// </summary>
    Task<InvariantCheckResult> CheckNoNegativeBalancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the total sum of balances across accounts matches the expected total funds.
    /// </summary>
    Task<InvariantCheckResult> CheckTotalSystemFundsConservedAsync(decimal expectedTotalFunds, CancellationToken cancellationToken = default);
}
