using Kumbara.Transfer.Application.DTOs;

namespace Kumbara.Transfer.Application.Services;

public interface ITransferQueryService
{
    Task<TransferDto?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
