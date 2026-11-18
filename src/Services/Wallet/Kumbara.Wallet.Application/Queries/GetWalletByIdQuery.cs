namespace Kumbara.Wallet.Application.Queries;

using MediatR;
using Kumbara.Wallet.Application.Common;
using Kumbara.Wallet.Application.DTOs;
using Kumbara.Wallet.Application.Interfaces;

public record GetWalletByIdQuery(Guid Id) : IRequest<WalletDto?>;

public class GetWalletByIdQueryHandler : IRequestHandler<GetWalletByIdQuery, WalletDto?>
{
    private readonly IWalletRepository _repository;

    public GetWalletByIdQueryHandler(IWalletRepository repository)
    {
        _repository = repository;
    }

    public async Task<WalletDto?> Handle(GetWalletByIdQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return wallet?.ToDto();
    }
}
