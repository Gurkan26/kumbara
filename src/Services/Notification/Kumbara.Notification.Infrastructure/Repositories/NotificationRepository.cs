using Microsoft.EntityFrameworkCore;
using Kumbara.Notification.Application.Interfaces;
using Kumbara.Notification.Domain.Entities;
using Kumbara.Notification.Domain.Enums;
using Kumbara.Notification.Domain.Exceptions;
using Kumbara.Notification.Infrastructure.Persistence;

namespace Kumbara.Notification.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _dbContext;

    public NotificationRepository(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsAsync(Guid transferId, NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.TransferId == transferId && n.Channel == channel, cancellationToken);
    }

    public async Task<NotificationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task AddAsync(NotificationRecord notification, CancellationToken cancellationToken = default)
    {
        // Enforce idempotency check before insert
        var exists = await ExistsAsync(notification.TransferId, notification.Channel, cancellationToken);
        if (exists)
        {
            throw new DuplicateNotificationException(notification.TransferId, notification.Channel);
        }

        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.TransferId == transferId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var takeCount = count <= 0 ? 50 : count;
        return await _dbContext.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(takeCount)
            .ToListAsync(cancellationToken);
    }
}
