using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Enums;
using Kumbara.Ledger.Domain.Exceptions;
using Kumbara.Ledger.Domain.ValueObjects;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class LedgerDomainTests
{
    [Fact]
    public void Money_Add_SameCurrency_Succeeds()
    {
        var m1 = new Money(100.50m, "TRY");
        var m2 = new Money(50.25m, "TRY");

        var result = m1.Add(m2);

        Assert.Equal(150.75m, result.Amount);
        Assert.Equal("TRY", result.Currency);
    }

    [Fact]
    public void Money_Add_DifferentCurrency_ThrowsCurrencyMismatchException()
    {
        var m1 = new Money(100m, "TRY");
        var m2 = new Money(50m, "USD");

        Assert.Throws<CurrencyMismatchException>(() => m1.Add(m2));
    }

    [Fact]
    public void Money_Rounding_RoundsToFourDecimals()
    {
        var m = new Money(10.123456m, "USD");
        Assert.Equal(10.1235m, m.Amount);
    }

    [Fact]
    public void AccountBalance_Debit_InsufficientFunds_ThrowsInsufficientFundsException()
    {
        var account = new AccountBalance(Guid.NewGuid(), "TRY", 100m);

        Assert.Throws<InsufficientFundsException>(() => account.Debit(150m));
        Assert.Equal(100m, account.AvailableBalance);
    }

    [Fact]
    public void AccountBalance_Credit_IncreasesAvailableBalance()
    {
        var account = new AccountBalance(Guid.NewGuid(), "TRY", 100m);
        account.Credit(50m);

        Assert.Equal(150m, account.AvailableBalance);
        Assert.Equal(150m, account.TotalBalance);
    }

    [Fact]
    public void AccountBalance_Hold_And_Commit_WorksAsExpected()
    {
        var account = new AccountBalance(Guid.NewGuid(), "TRY", 200m);

        // Hold 80
        account.Hold(80m);
        Assert.Equal(120m, account.AvailableBalance);
        Assert.Equal(80m, account.BlockedBalance);
        Assert.Equal(200m, account.TotalBalance);

        // Commit 80
        account.CommitHold(80m);
        Assert.Equal(120m, account.AvailableBalance);
        Assert.Equal(0m, account.BlockedBalance);
        Assert.Equal(120m, account.TotalBalance);
    }

    [Fact]
    public void AccountBalance_Hold_And_Release_WorksAsExpected()
    {
        var account = new AccountBalance(Guid.NewGuid(), "TRY", 200m);

        // Hold 50
        account.Hold(50m);
        Assert.Equal(150m, account.AvailableBalance);
        Assert.Equal(50m, account.BlockedBalance);

        // Release 50
        account.ReleaseHold(50m);
        Assert.Equal(200m, account.AvailableBalance);
        Assert.Equal(0m, account.BlockedBalance);
    }

    [Fact]
    public void LedgerEntry_CreateDebitAndCredit_SetsPropertiesCorrectly()
    {
        var txId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var debit = LedgerEntry.CreateDebit(txId, accountId, 100m, "TRY", "Test debit");
        var credit = LedgerEntry.CreateCredit(txId, accountId, 100m, "TRY", "Test credit");

        Assert.Equal(EntryType.Debit, debit.Type);
        Assert.Equal(100m, debit.Amount);
        Assert.Equal(EntryType.Credit, credit.Type);
        Assert.Equal(100m, credit.Amount);
    }
}
