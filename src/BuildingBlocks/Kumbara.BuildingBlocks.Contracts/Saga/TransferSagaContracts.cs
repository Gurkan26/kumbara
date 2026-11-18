namespace Kumbara.BuildingBlocks.Contracts.Saga;

using Kumbara.BuildingBlocks.Contracts.IntegrationEvents;

// ============================================================================
// Transfer Saga Commands
// ============================================================================

public record CheckTransferRiskCommand(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency);

public record HoldLedgerFundsCommand(
    Guid TransferId,
    Guid AccountId,
    decimal Amount,
    string Currency);

public record CommitLedgerHoldCommand(
    Guid TransferId,
    Guid DestinationAccountId);

public record ReleaseLedgerHoldCommand(
    Guid TransferId);

// ============================================================================
// Transfer Saga Events
// ============================================================================

public record TransferStartedEvent(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public TransferStartedEvent(Guid transferId, Guid sourceAccountId, Guid destinationAccountId, decimal amount, string currency)
        : this(transferId, sourceAccountId, destinationAccountId, amount, currency, DateTime.UtcNow) { }
}

public record RiskApprovedEvent(
    Guid TransferId,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public RiskApprovedEvent(Guid transferId)
        : this(transferId, DateTime.UtcNow) { }
}

public record RiskRejectedEvent(
    Guid TransferId,
    string Reason,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public RiskRejectedEvent(Guid transferId, string reason)
        : this(transferId, reason, DateTime.UtcNow) { }
}

public record FundsHeldEvent(
    Guid TransferId,
    Guid AccountId,
    decimal Amount,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public FundsHeldEvent(Guid transferId, Guid accountId, decimal amount)
        : this(transferId, accountId, amount, DateTime.UtcNow) { }
}

public record FundsHoldFailedEvent(
    Guid TransferId,
    string Reason,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public FundsHoldFailedEvent(Guid transferId, string reason)
        : this(transferId, reason, DateTime.UtcNow) { }
}

public record HoldCommittedEvent(
    Guid TransferId,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public HoldCommittedEvent(Guid transferId)
        : this(transferId, DateTime.UtcNow) { }
}

public record HoldCommitFailedEvent(
    Guid TransferId,
    string Reason,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public HoldCommitFailedEvent(Guid transferId, string reason)
        : this(transferId, reason, DateTime.UtcNow) { }
}

public record HoldReleasedEvent(
    Guid TransferId,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public HoldReleasedEvent(Guid transferId)
        : this(transferId, DateTime.UtcNow) { }
}

public record TransferCompletedEvent(
    Guid TransferId,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public TransferCompletedEvent(Guid transferId)
        : this(transferId, DateTime.UtcNow) { }
}

public record TransferFailedEvent(
    Guid TransferId,
    string Reason,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public TransferFailedEvent(Guid transferId, string reason)
        : this(transferId, reason, DateTime.UtcNow) { }
}
