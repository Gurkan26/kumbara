using Kumbara.Ledger.Domain.Exceptions;
using Kumbara.Ledger.Domain.ValueObjects;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class MoneyTests
{
    [Fact]
    public void Addition_WithSameCurrency_WorksAccurately()
    {
        var m1 = new Money(1234.5678m, "TRY");
        var m2 = new Money(8765.4321m, "TRY");

        var sum = m1.Add(m2);

        Assert.Equal(9999.9999m, sum.Amount);
        Assert.Equal("TRY", sum.Currency);

        // Operator +
        var sumOp = m1 + m2;
        Assert.Equal(9999.9999m, sumOp.Amount);
    }

    [Fact]
    public void Subtraction_WithSameCurrency_WorksAccurately()
    {
        var m1 = new Money(500.7500m, "USD");
        var m2 = new Money(200.2500m, "USD");

        var diff = m1.Subtract(m2);

        Assert.Equal(300.5000m, diff.Amount);
        Assert.Equal("USD", diff.Currency);

        // Operator -
        var diffOp = m1 - m2;
        Assert.Equal(300.5000m, diffOp.Amount);
    }

    [Fact]
    public void CurrencyMismatch_OnAddition_ThrowsCurrencyMismatchException()
    {
        var tryMoney = new Money(100.0000m, "TRY");
        var usdMoney = new Money(100.0000m, "USD");

        var ex = Assert.Throws<CurrencyMismatchException>(() => tryMoney.Add(usdMoney));
        Assert.Contains("TRY", ex.Message);
        Assert.Contains("USD", ex.Message);
    }

    [Fact]
    public void CurrencyMismatch_OnSubtraction_ThrowsCurrencyMismatchException()
    {
        var tryMoney = new Money(100.0000m, "TRY");
        var eurMoney = new Money(50.0000m, "EUR");

        var ex = Assert.Throws<CurrencyMismatchException>(() => tryMoney.Subtract(eurMoney));
        Assert.Contains("TRY", ex.Message);
        Assert.Contains("EUR", ex.Message);
    }

    [Fact]
    public void PrecisionCheck_DecimalArithmetic_NoFloatingPointDrift()
    {
        // Classic binary floating point drift: 0.1 + 0.2 != 0.3 in double/float
        var m1 = new Money(0.1000m, "TRY");
        var m2 = new Money(0.2000m, "TRY");

        var result = m1 + m2;

        Assert.Equal(0.3000m, result.Amount);

        // Rounding to 4 decimal places using MidpointRounding.AwayFromZero
        var precise = new Money(10.123456m, "EUR");
        Assert.Equal(10.1235m, precise.Amount);

        // Multiple repeated fractional additions
        var total = Money.Zero("USD");
        var increment = new Money(0.0001m, "USD");
        for (int i = 0; i < 10000; i++)
        {
            total += increment;
        }

        Assert.Equal(1.0000m, total.Amount);
    }

    [Fact]
    public void NegativeAmount_OnCreation_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1.00m, "TRY"));
        Assert.Equal("amount", ex.ParamName);
    }

    [Fact]
    public void NegativeAmount_OnSubtraction_ThrowsInvalidOperationException()
    {
        var m1 = new Money(50m, "TRY");
        var m2 = new Money(100m, "TRY");

        Assert.Throws<InvalidOperationException>(() => m1.Subtract(m2));
    }
}
