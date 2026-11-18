using MediatR;
using Kumbara.Notification.Application.DTOs;
using Kumbara.Notification.Application.Interfaces;

namespace Kumbara.Notification.Application.Queries.GetNotificationsByTransferId;

public class GetNotificationsByTransferIdQueryHandler : IRequestHandler<GetNotificationsByTransferIdQuery, IReadOnlyList<NotificationRecordDto>>
{
    private readonly INotificationRepository _repository;

    public GetNotificationsByTransferIdQueryHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<NotificationRecordDto>> Handle(GetNotificationsByTransferIdQuery request, CancellationToken cancellationToken)
    {
        var records = await _repository.GetByTransferIdAsync(request.TransferId, cancellationToken);
        return records.Select(NotificationRecordDto.FromDomain).ToList();
    }
}
