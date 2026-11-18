namespace Kumbara.Identity.Infrastructure.Services;

using Kumbara.Identity.Application.Interfaces;
using BCrypt.Net;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Verify(password, passwordHash);
    }
}
