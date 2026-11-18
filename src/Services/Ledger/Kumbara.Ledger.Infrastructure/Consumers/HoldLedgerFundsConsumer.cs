using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Ledger.Application.Commands;

namespace Kumbara.Ledger.Infrastructure.Consumers;

public class HoldLedgerFundsConsumer : IConsumer<HoldLedgerFundsCommand>
{
    private readonly IMediator _mediator;
    private readonly ILogger<HoldLedgerFundsConsumer> _logger;

    public HoldLedgerFundsConsumer(IMediator mediator, ILogger<HoldLedgerFundsConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<HoldLedgerFundsCommand> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing HoldLedgerFundsCommand for TransferId: {TransferId}, AccountId: {AccountId}, Amount: {Amount}",
            message.TransferId, message.AccountId, message.Amount);

        try
        {
            var command = new HoldFundsCommand(
                message.TransferId,
                message.AccountId,
                message.Amount,
                message.Currency
            );

            var result = await _mediator.Send(command, context.CancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Funds held successfully for TransferId: {TransferId}, HoldId: {HoldId}",
                    message.TransferId, result.Value);
                await context.Publish(new FundsHeldEvent(message.TransferId, message.AccountId, message.Amount));
            }
            else
            {
                _logger.LogWarning("Funds hold failed for TransferId: {TransferId}: {Error}",
                    message.TransferId, result.Error);
                await context.Publish(new FundsHoldFailedEvent(message.TransferId, result.Error ?? "Failed to hold funds"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while holding funds for TransferId: {TransferId}", message.TransferId);
            await context.Publish(new FundsHoldFailedEvent(message.TransferId, ex.Message));
        }
    }
}
