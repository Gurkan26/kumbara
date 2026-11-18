namespace Kumbara.BuildingBlocks.Contracts.IntegrationEvents;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
