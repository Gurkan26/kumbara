namespace Kumbara.Ledger.Domain.Exceptions;

public class CurrencyMismatchException : Exception
{
    public string ExpectedCurrency { get; }
    public string ActualCurrency { get; }

    public CurrencyMismatchException(string expectedCurrency, string actualCurrency)
        : base($"Currency mismatch. Expected '{expectedCurrency}', but received '{actualCurrency}'.")
    {
        ExpectedCurrency = expectedCurrency;
        ActualCurrency = actualCurrency;
    }

    public CurrencyMismatchException(string message) : base(message)
    {
        ExpectedCurrency = string.Empty;
        ActualCurrency = string.Empty;
    }
}
