using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Ledger.Application.Commands;

namespace Kumbara.Ledger.Infrastructure.Consumers;

public class ReleaseLedgerHoldConsumer : IConsumer<ReleaseLedgerHoldCommand>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReleaseLedgerHoldConsumer> _logger;

    public ReleaseLedgerHoldConsumer(IMediator mediator, ILogger<ReleaseLedgerHoldConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleaseLedgerHoldCommand> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing ReleaseLedgerHoldCommand for TransferId: {TransferId}", message.TransferId);

        try
        {
            var command = new ReleaseHoldCommand(message.TransferId);
            var result = await _mediator.Send(command, context.CancellationToken);

            _logger.LogInformation("Hold released successfully for TransferId: {TransferId}", message.TransferId);
            await context.Publish(new HoldReleasedEvent(message.TransferId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while releasing hold for TransferId: {TransferId}", message.TransferId);
            await context.Publish(new HoldReleasedEvent(message.TransferId));
        }
    }
}
