using WalletAggregate = Kumbara.Wallet.Domain.Entities.Wallet;
using Kumbara.Wallet.Domain.Enums;
using Kumbara.Wallet.Domain.Events;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class WalletStateMachineTests
{
    private static WalletAggregate CreateDefaultWallet()
    {
        return WalletAggregate.Create(
            userId: Guid.NewGuid(),
            currency: "TRY",
            dailyLimit: 25000m,
            monthlyLimit: 100000m);
    }

    [Fact]
    public void NewWallet_StartsActive()
    {
        var wallet = CreateDefaultWallet();

        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Single(wallet.DomainEvents, e => e is WalletCreatedDomainEvent);
    }

    [Fact]
    public void Freeze_ChangesStatusToFrozen()
    {
        var wallet = CreateDefaultWallet();

        wallet.Freeze("Suspicious activity detected");

        Assert.Equal(WalletStatus.Frozen, wallet.Status);
        Assert.Contains(wallet.DomainEvents, e => e is WalletFrozenDomainEvent);
    }

    [Fact]
    public void CannotFreeze_AlreadyFrozenWallet()
    {
        var wallet = CreateDefaultWallet();
        wallet.Freeze("First freeze");

        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Freeze("Second freeze"));
        Assert.Equal("Wallet is already frozen.", ex.Message);
    }

    [Fact]
    public void CannotFreeze_ClosedWallet()
    {
        var wallet = CreateDefaultWallet();
        wallet.Close();

        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Freeze("Freeze attempt"));
        Assert.Equal("Cannot freeze a closed wallet.", ex.Message);
    }

    [Fact]
    public void Unfreeze_RevertsStatusToActive()
    {
        var wallet = CreateDefaultWallet();
        wallet.Freeze("Compliance review");

        wallet.Unfreeze();

        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Contains(wallet.DomainEvents, e => e is WalletUnfrozenDomainEvent);
    }

    [Fact]
    public void CannotUnfreeze_NonFrozenWallet()
    {
        var wallet = CreateDefaultWallet(); // Currently Active

        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Unfreeze());
        Assert.Equal("Only frozen wallets can be unfrozen.", ex.Message);
    }

    [Fact]
    public void CannotUnfreeze_SuspendedWallet()
    {
        var wallet = CreateDefaultWallet();
        wallet.Suspend("KYC incomplete");

        var ex = Assert.Throws<InvalidOperationException>(() => wallet.Unfreeze());
        Assert.Equal("Only frozen wallets can be unfrozen.", ex.Message);
    }

    [Fact]
    public void ClosedWallet_IsTerminal_NoFurtherTransitionsPermitted()
    {
        var wallet = CreateDefaultWallet();
        wallet.Close();

        Assert.Equal(WalletStatus.Closed, wallet.Status);

        // Cannot close again
        Assert.Throws<InvalidOperationException>(() => wallet.Close());

        // Cannot freeze
        Assert.Throws<InvalidOperationException>(() => wallet.Freeze("Attempt freeze on closed"));

        // Cannot unfreeze
        Assert.Throws<InvalidOperationException>(() => wallet.Unfreeze());

        // Cannot suspend
        Assert.Throws<InvalidOperationException>(() => wallet.Suspend("Attempt suspend on closed"));

        // Cannot update limits
        Assert.Throws<InvalidOperationException>(() => wallet.UpdateLimits(1000m, 5000m));
    }
}
