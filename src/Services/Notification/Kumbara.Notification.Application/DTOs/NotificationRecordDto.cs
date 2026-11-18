using Kumbara.Notification.Domain.Enums;
using Kumbara.Notification.Domain.Entities;

namespace Kumbara.Notification.Application.DTOs;

public record NotificationRecordDto(
    Guid Id,
    Guid TransferId,
    string Recipient,
    string Message,
    NotificationChannel Channel,
    NotificationStatus Status,
    DateTime CreatedAt)
{
    public static NotificationRecordDto FromDomain(NotificationRecord record) =>
        new(record.Id, record.TransferId, record.Recipient, record.Message, record.Channel, record.Status, record.CreatedAt);
}
