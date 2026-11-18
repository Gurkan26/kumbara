namespace Kumbara.Identity.Application.DTOs;

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
