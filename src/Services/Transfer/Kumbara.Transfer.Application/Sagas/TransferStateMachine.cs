using MassTransit;
using Kumbara.BuildingBlocks.Contracts.Saga;
using Kumbara.Transfer.Domain.Entities;

namespace Kumbara.Transfer.Application.Sagas;

public class TransferStateMachine : MassTransitStateMachine<TransferState>
{
    public State AwaitingRisk { get; private set; } = null!;
    public State AwaitingHold { get; private set; } = null!;
    public State AwaitingCommit { get; private set; } = null!;
    public State AwaitingCompensation { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<TransferStartedEvent> TransferStarted { get; private set; } = null!;
    public Event<RiskApprovedEvent> RiskApproved { get; private set; } = null!;
    public Event<RiskRejectedEvent> RiskRejected { get; private set; } = null!;
    public Event<FundsHeldEvent> FundsHeld { get; private set; } = null!;
    public Event<FundsHoldFailedEvent> FundsHoldFailed { get; private set; } = null!;
    public Event<HoldCommittedEvent> HoldCommitted { get; private set; } = null!;
    public Event<HoldCommitFailedEvent> HoldCommitFailed { get; private set; } = null!;
    public Event<HoldReleasedEvent> HoldReleased { get; private set; } = null!;

    public TransferStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => TransferStarted, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => RiskApproved, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => RiskRejected, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => FundsHeld, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => FundsHoldFailed, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => HoldCommitted, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => HoldCommitFailed, x => x.CorrelateById(m => m.Message.TransferId));
        Event(() => HoldReleased, x => x.CorrelateById(m => m.Message.TransferId));

        // 1. Transfer Initiated
        Initially(
            When(TransferStarted)
                .Then(context =>
                {
                    context.Saga.CorrelationId = context.Message.TransferId;
                    context.Saga.SourceAccountId = context.Message.SourceAccountId;
                    context.Saga.DestinationAccountId = context.Message.DestinationAccountId;
                    context.Saga.Amount = context.Message.Amount;
                    context.Saga.Currency = context.Message.Currency;
                    context.Saga.CreatedAt = DateTime.UtcNow;
                })
                .Publish(context => new CheckTransferRiskCommand(
                    context.Saga.CorrelationId,
                    context.Saga.SourceAccountId,
                    context.Saga.DestinationAccountId,
                    context.Saga.Amount,
                    context.Saga.Currency))
                .TransitionTo(AwaitingRisk)
        );

        // 2. Risk Decision
        During(AwaitingRisk,
            When(RiskApproved)
                .Publish(context => new HoldLedgerFundsCommand(
                    context.Saga.CorrelationId,
                    context.Saga.SourceAccountId,
                    context.Saga.Amount,
                    context.Saga.Currency))
                .TransitionTo(AwaitingHold),
            When(RiskRejected)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .Publish(context => new TransferFailedEvent(
                    context.Saga.CorrelationId,
                    context.Message.Reason))
                .TransitionTo(Failed)
        );

        // 3. Ledger Hold Decision
        During(AwaitingHold,
            When(FundsHeld)
                .Publish(context => new CommitLedgerHoldCommand(
                    context.Saga.CorrelationId,
                    context.Saga.DestinationAccountId))
                .TransitionTo(AwaitingCommit),
            When(FundsHoldFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .Publish(context => new TransferFailedEvent(
                    context.Saga.CorrelationId,
                    context.Message.Reason))
                .TransitionTo(Failed)
        );

        // 4. Ledger Commit Decision
        During(AwaitingCommit,
            When(HoldCommitted)
                .Then(context =>
                {
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .Publish(context => new TransferCompletedEvent(context.Saga.CorrelationId))
                .TransitionTo(Completed),
            When(HoldCommitFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                })
                .Publish(context => new ReleaseLedgerHoldCommand(context.Saga.CorrelationId))
                .TransitionTo(AwaitingCompensation)
        );

        // 5. Compensation
        During(AwaitingCompensation,
            When(HoldReleased)
                .Then(context =>
                {
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .Publish(context => new TransferFailedEvent(
                    context.Saga.CorrelationId,
                    context.Saga.FailureReason ?? "Compensation completed"))
                .TransitionTo(Failed)
        );
    }
}
