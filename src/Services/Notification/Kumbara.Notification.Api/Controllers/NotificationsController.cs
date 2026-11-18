namespace Kumbara.Notification.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Kumbara.Notification.Application.Commands.SendNotification;
using Kumbara.Notification.Application.DTOs;
using Kumbara.Notification.Application.Queries.GetNotificationsByTransferId;
using Kumbara.Notification.Application.Queries.GetRecentNotifications;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all notification records associated with a specific transfer ID.
    /// </summary>
    [HttpGet("transfer/{transferId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTransferId([FromRoute] Guid transferId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetNotificationsByTransferIdQuery(transferId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets recent notifications across the system.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 50, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetRecentNotificationsQuery(count), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Dispatches a notification manually.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(NotificationRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
