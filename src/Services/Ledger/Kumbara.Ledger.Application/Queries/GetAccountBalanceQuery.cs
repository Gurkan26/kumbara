using MediatR;
using Kumbara.Ledger.Application.DTOs;
using Kumbara.Ledger.Application.Interfaces;

namespace Kumbara.Ledger.Application.Queries;

public record GetAccountBalanceQuery(Guid AccountId) : IRequest<AccountBalanceDto?>;

public class GetAccountBalanceQueryHandler : IRequestHandler<GetAccountBalanceQuery, AccountBalanceDto?>
{
    private readonly ILedgerRepository _repository;

    public GetAccountBalanceQueryHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccountBalanceDto?> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        var balance = await _repository.GetBalanceAsync(request.AccountId, cancellationToken);
        if (balance == null)
        {
            return null;
        }

        return new AccountBalanceDto(
            balance.AccountId,
            balance.Currency,
            balance.AvailableBalance,
            balance.BlockedBalance,
            balance.TotalBalance
        );
    }
}
