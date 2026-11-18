using System.Text.Json;
using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Exceptions;

namespace Kumbara.Ledger.Application.Commands;

public record DepositFundsCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    Guid? TransactionId = null,
    string? Description = null
) : IRequest<Result<Guid>>;

public class DepositFundsCommandHandler : IRequestHandler<DepositFundsCommand, Result<Guid>>
{
    private readonly ILedgerRepository _repository;
    private static readonly Guid SystemCashClearingAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DepositFundsCommandHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(DepositFundsCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Result<Guid>.Failure("Deposit amount must be greater than zero.");
        }

        var transactionId = request.TransactionId ?? Guid.NewGuid();
        var description = request.Description ?? "Initial deposit / fund seeding";
        Result<Guid> result = Result<Guid>.Failure("Deposit failed");

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var balance = await _repository.GetBalanceAsync(request.AccountId, cancellationToken);
            if (balance == null)
            {
                balance = new AccountBalance(request.AccountId, request.Currency);
                await _repository.AddBalanceAsync(balance, cancellationToken);
            }
            else if (!string.Equals(balance.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new CurrencyMismatchException(balance.Currency, request.Currency);
            }

            balance.Credit(request.Amount);

            // Double-entry bookkeeping: System clearing account debited, customer account credited
            var debitEntry = LedgerEntry.CreateDebit(
                transactionId,
                SystemCashClearingAccountId,
                request.Amount,
                request.Currency,
                $"Clearing debit for customer deposit: {description}");

            var creditEntry = LedgerEntry.CreateCredit(
                transactionId,
                request.AccountId,
                request.Amount,
                request.Currency,
                $"Deposit: {description}");

            await _repository.AddEntriesAsync(new[] { debitEntry, creditEntry }, cancellationToken);

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "FundsDepositedIntegrationEvent",
                Payload = JsonSerializer.Serialize(new
                {
                    TransactionId = transactionId,
                    request.AccountId,
                    request.Amount,
                    Currency = request.Currency.ToUpperInvariant(),
                    Description = description,
                    OccurredAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            result = Result<Guid>.Success(transactionId);
        }, cancellationToken);

        return result;
    }
}
