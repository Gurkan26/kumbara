namespace Kumbara.Identity.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kumbara.Identity.Api.Models;
using Kumbara.Identity.Application.Commands;
using Kumbara.Identity.Application.DTOs;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing user registration for email: {Email}", request.Email);
        var command = new RegisterUserCommand(request.Email, request.Password, request.FullName);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Registration failed for {Email}: {Error}", request.Email, result.Error);
            return BadRequest(ApiResponse<object>.Fail(result.Error ?? "Registration failed."));
        }

        return Ok(ApiResponse<object>.Ok(new { userId = result.Value }, "User registered successfully."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing user login for email: {Email}", request.Email);
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Login failed for {Email}: {Error}", request.Email, result.Error);
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.Error ?? "Invalid credentials."));
        }

        return Ok(ApiResponse<AuthResponse>.Ok(result.Value!, "Login successful."));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing refresh token request");
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Token refresh failed: {Error}", result.Error);
            return BadRequest(ApiResponse<AuthResponse>.Fail(result.Error ?? "Token refresh failed."));
        }

        return Ok(ApiResponse<AuthResponse>.Ok(result.Value!, "Token refreshed successfully."));
    }
}
