using Kumbara.Ledger.Domain.Entities;
using Kumbara.BuildingBlocks.Outbox;

namespace Kumbara.Ledger.Application.Interfaces;

public interface ILedgerRepository
{
    Task<AccountBalance?> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<List<AccountBalance>> GetBalancesOrderedAsync(Guid accountId1, Guid accountId2, CancellationToken cancellationToken = default);
    Task AddBalanceAsync(AccountBalance balance, CancellationToken cancellationToken = default);
    Task AddEntryAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddEntriesAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);
    Task<IEnumerable<LedgerEntry>> GetEntriesByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<LedgerHold?> GetHoldByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task AddHoldAsync(LedgerHold hold, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
