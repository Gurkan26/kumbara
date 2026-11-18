namespace Kumbara.Wallet.Application.DTOs;

using Kumbara.Wallet.Domain.Enums;

public record WalletDto(
    Guid Id,
    Guid UserId,
    string Currency,
    WalletStatus Status,
    decimal DailyLimit,
    decimal MonthlyLimit,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateWalletRequest(
    Guid UserId,
    string Currency,
    decimal? DailyLimit = null,
    decimal? MonthlyLimit = null
);

public record FreezeWalletRequest(
    string Reason
);

public record UpdateLimitsRequest(
    decimal DailyLimit,
    decimal MonthlyLimit
);
