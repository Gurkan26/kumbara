namespace Kumbara.Identity.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Kumbara.Identity.Application.Interfaces;
using Kumbara.Identity.Domain.Entities;

public class IdentityRepository : IIdentityRepository
{
    private readonly IdentityDbContext _context;

    public IdentityRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task CreateUserAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
        if (existing != null)
        {
            existing.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
