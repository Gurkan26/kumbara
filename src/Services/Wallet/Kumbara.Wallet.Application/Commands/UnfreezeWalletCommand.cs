namespace Kumbara.Wallet.Application.Commands;

using MediatR;
using Kumbara.Wallet.Application.Interfaces;

public record UnfreezeWalletCommand(
    Guid WalletId
) : IRequest<bool>;

public class UnfreezeWalletCommandHandler : IRequestHandler<UnfreezeWalletCommand, bool>
{
    private readonly IWalletRepository _repository;

    public UnfreezeWalletCommandHandler(IWalletRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UnfreezeWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _repository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
            throw new KeyNotFoundException($"Wallet with ID '{request.WalletId}' was not found.");

        wallet.Unfreeze();

        await _repository.UpdateAsync(wallet, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
