namespace Kumbara.Wallet.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Kumbara.Wallet.Domain.Entities;
using Kumbara.Wallet.Domain.Enums;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options)
        : base(options)
    {
    }

    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallets");

            entity.HasKey(w => w.Id);

            entity.Property(w => w.UserId)
                .IsRequired();

            entity.HasIndex(w => w.UserId);

            entity.Property(w => w.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(w => w.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(w => w.DailyLimit)
                .HasPrecision(19, 4)
                .IsRequired();

            entity.Property(w => w.MonthlyLimit)
                .HasPrecision(19, 4)
                .IsRequired();

            entity.Property(w => w.CreatedAt)
                .IsRequired();

            entity.Property(w => w.UpdatedAt);

            entity.Ignore(w => w.DomainEvents);
        });
    }
}
