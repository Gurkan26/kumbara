namespace Kumbara.Wallet.Application.Commands;

using MediatR;
using Kumbara.Wallet.Application.Interfaces;
using Kumbara.Wallet.Domain.Entities;

public record CreateWalletCommand(
    Guid UserId,
    string Currency,
    decimal? DailyLimit = null,
    decimal? MonthlyLimit = null
) : IRequest<Guid>;

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, Guid>
{
    private readonly IWalletRepository _repository;

    public CreateWalletCommandHandler(IWalletRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
    {
        var dailyLimit = request.DailyLimit ?? 50000m;
        var monthlyLimit = request.MonthlyLimit ?? 200000m;

        var wallet = Wallet.Create(request.UserId, request.Currency, dailyLimit, monthlyLimit);

        await _repository.AddAsync(wallet, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return wallet.Id;
    }
}
