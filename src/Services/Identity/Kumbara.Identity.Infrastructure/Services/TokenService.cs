namespace Kumbara.Identity.Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Kumbara.Identity.Application.DTOs;
using Kumbara.Identity.Application.Interfaces;
using Kumbara.Identity.Domain.Entities;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IIdentityRepository _repository;

    public TokenService(IConfiguration configuration, IIdentityRepository repository)
    {
        _configuration = configuration;
        _repository = repository;
    }

    public async Task<AuthResponse> GenerateTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var secret = _configuration["Jwt:Secret"] ?? "KumbaraSuperSecretKeyForDevelopmentPurposesOnly12345!";
        var issuer = _configuration["Jwt:Issuer"] ?? "Kumbara.Identity";
        var audience = _configuration["Jwt:Audience"] ?? "Kumbara";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);

        var refreshTokenString = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenString,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await _repository.SaveRefreshTokenAsync(refreshToken, cancellationToken);

        return new AuthResponse(accessToken, refreshTokenString, expiresAt);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var storedToken = await _repository.GetRefreshTokenAsync(refreshToken, cancellationToken);
        if (storedToken == null)
        {
            throw new InvalidOperationException("Refresh token not found.");
        }

        if (storedToken.IsRevoked)
        {
            throw new InvalidOperationException("Refresh token has been revoked.");
        }

        if (storedToken.IsExpired)
        {
            throw new InvalidOperationException("Refresh token has expired.");
        }

        await _repository.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        var user = await _repository.GetByIdAsync(storedToken.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new InvalidOperationException("User associated with token not found or inactive.");
        }

        return await GenerateTokensAsync(user, cancellationToken);
    }
}
