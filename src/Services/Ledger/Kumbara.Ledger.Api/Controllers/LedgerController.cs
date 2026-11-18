using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kumbara.Ledger.Api.Filters;
using Kumbara.Ledger.Api.Models;
using Kumbara.Ledger.Application.Commands;
using Kumbara.Ledger.Application.Queries;

namespace Kumbara.Ledger.Api.Controllers;

[ApiController]
[Route("api/ledger")]
public class LedgerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<LedgerController> _logger;

    public LedgerController(IMediator mediator, ILogger<LedgerController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Executes a double-entry direct transfer between two accounts.
    /// Requires Idempotency-Key header to prevent duplicate execution.
    /// </summary>
    [HttpPost("transfer")]
    [RequireIdempotencyKey]
    public async Task<IActionResult> Transfer(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken)
    {
        var transactionId = request.TransactionId ?? Guid.NewGuid();
        var command = new RecordDirectTransferCommand(
            transactionId,
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            request.Currency,
            request.Description ?? "Direct transfer"
        );

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            transactionId = result.Value,
            status = "Success",
            message = "Transfer completed successfully."
        });
    }

    /// <summary>
    /// Initial seeding or fund deposit into an account.
    /// </summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromBody] DepositRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DepositFundsCommand(
            request.AccountId,
            request.Amount,
            request.Currency,
            request.TransactionId,
            request.Description
        );

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            transactionId = result.Value,
            status = "Success",
            message = "Deposit processed successfully."
        });
    }

    /// <summary>
    /// Holds (bloke) funds on an account. Decrements available balance and increments blocked balance.
    /// </summary>
    [HttpPost("hold")]
    public async Task<IActionResult> HoldFunds(
        [FromBody] HoldRequest request,
        CancellationToken cancellationToken)
    {
        var command = new HoldFundsCommand(
            request.TransactionId,
            request.AccountId,
            request.Amount,
            request.Currency
        );

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            holdId = result.Value,
            transactionId = request.TransactionId,
            status = "Success",
            message = "Funds held successfully."
        });
    }

    /// <summary>
    /// Commits previously held funds and transfers them to the destination account.
    /// </summary>
    [HttpPost("commit-hold")]
    public async Task<IActionResult> CommitHold(
        [FromBody] CommitHoldRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CommitHoldCommand(
            request.TransactionId,
            request.DestinationAccountId
        );

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            transactionId = result.Value,
            status = "Success",
            message = "Hold committed successfully."
        });
    }

    /// <summary>
    /// Releases previously held funds back to the available balance.
    /// </summary>
    [HttpPost("release-hold")]
    public async Task<IActionResult> ReleaseHold(
        [FromBody] ReleaseHoldRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReleaseHoldCommand(request.TransactionId);

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            transactionId = result.Value,
            status = "Success",
            message = "Hold released successfully."
        });
    }

    /// <summary>
    /// Gets the snapshot balance for an account.
    /// </summary>
    [HttpGet("accounts/{accountId:guid}/balance")]
    public async Task<IActionResult> GetAccountBalance(
        [FromRoute] Guid accountId,
        CancellationToken cancellationToken)
    {
        var query = new GetAccountBalanceQuery(accountId);
        var balance = await _mediator.Send(query, cancellationToken);

        if (balance == null)
        {
            return NotFound(new { error = $"Account balance not found for account {accountId}." });
        }

        return Ok(balance);
    }

    /// <summary>
    /// Gets the append-only ledger entries for an account.
    /// </summary>
    [HttpGet("accounts/{accountId:guid}/entries")]
    public async Task<IActionResult> GetAccountEntries(
        [FromRoute] Guid accountId,
        CancellationToken cancellationToken)
    {
        var query = new GetAccountEntriesQuery(accountId);
        var entries = await _mediator.Send(query, cancellationToken);

        return Ok(entries);
    }
}
