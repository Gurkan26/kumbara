using System.Text.Json;
using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;

namespace Kumbara.Ledger.Application.Commands;

public record ReleaseHoldCommand(
    Guid TransactionId
) : IRequest<Result<Guid>>;

public class ReleaseHoldCommandHandler : IRequestHandler<ReleaseHoldCommand, Result<Guid>>
{
    private readonly ILedgerRepository _repository;

    public ReleaseHoldCommandHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(ReleaseHoldCommand request, CancellationToken cancellationToken)
    {
        Result<Guid> result = Result<Guid>.Failure("Release hold failed");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var hold = await _repository.GetHoldByTransactionIdAsync(request.TransactionId, cancellationToken);
            if (hold == null)
            {
                result = Result<Guid>.Failure($"Active hold not found for transaction {request.TransactionId}.");
                return;
            }

            var balance = await _repository.GetBalanceAsync(hold.AccountId, cancellationToken);
            if (balance == null)
            {
                result = Result<Guid>.Failure($"Account balance for account {hold.AccountId} not found.");
                return;
            }

            // Release hold on entity and restore balance
            hold.Release();
            balance.ReleaseHold(hold.Amount);

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "HoldReleasedIntegrationEvent",
                Payload = JsonSerializer.Serialize(new
                {
                    HoldId = hold.Id,
                    request.TransactionId,
                    hold.AccountId,
                    hold.Amount,
                    Currency = hold.Currency.ToUpperInvariant(),
                    OccurredAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            result = Result<Guid>.Success(request.TransactionId);
        }, cancellationToken);

        return result;
    }
}
