using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Ledger.Application.Commands;

namespace Kumbara.Ledger.Infrastructure.Consumers;

public class CommitLedgerHoldConsumer : IConsumer<CommitLedgerHoldCommand>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CommitLedgerHoldConsumer> _logger;

    public CommitLedgerHoldConsumer(IMediator mediator, ILogger<CommitLedgerHoldConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CommitLedgerHoldCommand> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing CommitLedgerHoldCommand for TransferId: {TransferId}, DestinationAccountId: {DestinationAccountId}",
            message.TransferId, message.DestinationAccountId);

        try
        {
            var command = new CommitHoldCommand(
                message.TransferId,
                message.DestinationAccountId
            );

            var result = await _mediator.Send(command, context.CancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Hold committed successfully for TransferId: {TransferId}", message.TransferId);
                await context.Publish(new HoldCommittedEvent(message.TransferId));
            }
            else
            {
                _logger.LogWarning("Hold commit failed for TransferId: {TransferId}: {Error}",
                    message.TransferId, result.Error);
                await context.Publish(new HoldCommitFailedEvent(message.TransferId, result.Error ?? "Failed to commit hold"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while committing hold for TransferId: {TransferId}", message.TransferId);
            await context.Publish(new HoldCommitFailedEvent(message.TransferId, ex.Message));
        }
    }
}
