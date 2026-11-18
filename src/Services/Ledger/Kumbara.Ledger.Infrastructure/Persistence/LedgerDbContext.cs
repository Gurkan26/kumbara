using Microsoft.EntityFrameworkCore;
using Kumbara.BuildingBlocks.Idempotency;
using Kumbara.BuildingBlocks.Outbox;
using Kumbara.Ledger.Application.Interfaces;
using Kumbara.Ledger.Domain.Entities;

namespace Kumbara.Ledger.Infrastructure.Persistence;

public class LedgerDbContext : DbContext, ILedgerDbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options)
    {
    }

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();
    public DbSet<LedgerHold> LedgerHolds => Set<LedgerHold>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. LedgerEntry (Append-Only)
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("LedgerEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionId).IsRequired();
            entity.Property(e => e.AccountId).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
            entity.Property(e => e.Type).HasConversion<int>().IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // 2. AccountBalance (Snapshot with RowVersion for Optimistic Concurrency)
        modelBuilder.Entity<AccountBalance>(entity =>
        {
            entity.ToTable("AccountBalances");
            entity.HasKey(e => e.AccountId);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.AvailableBalance).HasPrecision(19, 4).IsRequired();
            entity.Property(e => e.BlockedBalance).HasPrecision(19, 4).IsRequired();
            entity.Ignore(e => e.TotalBalance);

            // Optimistic concurrency token
            entity.Property(e => e.RowVersion)
                  .IsRowVersion()
                  .IsConcurrencyToken();
        });

        // 3. LedgerHold
        modelBuilder.Entity<LedgerHold>(entity =>
        {
            entity.ToTable("LedgerHolds");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionId).IsRequired();
            entity.Property(e => e.AccountId).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(19, 4).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>().IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.AccountId);
        });

        // 4. OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.ProcessedAt);
            entity.HasIndex(e => e.CreatedAt);
        });

        // 5. IdempotencyRecord
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(e => e.IdempotencyKey);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(256);
            entity.Property(e => e.RequestHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Enforce Append-Only invariant for LedgerEntry
        var illegalEntries = ChangeTracker.Entries<LedgerEntry>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        if (illegalEntries.Count > 0)
        {
            throw new InvalidOperationException("LedgerEntry is APPEND-ONLY! Modification or deletion is strictly prohibited.");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
