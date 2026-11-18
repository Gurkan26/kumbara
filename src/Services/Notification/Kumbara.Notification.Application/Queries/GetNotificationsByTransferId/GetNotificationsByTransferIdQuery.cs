using MediatR;
using Kumbara.Notification.Application.DTOs;

namespace Kumbara.Notification.Application.Queries.GetNotificationsByTransferId;

public record GetNotificationsByTransferIdQuery(Guid TransferId) : IRequest<IReadOnlyList<NotificationRecordDto>>;
