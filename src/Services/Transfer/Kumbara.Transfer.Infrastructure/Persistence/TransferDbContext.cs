using Microsoft.EntityFrameworkCore;
using Kumbara.BuildingBlocks.Idempotency;
using Kumbara.Transfer.Domain.Entities;
using Kumbara.Transfer.Infrastructure.Persistence.Maps;

namespace Kumbara.Transfer.Infrastructure.Persistence;

public class TransferDbContext : DbContext
{
    public TransferDbContext(DbContextOptions<TransferDbContext> options) : base(options)
    {
    }

    public DbSet<TransferState> TransferStates => Set<TransferState>();
    public DbSet<TransferState> Transfers => TransferStates;
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        new TransferStateMap().Configure(modelBuilder);

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(e => e.IdempotencyKey);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(256);
            entity.Property(e => e.RequestHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ResponseBody).IsRequired();
            entity.Property(e => e.StatusCode).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
