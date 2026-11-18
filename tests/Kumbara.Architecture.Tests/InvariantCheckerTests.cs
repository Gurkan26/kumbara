using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Kumbara.Ledger.Application.Invariants;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Enums;
using Kumbara.Ledger.Infrastructure.Invariants;
using Kumbara.Ledger.Infrastructure.Persistence;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class InvariantCheckerTests
{
    private static LedgerDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new LedgerDbContext(options);
    }

    [Fact]
    public async Task DoubleEntryZeroSum_ShouldPass_WhenLedgerIsBalanced()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        var tx1 = Guid.NewGuid();
        var tx2 = Guid.NewGuid();
        var clearingAccount = Guid.NewGuid();
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        // Transaction 1: Deposit of 1000 TRY
        context.LedgerEntries.AddRange(
            LedgerEntry.CreateDebit(tx1, clearingAccount, 1000m, "TRY", "Clearing debit"),
            LedgerEntry.CreateCredit(tx1, customer1, 1000m, "TRY", "Customer deposit")
        );

        // Transaction 2: Transfer of 350.50 TRY
        context.LedgerEntries.AddRange(
            LedgerEntry.CreateDebit(tx2, customer1, 350.50m, "TRY", "Transfer leg debit"),
            LedgerEntry.CreateCredit(tx2, customer2, 350.50m, "TRY", "Transfer leg credit")
        );

        await context.SaveChangesAsync();

        var result = await checker.CheckDoubleEntryZeroSumAsync();

        Assert.True(result.IsValid);
        Assert.Contains("strictly zero", result.Message);
    }

    [Fact]
    public async Task DoubleEntryZeroSum_ShouldFail_WhenDebitCreditSumDoesNotEqualZero()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        var txId = Guid.NewGuid();
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();

        // Unbalanced ledger entries (phantom money creation)
        context.LedgerEntries.AddRange(
            LedgerEntry.CreateDebit(txId, accountA, 500m, "TRY", "Debit leg"),
            LedgerEntry.CreateCredit(txId, accountB, 750m, "TRY", "Asymmetric credit leg")
        );

        await context.SaveChangesAsync();

        var result = await checker.CheckDoubleEntryZeroSumAsync();

        Assert.False(result.IsValid);
        Assert.Contains("Double-entry invariant VIOLATION", result.Message);
        Assert.Contains("Discrepancy: 250,0000", result.Message);
    }

    [Fact]
    public async Task NoNegativeBalances_ShouldPass_WhenAllBalancesAreNonNegative()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        context.AccountBalances.AddRange(
            new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 1500m, initialBlocked: 0m),
            new AccountBalance(Guid.NewGuid(), "USD", initialAvailable: 250m, initialBlocked: 100m),
            new AccountBalance(Guid.NewGuid(), "EUR", initialAvailable: 0m, initialBlocked: 0m)
        );

        await context.SaveChangesAsync();

        var result = await checker.CheckNoNegativeBalancesAsync();

        Assert.True(result.IsValid);
        Assert.Contains("verified", result.Message);
    }

    [Fact]
    public async Task NoNegativeBalances_ShouldFail_WhenAccountHasNegativeAvailableBalance()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        var accountId = Guid.NewGuid();
        var corruptedBalance = new AccountBalance(accountId, "TRY", initialAvailable: 100m);

        // Corrupt via direct reflection/property simulation in test to test detector
        typeof(AccountBalance).GetProperty(nameof(AccountBalance.AvailableBalance))!
            .SetValue(corruptedBalance, -150.00m);

        context.AccountBalances.Add(corruptedBalance);
        await context.SaveChangesAsync();

        var result = await checker.CheckNoNegativeBalancesAsync();

        Assert.False(result.IsValid);
        Assert.Contains("Negative balance invariant VIOLATION", result.Message);
        Assert.Contains(accountId.ToString(), result.Message);
    }

    [Fact]
    public async Task NoNegativeBalances_ShouldFail_WhenAccountHasNegativeBlockedBalance()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        var accountId = Guid.NewGuid();
        var corruptedBalance = new AccountBalance(accountId, "USD", initialAvailable: 500m);

        typeof(AccountBalance).GetProperty(nameof(AccountBalance.BlockedBalance))!
            .SetValue(corruptedBalance, -25.50m);

        context.AccountBalances.Add(corruptedBalance);
        await context.SaveChangesAsync();

        var result = await checker.CheckNoNegativeBalancesAsync();

        Assert.False(result.IsValid);
        Assert.Contains("Negative balance invariant VIOLATION", result.Message);
    }

    [Fact]
    public async Task TotalSystemFundsConserved_ShouldPass_WhenTotalBalancesMatchExpected()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        context.AccountBalances.AddRange(
            new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 1000m, initialBlocked: 0m),
            new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 500m, initialBlocked: 200m),
            new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 300m, initialBlocked: 0m)
        );

        await context.SaveChangesAsync();

        // 1000 + (500 + 200) + 300 = 2000m
        var result = await checker.CheckTotalSystemFundsConservedAsync(2000m);

        Assert.True(result.IsValid);
        Assert.Contains("strictly conserved", result.Message);
    }

    [Fact]
    public async Task TotalSystemFundsConserved_ShouldFail_WhenTotalBalancesMismatchExpected()
    {
        using var context = CreateInMemoryDbContext();
        var checker = new LedgerInvariantChecker(context);

        context.AccountBalances.AddRange(
            new AccountBalance(Guid.NewGuid(), "TRY", initialAvailable: 500m, initialBlocked: 0m)
        );

        await context.SaveChangesAsync();

        // Expected 1000m but actual is 500m
        var result = await checker.CheckTotalSystemFundsConservedAsync(1000m);

        Assert.False(result.IsValid);
        Assert.Contains("Total funds conservation VIOLATION", result.Message);
        Assert.Contains("Discrepancy: 500,0000", result.Message);
    }

    [Fact]
    public async Task BackgroundService_RunInvariantChecksAsync_ExecutesSuccessfullyOnHealthyLedger()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var services = new ServiceCollection();
        services.AddScoped<ILedgerDbContext>(_ => new LedgerDbContext(options));
        services.AddScoped<ILedgerInvariantChecker, LedgerInvariantChecker>();
        var serviceProvider = services.BuildServiceProvider();

        // Populate valid balanced ledger
        using (var scope = serviceProvider.CreateScope())
        {
            var db = (LedgerDbContext)scope.ServiceProvider.GetRequiredService<ILedgerDbContext>();
            var txId = Guid.NewGuid();
            var a1 = Guid.NewGuid();
            var a2 = Guid.NewGuid();

            db.AccountBalances.AddRange(
                new AccountBalance(a1, "TRY", 1000m),
                new AccountBalance(a2, "TRY", 500m)
            );

            db.LedgerEntries.AddRange(
                LedgerEntry.CreateDebit(txId, a1, 200m, "TRY", "Debit"),
                LedgerEntry.CreateCredit(txId, a2, 200m, "TRY", "Credit")
            );

            await db.SaveChangesAsync();
        }

        var backgroundService = new LedgerInvariantCheckBackgroundService(
            serviceProvider,
            NullLogger<LedgerInvariantCheckBackgroundService>.Instance,
            TimeSpan.FromSeconds(1));

        bool allPassed = await backgroundService.RunInvariantChecksAsync();

        Assert.True(allPassed);
    }
}
