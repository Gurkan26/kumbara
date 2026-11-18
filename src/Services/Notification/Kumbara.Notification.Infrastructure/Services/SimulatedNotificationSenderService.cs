using Microsoft.Extensions.Logging;
using Kumbara.Notification.Application.Interfaces;
using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Infrastructure.Services;

public class SimulatedNotificationSenderService : INotificationSenderService
{
    private readonly ILogger<SimulatedNotificationSenderService> _logger;

    public SimulatedNotificationSenderService(ILogger<SimulatedNotificationSenderService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendNotificationAsync(
        Guid transferId,
        string recipient,
        string message,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[{Channel}] Notification dispatched successfully to {Recipient} for Transfer {TransferId}. Content: {Message}",
            channel, recipient, transferId, message);

        return Task.FromResult(true);
    }
}
