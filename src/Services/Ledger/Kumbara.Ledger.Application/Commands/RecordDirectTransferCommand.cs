using System.Text.Json;
using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Exceptions;

namespace Kumbara.Ledger.Application.Commands;

public record RecordDirectTransferCommand(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    string Description
) : IRequest<Result<Guid>>;

public class RecordDirectTransferCommandHandler : IRequestHandler<RecordDirectTransferCommand, Result<Guid>>
{
    private readonly ILedgerRepository _repository;

    public RecordDirectTransferCommandHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(RecordDirectTransferCommand request, CancellationToken cancellationToken)
    {
        if (request.FromAccountId == request.ToAccountId)
        {
            return Result<Guid>.Failure("Source and destination accounts cannot be identical.");
        }

        if (request.Amount <= 0)
        {
            return Result<Guid>.Failure("Transfer amount must be greater than zero.");
        }

        Result<Guid> result = Result<Guid>.Failure("Transfer failed");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            // Deadlock prevention: Query balances ordered by Guid ascending
            var balances = await _repository.GetBalancesOrderedAsync(
                request.FromAccountId, 
                request.ToAccountId, 
                cancellationToken);

            var fromBalance = balances.FirstOrDefault(b => b.AccountId == request.FromAccountId);
            var toBalance = balances.FirstOrDefault(b => b.AccountId == request.ToAccountId);

            if (fromBalance == null)
            {
                throw new InsufficientFundsException(request.FromAccountId, request.Amount, 0m);
            }

            if (!string.Equals(fromBalance.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new CurrencyMismatchException(fromBalance.Currency, request.Currency);
            }

            if (toBalance == null)
            {
                toBalance = new AccountBalance(request.ToAccountId, request.Currency);
                await _repository.AddBalanceAsync(toBalance, cancellationToken);
            }
            else if (!string.Equals(toBalance.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new CurrencyMismatchException(toBalance.Currency, request.Currency);
            }

            // Debit from source (enforces balance invariant) and Credit to destination
            fromBalance.Debit(request.Amount);
            toBalance.Credit(request.Amount);

            // Double-entry bookkeeping: sum(Debit) = sum(Credit)
            var debitEntry = LedgerEntry.CreateDebit(
                request.TransactionId,
                request.FromAccountId,
                request.Amount,
                request.Currency,
                $"Debit for transfer to {request.ToAccountId}: {request.Description}");

            var creditEntry = LedgerEntry.CreateCredit(
                request.TransactionId,
                request.ToAccountId,
                request.Amount,
                request.Currency,
                $"Credit for transfer from {request.FromAccountId}: {request.Description}");

            await _repository.AddEntriesAsync(new[] { debitEntry, creditEntry }, cancellationToken);

            // Transactional Outbox
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "DirectTransferRecordedIntegrationEvent",
                Payload = JsonSerializer.Serialize(new
                {
                    request.TransactionId,
                    request.FromAccountId,
                    request.ToAccountId,
                    request.Amount,
                    Currency = request.Currency.ToUpperInvariant(),
                    request.Description,
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
