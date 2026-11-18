using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Enums;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class DoubleEntryInvariantTests
{
    [Fact]
    public void EveryTransfer_GeneratesOneDebitAndOneCredit()
    {
        var txId = Guid.NewGuid();
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        const decimal amount = 250.75m;
        const string currency = "TRY";

        var debitEntry = LedgerEntry.CreateDebit(
            txId,
            fromAccountId,
            amount,
            currency,
            $"Transfer to {toAccountId}");

        var creditEntry = LedgerEntry.CreateCredit(
            txId,
            toAccountId,
            amount,
            currency,
            $"Transfer from {fromAccountId}");

        var entries = new List<LedgerEntry> { debitEntry, creditEntry };

        // Exactly 2 entries
        Assert.Equal(2, entries.Count);

        // Exactly 1 Debit and 1 Credit
        Assert.Single(entries, e => e.Type == EntryType.Debit);
        Assert.Single(entries, e => e.Type == EntryType.Credit);

        // Both share same TransactionId
        Assert.All(entries, e => Assert.Equal(txId, e.TransactionId));

        // Both share same Currency and positive Amount
        Assert.All(entries, e =>
        {
            Assert.Equal(currency, e.Currency);
            Assert.Equal(amount, e.Amount);
        });
    }

    [Fact]
    public void SumOfDebitsAndCredits_AcrossTransfer_IsStrictlyZero()
    {
        var txId = Guid.NewGuid();
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        const decimal transferAmount = 1450.5555m;
        const string currency = "USD";

        var debit = LedgerEntry.CreateDebit(txId, fromAccountId, transferAmount, currency, "Debit leg");
        var credit = LedgerEntry.CreateCredit(txId, toAccountId, transferAmount, currency, "Credit leg");

        var entries = new[] { debit, credit };

        // Financial invariant: Signed sum of Debits (-Amount) and Credits (+Amount) == 0
        decimal signedSum = entries.Sum(e => e.Type switch
        {
            EntryType.Debit => -e.Amount,
            EntryType.Credit => e.Amount,
            _ => throw new InvalidOperationException("Unsupported entry type")
        });

        Assert.Equal(0.0000m, signedSum);
    }

    [Fact]
    public void MultipleTransfers_ConsistentlyPreserve_DoubleEntryZeroSum()
    {
        var testAmounts = new[] { 10.00m, 123.4567m, 999999.9999m, 0.0001m };
        var random = new Random(42);

        foreach (var amount in testAmounts)
        {
            var txId = Guid.NewGuid();
            var src = Guid.NewGuid();
            var dst = Guid.NewGuid();

            var debit = LedgerEntry.CreateDebit(txId, src, amount, "TRY", "Direct Transfer Debit");
            var credit = LedgerEntry.CreateCredit(txId, dst, amount, "TRY", "Direct Transfer Credit");

            var pair = new[] { debit, credit };

            decimal net = pair.Sum(p => p.Type == EntryType.Debit ? -p.Amount : p.Amount);
            Assert.Equal(0m, net);
        }
    }

    [Fact]
    public void AccountBalances_AfterTransfer_PreservesTotalSystemFunds()
    {
        var fromAccount = new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 1000m);
        var toAccount = new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 500m);

        decimal systemTotalBefore = fromAccount.TotalBalance + toAccount.TotalBalance;
        const decimal transferAmount = 350.25m;

        // Perform transfer operations
        fromAccount.Debit(transferAmount);
        toAccount.Credit(transferAmount);

        decimal systemTotalAfter = fromAccount.TotalBalance + toAccount.TotalBalance;

        // Balance conservation invariant
        Assert.Equal(systemTotalBefore, systemTotalAfter);
        Assert.Equal(649.75m, fromAccount.AvailableBalance);
        Assert.Equal(850.25m, toAccount.AvailableBalance);
    }
}
