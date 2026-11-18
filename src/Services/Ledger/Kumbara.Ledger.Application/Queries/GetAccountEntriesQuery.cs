using MediatR;
using Kumbara.Ledger.Application.DTOs;
using Kumbara.Ledger.Application.Interfaces;

namespace Kumbara.Ledger.Application.Queries;

public record GetAccountEntriesQuery(Guid AccountId) : IRequest<IEnumerable<LedgerEntryDto>>;

public class GetAccountEntriesQueryHandler : IRequestHandler<GetAccountEntriesQuery, IEnumerable<LedgerEntryDto>>
{
    private readonly ILedgerRepository _repository;

    public GetAccountEntriesQueryHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<LedgerEntryDto>> Handle(GetAccountEntriesQuery request, CancellationToken cancellationToken)
    {
        var entries = await _repository.GetEntriesByAccountIdAsync(request.AccountId, cancellationToken);
        return entries.Select(e => new LedgerEntryDto(
            e.Id,
            e.TransactionId,
            e.AccountId,
            e.Amount,
            e.Type.ToString(),
            e.Currency,
            e.Description,
            e.CreatedAt
        ));
    }
}
