using Microsoft.EntityFrameworkCore;
using Kumbara.Ledger.Domain.Entities;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.BuildingBlocks.Idempotency;

namespace Kumbara.Ledger.Application.Interfaces;

public interface ILedgerDbContext
{
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<AccountBalance> AccountBalances { get; }
    DbSet<LedgerHold> LedgerHolds { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
