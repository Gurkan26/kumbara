using MediatR;
using Kumbara.Notification.Application.DTOs;
using Kumbara.Notification.Application.Interfaces;

namespace Kumbara.Notification.Application.Queries.GetRecentNotifications;

public class GetRecentNotificationsQueryHandler : IRequestHandler<GetRecentNotificationsQuery, IReadOnlyList<NotificationRecordDto>>
{
    private readonly INotificationRepository _repository;

    public GetRecentNotificationsQueryHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<NotificationRecordDto>> Handle(GetRecentNotificationsQuery request, CancellationToken cancellationToken)
    {
        var count = request.Count <= 0 ? 50 : request.Count;
        var records = await _repository.GetRecentAsync(count, cancellationToken);
        return records.Select(NotificationRecordDto.FromDomain).ToList();
    }
}
