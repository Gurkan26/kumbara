namespace Kumbara.Wallet.Application.Common;

using Kumbara.Wallet.Application.DTOs;
using Kumbara.Wallet.Domain.Entities;

public static class WalletMappingExtensions
{
    public static WalletDto ToDto(this Wallet wallet)
    {
        return new WalletDto(
            wallet.Id,
            wallet.UserId,
            wallet.Currency,
            wallet.Status,
            wallet.DailyLimit,
            wallet.MonthlyLimit,
            wallet.CreatedAt,
            wallet.UpdatedAt
        );
    }
}
