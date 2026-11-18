namespace Kumbara.Identity.Application.Commands;

using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.Identity.Application.Interfaces;
using Kumbara.Identity.Domain.Entities;

public record RegisterUserCommand(string Email, string Password, string FullName) : IRequest<Result<Guid>>;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<Guid>>
{
    private readonly IIdentityRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IIdentityRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<Guid>.Failure("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return Result<Guid>.Failure("Password is required.");

        if (request.Password.Length < 6)
            return Result<Guid>.Failure("Password must be at least 6 characters.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await _repository.EmailExistsAsync(normalizedEmail, cancellationToken);
        if (exists)
            return Result<Guid>.Failure("User with this email already exists.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName?.Trim() ?? string.Empty,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Role = "User"
        };

        await _repository.CreateUserAsync(user, cancellationToken);

        return Result<Guid>.Success(user.Id);
    }
}
