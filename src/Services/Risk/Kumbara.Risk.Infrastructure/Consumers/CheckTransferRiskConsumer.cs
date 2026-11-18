namespace Kumbara.Risk.Infrastructure.Consumers;

using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Risk.Application.Commands;

public class CheckTransferRiskConsumer : IConsumer<CheckTransferRiskCommand>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CheckTransferRiskConsumer> _logger;

    public CheckTransferRiskConsumer(IMediator mediator, ILogger<CheckTransferRiskConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckTransferRiskCommand> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Consuming CheckTransferRiskCommand for TransferId: {TransferId}, Source: {Source}, Dest: {Dest}, Amount: {Amount} {Currency}",
            message.TransferId, message.SourceAccountId, message.DestinationAccountId, message.Amount, message.Currency);

        var command = new EvaluateRiskCommand(
            message.TransferId,
            message.SourceAccountId,
            message.DestinationAccountId,
            message.Amount,
            message.Currency);

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsApproved)
        {
            _logger.LogInformation("Transfer {TransferId} approved by Risk service. Publishing RiskApprovedEvent.", message.TransferId);
            await context.Publish(new RiskApprovedEvent(message.TransferId, DateTime.UtcNow), context.CancellationToken);
        }
        else
        {
            var reason = result.RejectionReason ?? "Risk check rejected by policy.";
            _logger.LogWarning("Transfer {TransferId} rejected by Risk service: {Reason}. Publishing RiskRejectedEvent.", message.TransferId, reason);
            await context.Publish(new RiskRejectedEvent(message.TransferId, reason, DateTime.UtcNow), context.CancellationToken);
        }
    }
}
