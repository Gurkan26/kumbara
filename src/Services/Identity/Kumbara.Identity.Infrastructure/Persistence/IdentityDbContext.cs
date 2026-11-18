namespace Kumbara.Identity.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Kumbara.Identity.Domain.Entities;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.FullName).HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(50).HasDefaultValue("User");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Token).IsUnique();
            entity.Property(t => t.Token).IsRequired().HasMaxLength(512);
            entity.HasIndex(t => t.UserId);
        });
    }
}
