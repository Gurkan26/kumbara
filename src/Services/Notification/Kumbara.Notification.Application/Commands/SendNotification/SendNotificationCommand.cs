using MediatR;
using Kumbara.BuildingBlocks.Contracts.Common;
using Kumbara.Notification.Application.DTOs;
using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Application.Commands.SendNotification;

public record SendNotificationCommand(
    Guid TransferId,
    string Recipient,
    string Message,
    NotificationChannel Channel) : IRequest<Result<NotificationRecordDto>>;
