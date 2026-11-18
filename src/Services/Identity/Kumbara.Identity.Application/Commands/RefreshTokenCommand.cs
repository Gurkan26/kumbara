namespace Kumbara.Identity.Application.Commands;

using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.Identity.Application.DTOs;
using Kumbara.Identity.Application.Interfaces;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<AuthResponse>.Failure("Refresh token is required.");

        try
        {
            var authResponse = await _tokenService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
            return Result<AuthResponse>.Success(authResponse);
        }
        catch (Exception ex)
        {
            return Result<AuthResponse>.Failure(ex.Message);
        }
    }
}
