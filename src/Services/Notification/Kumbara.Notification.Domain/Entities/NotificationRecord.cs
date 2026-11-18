using Kumbara.Notification.Domain.Enums;

namespace Kumbara.Notification.Domain.Entities;

public class NotificationRecord
{
    public Guid Id { get; private set; }
    public Guid TransferId { get; private set; }
    public string Recipient { get; private set; }
    public string Message { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core constructor
    private NotificationRecord()
    {
        Recipient = string.Empty;
        Message = string.Empty;
    }

    public NotificationRecord(
        Guid transferId,
        string recipient,
        string message,
        NotificationChannel channel,
        NotificationStatus status = NotificationStatus.Sent,
        Guid? id = null,
        DateTime? createdAt = null)
    {
        if (transferId == Guid.Empty)
            throw new ArgumentException("TransferId cannot be empty.", nameof(transferId));

        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Recipient cannot be null or empty.", nameof(recipient));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));

        Id = id ?? Guid.NewGuid();
        TransferId = transferId;
        Recipient = recipient.Trim();
        Message = message.Trim();
        Channel = channel;
        Status = status;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = NotificationStatus.Failed;
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
    }
}
