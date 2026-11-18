namespace Kumbara.Identity.Application.Interfaces;

using Kumbara.Identity.Domain.Entities;

public interface IIdentityRepository
{
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task CreateUserAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
}
