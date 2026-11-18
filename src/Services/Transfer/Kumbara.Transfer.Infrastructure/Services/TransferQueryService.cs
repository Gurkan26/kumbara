using Microsoft.EntityFrameworkCore;
using Kumbara.Transfer.Application.DTOs;
using Kumbara.Transfer.Application.Services;
using Kumbara.Transfer.Infrastructure.Persistence;

namespace Kumbara.Transfer.Infrastructure.Services;

public class TransferQueryService : ITransferQueryService
{
    private readonly TransferDbContext _dbContext;

    public TransferQueryService(TransferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TransferDto?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.TransferStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CorrelationId == id, cancellationToken);

        if (state == null)
        {
            return null;
        }

        return new TransferDto(
            state.CorrelationId,
            state.CurrentState,
            state.SourceAccountId,
            state.DestinationAccountId,
            state.Amount,
            state.Currency,
            state.FailureReason,
            state.CreatedAt,
            state.CompletedAt
        );
    }
}
