using Microsoft.AspNetCore.Mvc;
using MassTransit;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Transfer.Application.DTOs;
using Kumbara.Transfer.Application.Services;

namespace Kumbara.Transfer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITransferQueryService _queryService;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(
        IPublishEndpoint publishEndpoint,
        ITransferQueryService queryService,
        ILogger<TransfersController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _queryService = queryService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateTransfer(
        [FromBody] InitiateTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceAccountId == Guid.Empty || request.DestinationAccountId == Guid.Empty)
        {
            return BadRequest(new { error = "InvalidAccount", message = "Source and Destination account IDs must be valid GUIDs." });
        }

        if (request.SourceAccountId == request.DestinationAccountId)
        {
            return BadRequest(new { error = "InvalidTransfer", message = "Source and Destination account IDs cannot be the same." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { error = "InvalidAmount", message = "Transfer amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return BadRequest(new { error = "InvalidCurrency", message = "Currency is required." });
        }

        var transferId = Guid.NewGuid();

        _logger.LogInformation("Initiating transfer {TransferId} from account {SourceAccountId} to {DestinationAccountId} for {Amount} {Currency}",
            transferId, request.SourceAccountId, request.DestinationAccountId, request.Amount, request.Currency);

        await _publishEndpoint.Publish(new TransferStartedEvent(
            transferId,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Currency,
            DateTime.UtcNow), cancellationToken);

        var response = new
        {
            transferId = transferId,
            status = "Initiated",
            sourceAccountId = request.SourceAccountId,
            destinationAccountId = request.DestinationAccountId,
            amount = request.Amount,
            currency = request.Currency,
            createdAt = DateTime.UtcNow
        };

        return Accepted($"/api/transfers/{transferId}", response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransferById(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await _queryService.GetTransferByIdAsync(id, cancellationToken);
        if (transfer == null)
        {
            return NotFound(new { error = "NotFound", message = $"Transfer with ID {id} was not found." });
        }

        return Ok(transfer);
    }
}
