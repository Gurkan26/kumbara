using System.Data;
using Microsoft.EntityFrameworkCore;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.Ledger.Domain.Enums;

namespace Kumbara.Ledger.Infrastructure.Persistence;

public class LedgerRepository : ILedgerRepository
{
    private readonly LedgerDbContext _context;

    public LedgerRepository(LedgerDbContext context)
    {
        _context = context;
    }

    public async Task<AccountBalance?> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _context.AccountBalances
            .FirstOrDefaultAsync(b => b.AccountId == accountId, cancellationToken);
    }

    /// <summary>
    /// Loads multiple account balances ordered by AccountId ascending.
    /// This prevents deadlocks when concurrent transfers involve the same accounts in reverse order.
    /// </summary>
    public async Task<List<AccountBalance>> GetBalancesOrderedAsync(
        Guid accountId1, 
        Guid accountId2, 
        CancellationToken cancellationToken = default)
    {
        var orderedIds = new[] { accountId1, accountId2 }.Distinct().OrderBy(id => id).ToList();

        return await _context.AccountBalances
            .Where(b => orderedIds.Contains(b.AccountId))
            .OrderBy(b => b.AccountId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddBalanceAsync(AccountBalance balance, CancellationToken cancellationToken = default)
    {
        await _context.AccountBalances.AddAsync(balance, cancellationToken);
    }

    public async Task AddEntryAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.LedgerEntries.AddAsync(entry, cancellationToken);
    }

    public async Task AddEntriesAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default)
    {
        await _context.LedgerEntries.AddRangeAsync(entries, cancellationToken);
    }

    public async Task<IEnumerable<LedgerEntry>> GetEntriesByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LedgerHold?> GetHoldByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerHolds
            .FirstOrDefaultAsync(h => h.TransactionId == transactionId && h.Status == HoldStatus.Active, cancellationToken);
    }

    public async Task AddHoldAsync(LedgerHold hold, CancellationToken cancellationToken = default)
    {
        await _context.LedgerHolds.AddAsync(hold, cancellationToken);
    }

    public async Task AddOutboxMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
