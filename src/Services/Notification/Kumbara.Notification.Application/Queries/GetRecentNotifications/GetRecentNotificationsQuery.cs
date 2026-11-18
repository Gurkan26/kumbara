using MediatR;
using Kumbara.Notification.Application.DTOs;

namespace Kumbara.Notification.Application.Queries.GetRecentNotifications;

public record GetRecentNotificationsQuery(int Count = 50) : IRequest<IReadOnlyList<NotificationRecordDto>>;
