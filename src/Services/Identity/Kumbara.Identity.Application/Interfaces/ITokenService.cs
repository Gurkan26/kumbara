namespace Kumbara.Identity.Application.Interfaces;

using Kumbara.Identity.Application.DTOs;
using Kumbara.Identity.Domain.Entities;

public interface ITokenService
{
    Task<AuthResponse> GenerateTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
