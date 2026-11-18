using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Application.Interfaces;

public interface INotificationSenderService
{
    Task<bool> SendNotificationAsync(Guid transferId, string recipient, string message, NotificationChannel channel, CancellationToken cancellationToken = default);
}
