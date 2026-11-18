using System.Text.Json;
using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Exceptions;

namespace Kumbara.Ledger.Application.Commands;

public record CommitHoldCommand(
    Guid TransactionId,
    Guid DestinationAccountId
) : IRequest<Result<Guid>>;

public class CommitHoldCommandHandler : IRequestHandler<CommitHoldCommand, Result<Guid>>
{
    private readonly ILedgerRepository _repository;

    public CommitHoldCommandHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(CommitHoldCommand request, CancellationToken cancellationToken)
    {
        Result<Guid> result = Result<Guid>.Failure("Commit hold failed");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var hold = await _repository.GetHoldByTransactionIdAsync(request.TransactionId, cancellationToken);
            if (hold == null)
            {
                result = Result<Guid>.Failure($"Active hold not found for transaction {request.TransactionId}.");
                return;
            }

            // Deadlock prevention: Query balances ordered by Guid ascending
            var balances = await _repository.GetBalancesOrderedAsync(
                hold.AccountId,
                request.DestinationAccountId,
                cancellationToken);

            var sourceBalance = balances.FirstOrDefault(b => b.AccountId == hold.AccountId);
            var destBalance = balances.FirstOrDefault(b => b.AccountId == request.DestinationAccountId);

            if (sourceBalance == null)
            {
                result = Result<Guid>.Failure($"Source account balance for account {hold.AccountId} not found.");
                return;
            }

            if (destBalance == null)
            {
                destBalance = new AccountBalance(request.DestinationAccountId, hold.Currency);
                await _repository.AddBalanceAsync(destBalance, cancellationToken);
            }
            else if (!string.Equals(destBalance.Currency, hold.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new CurrencyMismatchException(destBalance.Currency, hold.Currency);
            }

            // Mark hold committed
            hold.Commit();

            // Decrement source blocked balance and credit destination available balance
            sourceBalance.CommitHold(hold.Amount);
            destBalance.Credit(hold.Amount);

            // Double-entry bookkeeping: sum(Debit) = sum(Credit)
            var debitEntry = LedgerEntry.CreateDebit(
                request.TransactionId,
                hold.AccountId,
                hold.Amount,
                hold.Currency,
                $"Committed hold to {request.DestinationAccountId}");

            var creditEntry = LedgerEntry.CreateCredit(
                request.TransactionId,
                request.DestinationAccountId,
                hold.Amount,
                hold.Currency,
                $"Committed hold from {hold.AccountId}");

            await _repository.AddEntriesAsync(new[] { debitEntry, creditEntry }, cancellationToken);

            // Transactional Outbox
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "HoldCommittedIntegrationEvent",
                Payload = JsonSerializer.Serialize(new
                {
                    HoldId = hold.Id,
                    request.TransactionId,
                    SourceAccountId = hold.AccountId,
                    request.DestinationAccountId,
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
