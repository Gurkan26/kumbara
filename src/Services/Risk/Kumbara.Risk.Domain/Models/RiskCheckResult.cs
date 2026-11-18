namespace Kumbara.Risk.Domain.Models;

public record RiskCheckResult(bool IsApproved, string? RejectionReason = null)
{
    public static RiskCheckResult Approved() => new(true, null);
    public static RiskCheckResult Rejected(string reason) => new(false, reason);
}
