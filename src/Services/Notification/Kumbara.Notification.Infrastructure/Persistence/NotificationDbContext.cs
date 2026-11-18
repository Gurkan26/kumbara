using Microsoft.EntityFrameworkCore;
using Kumbara.Notification.Domain.Entities;
using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationRecord>(entity =>
        {
            entity.ToTable("Notifications");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransferId)
                .IsRequired();

            entity.Property(e => e.Recipient)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.Channel)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            // Unique index for idempotency: Each TransferId can only be notified once per channel
            entity.HasIndex(e => new { e.TransferId, e.Channel })
                .IsUnique()
                .HasDatabaseName("IX_Notifications_TransferId_Channel");

            entity.HasIndex(e => e.TransferId)
                .HasDatabaseName("IX_Notifications_TransferId");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Notifications_CreatedAt");
        });
    }
}
