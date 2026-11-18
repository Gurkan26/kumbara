namespace Kumbara.Wallet.Application.Queries;

using MediatR;
using Kumbara.Wallet.Application.Common;
using Kumbara.Wallet.Application.DTOs;
using Kumbara.Wallet.Application.Interfaces;

public record GetWalletsByUserIdQuery(Guid UserId) : IRequest<IEnumerable<WalletDto>>;

public class GetWalletsByUserIdQueryHandler : IRequestHandler<GetWalletsByUserIdQuery, IEnumerable<WalletDto>>
{
    private readonly IWalletRepository _repository;

    public GetWalletsByUserIdQueryHandler(IWalletRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<WalletDto>> Handle(GetWalletsByUserIdQuery request, CancellationToken cancellationToken)
    {
        var wallets = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        return wallets.Select(w => w.ToDto()).ToList();
    }
}
