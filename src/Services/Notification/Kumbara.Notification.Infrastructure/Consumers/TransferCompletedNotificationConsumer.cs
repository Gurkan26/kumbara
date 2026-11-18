using MassTransit;
using Microsoft.Extensions.Logging;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Notification.Application.Interfaces;
using Kumbara.Notification.Domain.Entities;
using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Infrastructure.Consumers;

public class TransferCompletedNotificationConsumer : IConsumer<TransferCompletedEvent>
{
    private readonly INotificationRepository _repository;
    private readonly INotificationSenderService _senderService;
    private readonly ILogger<TransferCompletedNotificationConsumer> _logger;

    public TransferCompletedNotificationConsumer(
        INotificationRepository repository,
        INotificationSenderService senderService,
        ILogger<TransferCompletedNotificationConsumer> logger)
    {
        _repository = repository;
        _senderService = senderService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransferCompletedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received TransferCompletedEvent for Transfer {TransferId} occurred at {OccurredAt:u}",
            message.TransferId, message.OccurredAt);

        const NotificationChannel channel = NotificationChannel.Email;
        const string recipient = "customer@kumbara.local";

        // Domain rule / Idempotency check: Each TransferId can only be notified once per channel
        var alreadyNotified = await _repository.ExistsAsync(message.TransferId, channel, context.CancellationToken);
        if (alreadyNotified)
        {
            _logger.LogWarning("TransferCompletedNotificationConsumer: Notification already recorded for Transfer {TransferId} on {Channel}. Skipping duplicate event.",
                message.TransferId, channel);
            return;
        }

        var notificationText = $"Congratulations! Your transfer with ID {message.TransferId} has completed successfully at {message.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC.";

        var isSent = await _senderService.SendNotificationAsync(
            message.TransferId,
            recipient,
            notificationText,
            channel,
            context.CancellationToken);

        var status = isSent ? NotificationStatus.Sent : NotificationStatus.Failed;
        var record = new NotificationRecord(
            message.TransferId,
            recipient,
            notificationText,
            channel,
            status,
            createdAt: message.OccurredAt);

        await _repository.AddAsync(record, context.CancellationToken);

        _logger.LogInformation(
            "TransferCompletedNotificationConsumer: Idempotent notification record {NotificationId} created for Transfer {TransferId} with status {Status}",
            record.Id, message.TransferId, record.Status);
    }
}
