namespace Kumbara.Notification.Domain.Exceptions;

public class DuplicateNotificationException : Exception
{
    public Guid TransferId { get; }
    public Enums.NotificationChannel Channel { get; }

    public DuplicateNotificationException(Guid transferId, Enums.NotificationChannel channel)
        : base($"Transfer '{transferId}' has already been notified via channel '{channel}'.")
    {
        TransferId = transferId;
        Channel = channel;
    }
}
