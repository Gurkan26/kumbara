using System.Text.Json;
using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Exceptions;

namespace Kumbara.Ledger.Application.Commands;

public record HoldFundsCommand(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string Currency
) : IRequest<Result<Guid>>;

public class HoldFundsCommandHandler : IRequestHandler<HoldFundsCommand, Result<Guid>>
{
    private readonly ILedgerRepository _repository;

    public HoldFundsCommandHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(HoldFundsCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Result<Guid>.Failure("Hold amount must be greater than zero.");
        }

        Result<Guid> result = Result<Guid>.Failure("Hold failed");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var balance = await _repository.GetBalanceAsync(request.AccountId, cancellationToken);
            if (balance == null)
            {
                throw new InsufficientFundsException(request.AccountId, request.Amount, 0m);
            }

            if (!string.Equals(balance.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new CurrencyMismatchException(balance.Currency, request.Currency);
            }

            // Holds funds: AvailableBalance decreases, BlockedBalance increases
            balance.Hold(request.Amount);

            var hold = new LedgerHold(request.TransactionId, request.AccountId, request.Amount, request.Currency);
            await _repository.AddHoldAsync(hold, cancellationToken);

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "FundsHeldIntegrationEvent",
                Payload = JsonSerializer.Serialize(new
                {
                    HoldId = hold.Id,
                    request.TransactionId,
                    request.AccountId,
                    request.Amount,
                    Currency = request.Currency.ToUpperInvariant(),
                    OccurredAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            result = Result<Guid>.Success(hold.Id);
        }, cancellationToken);

        return result;
    }
}
