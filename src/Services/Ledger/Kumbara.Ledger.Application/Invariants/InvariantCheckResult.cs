namespace Kumbara.Ledger.Application.Invariants;

public class InvariantCheckResult
{
    public bool IsValid { get; }
    public string InvariantName { get; }
    public string Message { get; }
    public DateTime CheckedAt { get; }
    public object? Metadata { get; }

    public InvariantCheckResult(bool isValid, string invariantName, string message, object? metadata = null)
    {
        IsValid = isValid;
        InvariantName = invariantName;
        Message = message;
        CheckedAt = DateTime.UtcNow;
        Metadata = metadata;
    }

    public static InvariantCheckResult Success(string invariantName, string message = "Invariant verified successfully.", object? metadata = null)
        => new(true, invariantName, message, metadata);

    public static InvariantCheckResult Failure(string invariantName, string message, object? metadata = null)
        => new(false, invariantName, message, metadata);
}
