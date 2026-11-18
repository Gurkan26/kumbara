namespace Kumbara.Identity.Application.Commands;

using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.Identity.Application.DTOs;
using Kumbara.Identity.Application.Interfaces;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IIdentityRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IIdentityRepository repository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponse>.Failure("Email and password are required.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
            return Result<AuthResponse>.Failure("Invalid email or password.");

        if (!user.IsActive)
            return Result<AuthResponse>.Failure("User account is inactive.");

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
            return Result<AuthResponse>.Failure("Invalid email or password.");

        var tokens = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        return Result<AuthResponse>.Success(tokens);
    }
}
