using System.ComponentModel.DataAnnotations;

namespace Kumbara.Ledger.Api.Models;

public record TransferRequest(
    Guid? TransactionId,
    [Required] Guid FromAccountId,
    [Required] Guid ToAccountId,
    [Required][Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")] decimal Amount,
    [Required][StringLength(3, MinimumLength = 3)] string Currency,
    string? Description
);

public record DepositRequest(
    [Required] Guid AccountId,
    [Required][Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")] decimal Amount,
    [Required][StringLength(3, MinimumLength = 3)] string Currency,
    Guid? TransactionId,
    string? Description
);

public record HoldRequest(
    [Required] Guid TransactionId,
    [Required] Guid AccountId,
    [Required][Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")] decimal Amount,
    [Required][StringLength(3, MinimumLength = 3)] string Currency
);

public record CommitHoldRequest(
    [Required] Guid TransactionId,
    [Required] Guid DestinationAccountId
);

public record ReleaseHoldRequest(
    [Required] Guid TransactionId
);
