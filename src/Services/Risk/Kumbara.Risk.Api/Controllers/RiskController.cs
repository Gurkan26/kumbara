namespace Kumbara.Risk.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kumbara.Risk.Application.Commands;
using Kumbara.Risk.Application.DTOs;

[ApiController]
[Route("api/[controller]")]
public class RiskController : ControllerBase
{
    private readonly IMediator _mediator;

    public RiskController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Synchronously evaluates risk for a transfer request.
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(EvaluateRiskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRiskRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request payload cannot be null.");
        }

        var transferId = request.TransferId ?? Guid.NewGuid();
        var command = new EvaluateRiskCommand(
            transferId,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Currency);

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new EvaluateRiskResponse(
            transferId,
            result.IsApproved,
            result.RejectionReason));
    }

    /// <summary>
    /// Health check endpoint for Risk service.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy", service = "Kumbara.Risk" });
    }
}
