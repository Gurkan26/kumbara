using Kumbara.Notification.Domain.Entities;
using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Application.Interfaces;

public interface INotificationRepository
{
    Task<bool> ExistsAsync(Guid transferId, NotificationChannel channel, CancellationToken cancellationToken = default);
    Task<NotificationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationRecord notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecord>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecord>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default);
}
