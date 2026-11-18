namespace Kumbara.Wallet.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kumbara.Wallet.Api.Models;
using Kumbara.Wallet.Application.Commands;
using Kumbara.Wallet.Application.DTOs;
using Kumbara.Wallet.Application.Queries;

[ApiController]
[Route("api/[controller]")]
public class WalletsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WalletsController> _logger;

    public WalletsController(IMediator mediator, ILogger<WalletsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new wallet for a user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWalletRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating wallet for user {UserId} with currency {Currency}", request.UserId, request.Currency);

        if (request.UserId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Fail("UserId is required."));

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
            return BadRequest(ApiResponse<object>.Fail("Currency must be a valid 3-letter ISO code."));

        try
        {
            var command = new CreateWalletCommand(request.UserId, request.Currency, request.DailyLimit, request.MonthlyLimit);
            var walletId = await _mediator.Send(command, cancellationToken);
            var wallet = await _mediator.Send(new GetWalletByIdQuery(walletId), cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = walletId }, ApiResponse<WalletDto>.Ok(wallet!, "Wallet created successfully."));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error creating wallet");
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Domain operation error creating wallet");
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Gets a wallet by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching wallet by ID: {WalletId}", id);
        var wallet = await _mediator.Send(new GetWalletByIdQuery(id), cancellationToken);

        if (wallet == null)
            return NotFound(ApiResponse<object>.Fail($"Wallet with ID '{id}' was not found."));

        return Ok(ApiResponse<WalletDto>.Ok(wallet));
    }

    /// <summary>
    /// Gets all wallets belonging to a user.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WalletDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching wallets for user: {UserId}", userId);
        var wallets = await _mediator.Send(new GetWalletsByUserIdQuery(userId), cancellationToken);

        return Ok(ApiResponse<IEnumerable<WalletDto>>.Ok(wallets));
    }

    /// <summary>
    /// Freezes an active or suspended wallet.
    /// </summary>
    [HttpPost("{id:guid}/freeze")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Freeze(Guid id, [FromBody] FreezeWalletRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Freezing wallet: {WalletId}, reason: {Reason}", id, request.Reason);
        try
        {
            var command = new FreezeWalletCommand(id, request.Reason);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<bool>.Ok(result, "Wallet frozen successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Unfreezes a frozen wallet back to active.
    /// </summary>
    [HttpPost("{id:guid}/unfreeze")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unfreeze(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unfreezing wallet: {WalletId}", id);
        try
        {
            var command = new UnfreezeWalletCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<bool>.Ok(result, "Wallet unfrozen successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Updates the daily and monthly limits of a wallet.
    /// </summary>
    [HttpPut("{id:guid}/limits")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLimits(Guid id, [FromBody] UpdateLimitsRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating limits for wallet: {WalletId}, daily: {Daily}, monthly: {Monthly}",
            id, request.DailyLimit, request.MonthlyLimit);
        try
        {
            var command = new UpdateWalletLimitsCommand(id, request.DailyLimit, request.MonthlyLimit);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<bool>.Ok(result, "Wallet limits updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
