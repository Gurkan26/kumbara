namespace Kumbara.Wallet.Application.Commands;

using MediatR;
using Kumbara.Wallet.Application.Interfaces;

public record UpdateWalletLimitsCommand(
    Guid WalletId,
    decimal DailyLimit,
    decimal MonthlyLimit
) : IRequest<bool>;

public class UpdateWalletLimitsCommandHandler : IRequestHandler<UpdateWalletLimitsCommand, bool>
{
    private readonly IWalletRepository _repository;

    public UpdateWalletLimitsCommandHandler(IWalletRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateWalletLimitsCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _repository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
            throw new KeyNotFoundException($"Wallet with ID '{request.WalletId}' was not found.");

        wallet.UpdateLimits(request.DailyLimit, request.MonthlyLimit);

        await _repository.UpdateAsync(wallet, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
