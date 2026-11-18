namespace Kumbara.Wallet.Application.Commands;

using MediatR;
using Kumbara.Wallet.Application.Interfaces;

public record FreezeWalletCommand(
    Guid WalletId,
    string Reason
) : IRequest<bool>;

public class FreezeWalletCommandHandler : IRequestHandler<FreezeWalletCommand, bool>
{
    private readonly IWalletRepository _repository;

    public FreezeWalletCommandHandler(IWalletRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(FreezeWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _repository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
            throw new KeyNotFoundException($"Wallet with ID '{request.WalletId}' was not found.");

        wallet.Freeze(request.Reason);

        await _repository.UpdateAsync(wallet, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
