using MediatR;
using Microsoft.Extensions.Logging;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.Notification.Application.DTOs;
using Kumbara.Notification.Application.Interfaces;
using Kumbara.Notification.Domain.Entities;
using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Application.Commands.SendNotification;

public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, Result<NotificationRecordDto>>
{
    private readonly INotificationRepository _repository;
    private readonly INotificationSenderService _senderService;
    private readonly ILogger<SendNotificationCommandHandler> _logger;

    public SendNotificationCommandHandler(
        INotificationRepository repository,
        INotificationSenderService senderService,
        ILogger<SendNotificationCommandHandler> logger)
    {
        _repository = repository;
        _senderService = senderService;
        _logger = logger;
    }

    public async Task<Result<NotificationRecordDto>> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing SendNotificationCommand for Transfer {TransferId} on {Channel}", request.TransferId, request.Channel);

        // Domain rule / Idempotency check: Each TransferId can only be notified once per channel
        var exists = await _repository.ExistsAsync(request.TransferId, request.Channel, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Notification already sent for Transfer {TransferId} on {Channel}. Duplicate ignored.", request.TransferId, request.Channel);
            return Result<NotificationRecordDto>.Failure($"Transfer {request.TransferId} has already been notified via {request.Channel}.");
        }

        var isSent = await _senderService.SendNotificationAsync(request.TransferId, request.Recipient, request.Message, request.Channel, cancellationToken);
        var status = isSent ? NotificationStatus.Sent : NotificationStatus.Failed;

        var record = new NotificationRecord(request.TransferId, request.Recipient, request.Message, request.Channel, status);

        await _repository.AddAsync(record, cancellationToken);

        _logger.LogInformation("Notification record saved: {NotificationId} for Transfer {TransferId} with status {Status}", record.Id, record.TransferId, record.Status);

        return Result<NotificationRecordDto>.Success(NotificationRecordDto.FromDomain(record));
    }
}
